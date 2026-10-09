# FishGame BUFF 能力线全量清单与消费方取证

## 一、记录范围

本记录接续：`设计文档/AI归档记录/2026-10-9/3-路线B定稿与板块BUFF地基核查.md`

覆盖内容：**2026-10-09**。用户拍板板块 BUFF 走**常驻生效**（上一条归档第八节的选项①），随即提出「还没决定好做哪些 BUFF，先把 BUFF 有哪些列出来，现在有点乱」。本轮是一次**全量能力线盘点 + 消费方取证**，**没有改任何 `.cs` / `.prefab` / `.asset` / `.unity`**。

主设计文档：`设计文档/03-玩法扩展设计-肉鸽背包.md` 第三章、第八章

---

## 二、「乱」的根源诊断（本轮定位）

同一个「能力」这件事目前有 **4 个来源各说各话**，这是乱的结构性原因：

```text
① IFishingStats        8 条字段   Assets/Scripts/Module/Fish/Model/IFishingStats.cs   —— 接口层，设计钦定的唯一出口
② BackpackStatMod      4 条枚举   Assets/Scripts/Module/Backpack/Data/BackpackStatMod.cs —— 数据层，只收「有消费方」的
③ HookConfigSO        另有 13 条  Assets/Scripts/Module/Fish/Data/HookConfigSO.cs   —— 手感段与张力段，接口没覆盖
④ 商店 4 张占位卡       4 个名字   Assets/Scripts/UI/Shop/UIShopPanel.cs rebuildGoods() —— UI 自己拍的名字，没接任何数据
```

且第 ④ 项里 **两张卡根本不该进 BUFF 表**：

```text
「背包扩容 +1 格 60」 → 路线 B 下它就是板块本体，不是 BUFF，是商品形状
「钓竿升阶 120」      → 那是 BackpackItemSO 的档位（tier），是装备成长，不是 BUFF
真正算 BUFF 的只有：「延长鱼线 +8 米」= LineLength、「金币加成 +10%」= CoinBonus（而 CoinBonus 零消费方）
```

「鱼竿升级」这个词在工程里同时指三件事，必须切开：

```text
1 档位 tier      BackpackItemSO.TierShapes / TierValues，rod_training 实测 1→2→3 档
                 形状 多米诺2 → I形3 → 2x3板6，MaxCarryCount 1 → 2 → 4   ← 唯一真在跑的
2 三同合成       设计文档第六章的 SynthesisRuleTableSO 今天不存在（全工程搜不到），合成整条线未实现
3 钓竿配件槽     设计文档第十章待细化第 3 条，未定，且与「板块占格子」冲突 → 建议先不做
```

---

## 三、A 组：接口已有 + 下游真在读（接上零成本，首批直接用）

| 能力线 | 基准 | 消费点（本轮 grep 实测） | 已配装备（TierValues） |
| --- | --- | --- | --- |
| LineLength | 25 m | FishSpawnService.cs:95 / HookViewController.cs:45 | line_extension 6 / 12 / 24 |
| ReelSpeed | 4.5 m/s | ReelingHookState.cs:31 | reel_handle 0.8 / 1.6 / 3.2 |
| MaxCarryCount | 10 条 | FishCarryService.cs:46 | rod_training 1 / 2 / 4 |
| LurePull | 1.0 | FishSpawnService.cs:201 | lure_bait 0.2 / 0.4 / 0.8 |

四件装备资产实测位于 `Assets/Data/Backpack/Items/`，`ModType` 依次为 0 / 1 / 2 / 3，与设计文档 5.3 表一致。

---

## 四、B 组：下游在读但不在接口里（要做 BUFF 就得给 IFishingStats + BackpackStatMod 各加一项）

| 能力线 | 基准 | 消费点 | 玩家语义 |
| --- | --- | --- | --- |
| DescendSpeed | 6 m/s | DescendingHookState.cs:37 | 下潜多快 |
| LateralSpeed | 4 m/s | HookLateralMover.cs:24 | 钩子横移限速 |
| LateralDamping | 8 | HookLateralMover.cs:25 | 跟手程度 |
| HookRadius | 0.15 m | FishingController.cs:186 / 188 | 捕获判定宽度 |
| DetachChance | 0.35 | FishingController.cs:417 | 河豚甩掉已挂鱼的概率，越低越好 |
| TangledDuration | 1.2 s | TangledHookState.cs:31 | 被河豚缠住的僵直时长，越短越好 |
| StashCapacity | 5 | RunBackpack.cs:83 | 暂存架格数（来自 BackpackConfigSO.StashCapacityLimit） |
| ComboStep | 0.15 | FishingController.cs:476 | 同一次回收的连击步长 |
| DepthBonusScale | 1 | FishingController.cs:477 | 深度收益系数 |

注意最后两条挂在 `FishingSessionConfigSO`（DepthBonusFactor / ComboStepPerCatch），属于**关卡难度侧**，与岛屿目标值联动，默认不做成玩家可买 BUFF，除非明确要让「财运」系板能吃深度收益。

---

## 五、C 组：接口声明了但零读取点（配了不生效，正是 BackpackStatMod 文件头禁的「死能力」）

```text
TensionMax 100 / TensionRelief 0 / TensionLoadUnit 12 / TensionDecayPerSecond 5
   全工程只有 HookConfigSO 暴露 + DefaultFishingStats 转发，下游无人读
   设计文档第一章原话「TensionMax = 能拖多大的鱼（本版不做断线）」
   → 整组建议搁置，要做张力类 BUFF 等于要先做断线，另一个量级

DetectRadius 0.8
   同样零读取点，FishDetectionService 用的是 HookRadius + TOUCH_TOLERANCE
   → 想做「声呐」板块得先在 View 或 Detection 层补显示裁剪，中等改动

CoinBonus 0
   收益公式 FishingController.cs:478 那行 Coins = RoundToInt(data.CoinValue * depthBonus * combo) 没乘它
   → 接线成本一行：乘上 (1 + m_stats.CoinBonus)，接口注释已要求实现侧锁 0.6
   → 「财运」阵营最容易让玩家感知，建议首批就把这一行补上
```

---

## 六、阵营归并（板块底色 = 阵营色，本轮给用户的提案）

| 阵营 | 板块淡彩色 | 收的能力线 | 一句话 |
| --- | --- | --- | --- |
| 深潜 | #A8C6E8 | LineLength | 够得着 |
| 迅捷 | #A9D8C0 | ReelSpeed, DescendSpeed, LateralSpeed, LateralDamping | 拉得快、跟手 |
| 负荷 | #F0C89A | MaxCarryCount, StashCapacity | 带得回 |
| 饵香 | #D3BCE8 | LurePull | 钓得到稀有的 |
| 财运 | #F2DE9E | CoinBonus（+可选 ComboStep / DepthBonusScale） | 卖得多 |
| 抗造 | #BFCBD6 | DetachChance↓, TangledDuration↓ | 不怕河豚 |
| 空白 | #EDE0C8 | 无 | 纯容量，对应参考图「空白纸页 · 用于承载道具的纸页」 |

四条定则（避免两层 BUFF 打架，也避免继续变乱）：

```text
1 一个板块只绑一个阵营、一条能力线，不做复合效果
2 板块底色走「低饱和高明度的纸页淡彩」，装备底色保持 UIShopQuality 的高饱和品质纯色
  → 两套色靠饱和度分层，不靠语义硬记，也不会撞色
3 板块给小额常驻，装备给大头；每条线各设封顶（CoinBonus 已锁 0.6，其余照做），两层来源叠起来很容易爆
4 空白板必须存在于奖池，让玩家能只买空间不被迫买 build
```

---

## 七、本轮给用户的首批建议范围

```text
首批 8 条：LineLength / ReelSpeed / MaxCarryCount / LurePull（A 组，枚举与装备已就位）
          CoinBonus（补一行接线）
          DescendSpeed / HookRadius / 抗造组两条（B 组，需扩接口字段 + 枚举项）
不做：Tension 全四条（本版无断线）、DetectRadius（需补裁剪逻辑）、
      RoundDivesCount（设计文档明令「次数本身不做成可升级数值」）、
      BaseRoundTarget / TargetGrowthFactor（玩家能改难度，肉鸽裁决当场失效）
待用户删改：阵营划分表与首批条数，用户尚未确认
```

---

## 八、工作区状态

```text
HEAD  b85338c AI归档（未变）
 M Assets/Data/Config/BackpackConfig.asset          ← 用户自己的编辑，别动别提交
?? 设计文档/AI归档记录/2026-10-9/2-...md            ← 未入库
?? 设计文档/AI归档记录/2026-10-9/3-...md            ← 上一条，未入库
?? 设计文档/AI归档记录/2026-10-9/4-...md            ← 本条
本轮零代码改动，未跑任何菜单 Builder，编辑器运行时状态沿用第 3 条归档第七节
```

---

## 九、下一步

```text
1 等用户对第六节阵营表与第七节首批范围做删改 → 定稿后写进设计文档 03 第三章（扩表）与第五章
2 开工顺序沿用第 3 条归档第九节：数据层 → 玩法聚合层 → 商店层 → UI 层
3 若首批含 CoinBonus，先把 FishingController.cs:478 那行乘上 (1 + m_stats.CoinBonus) 并验证金币真的变多
4 上一轮欠的验证回执仍未收到：关掉 Profiler 窗口后连续改 SO 十几分钟还卡不卡
```
