# FishGame B 组能力线接入属性包 + BUFF 藏品清单定稿

## 一、记录范围

接续：`设计文档/AI归档记录/2026-10-9/4-BUFF能力线全量清单与消费方取证.md`

覆盖内容：**2026-10-09**。用户认可阵营配色表（数值待调），要求 ① 再列 BUFF 藏品清单表 ② 给「C 组是否删除」的处置结论 ③ **B 组接入 IFishingStats 接口** ④ 一张更清楚的主表。

本轮**改了 7 个 `.cs`**，零 `.prefab` / `.unity` 改动，`.asset` 只有一处是用户自己的编辑。

---

## 二、C 组处置结论（不删字段，但永久除名出 BUFF 词表）

主设计文档 `设计文档/01-主设计文档.md` 原文早就定过口径，本轮逐行核到：

```text
144  「张力（本版不做）：原设计按体型累加张力、满值断线。鱼线不会断；
      钩上设条数名额上限（CarryCountCeiling=10）」
164  「~~卷线器归零后再次受罚~~ / ~~张力断线~~：本版不做，鱼线不会断」
340  「~~FishingTensionChanged~~ | float 0-1 | TensionModel | 本版不做断线，先不发布」
362  「IFishingStats：线长/携带条数上限/收线速度/张力上限/探照半径/金币加成
      …本版下潜层消费线长与携带条数上限，张力上限字段保留但不参与判定」
```

所以「C 组要不要删」的答案是**两半**：

```text
接口字段      不删。作者自己写了「字段保留但不参与判定」，删了等于推翻已定口径，
              而且真做断线那天要把整组字段和注释一起加回来
BUFF 词表     全删。TensionMax / TensionRelief / DetectRadius 一律不得配成商品，
              这正是 BackpackStatMod 文件头「避免配了没人读的死能力」的规矩
```

补一条取证到的事实：张力这条线**不是「已经能接」，是「只写不读」的半成品**——

```text
RarityRow.TensionLoad（配置） → FishDataSO.ResolveTensionLoad(table)
→ FishCarryService.cs:79 写入 CatchRecord.TensionLoad
→ 全工程零读取点
```

即断线的分子（每条鱼贡献多少张力）已经在算，分母（张力累计与断线判定）不存在。
接张力类 BUFF = 先补完整个断线玩法，另一个量级，不在本轮范围。

同理 `IHookRuntime.HookConfig` 在本轮改完后**在状态层已无任何读取点**（手感段 `LineWidth` 由
`FishingLineView` 自己序列化资产引用取），属于可清理项，本轮**没动**，等用户点头。

---

## 三、本轮代码改动（B 组接入，共 7 个文件）

### 3.1 枚举只能追加，不能重排（硬约束）

`BackpackStatMod` 的 0/1/2/3 已被四个 `.asset` 序列化成整数
（`line_extension` ModType 0 / `reel_handle` 1 / `rod_training` 2 / `lure_bait` 3），
所以新项一律从 **4** 往后排：

```text
CoinBonus = 4   DescendSpeed = 5   LateralSpeed = 6   LateralDamping = 7
HookRadius = 8  DetachChance = 9   TangledDuration = 10
```

### 3.2 IFishingStats 新增 6 条 + 3 条标注预留

新增（都带 XML summary）：`DescendSpeed / LateralSpeed / LateralDamping / HookRadius / DetachChance / TangledDuration`
其中 `DetachChance`、`TangledDuration` 是**越小越好**，注释里写明实现侧必须 clamp 到 ≥0。
接口头新增契约段：实现侧自行 clamp 正向系数上限、越小越好字段下限且不得为负。
`TensionMax / TensionRelief / DetectRadius` 的 summary 补「**预留未接线，禁止配成商品 BUFF**」，
并按文档口径排到接口末尾。
文件头补规矩：**只有下游真的在读的字段才有资格进 BUFF 词表**。

### 3.3 消费点替换（HookConfig → Stats）

```text
DescendingHookState.cs:37  runtime.HookConfig.DescendSpeed  → runtime.Stats.DescendSpeed
HookLateralMover.cs:24/25  HookConfigSO config             → IFishingStats stats（删掉 Module.Fish.Data using）
TangledHookState.cs:35     runtime.HookConfig.TangledDuration → Mathf.Max(MIN_TANGLED_DURATION, runtime.Stats.TangledDuration)
                           新增 const float MIN_TANGLED_DURATION = 0.1f 防止叠成 0 后一帧抖动
FishingController.cs:180   HookConfigSO config = hook.HookConfig → float hookRadius = hook.Stats.HookRadius
FishingController.cs:417   m_scene.HookConfig.DetachChance → Mathf.Clamp01(m_stats.DetachChance)
```

`DefaultFishingStats` 同步实现全部 14 条，数值一律 `=> m_config.X`，代码里不出现玩法常量。

### 3.4 CoinBonus 一行接线（财运阵营的地基）

```text
FishingController.cs:485
- Coins = Mathf.RoundToInt(data.CoinValue * depthBonus * combo)
+ Coins = Mathf.RoundToInt(data.CoinValue * depthBonus * combo * (1f + Mathf.Clamp01(m_stats.CoinBonus)))
```

### 3.5 唯一替换点不变

`FishingController.cs:116  m_stats = new DefaultFishingStats(m_scene.HookConfig);`
背包聚合实现（`BackpackFishingStats`）落地时只改这一行，本轮改的 6 个消费点一行都不用再动。

### 3.6 验证

`AssetDatabase.Refresh(ForceSynchronousImport)` 后：
`EditorApplication.isCompiling=False`、Console 零 Error 零 Warning；
反射确认 `IFishingStats` 14 个属性在 `DefaultFishingStats` 上 missing=NONE；
枚举 11 项编号正确；基准值回读
`Line=25 Reel=4.5 Descend=6 Lat=4/8 Radius=0.15 Detach=0.35 Tangled=1.2 Carry=10 Lure=1 Coin=0 Detect=0.8 Tension=100/0`
与 `HookConfigSO` 资产一致 ⇒ 纯替换、零行为漂移。

---

## 四、主表：能力线 × 阵营 × 接线状态 × 额度（数值全部为初值，待调）

见本目录同轮回复的第一张表。定则不变：
一个板块只绑一个阵营一条线；板块走低饱和纸页淡彩，装备保持 UIShopQuality 高饱和品质色；
板块给小额常驻、装备给大头，每条线各设「板块+装备」合计封顶。

关键口径：

```text
MaxDepth = Mathf.Min(ShaftDepth, Stats.LineLength)  → 线长溢出天然被井深夹住，不用额外封顶
HookViewController 的 Stats 属性早已存在（:36/37），状态层读 Stats 有先例
   （ReelingHookState.cs:31 的 runtime.Stats.ReelSpeed 是本轮改法的一模一样的样板）
StashCapacity 不进 IFishingStats：接口文件头明令「下潜层禁止读任何背包概念」，
   它归 P2 的「背包侧属性包」，别混进这张表
```

---

## 五、价格口径纠正（本轮核到）

`BackpackItemSO` **没有** Price 字段（只有 ItemId/ItemLabel/ItemIcon/ModType/TierShapes/TierValues）。
售价唯一来源是 `BackpackConfigSO.TierPriceList = 80 / 200 / 560`，按**档位**统一定价。
所以藏品表不需要逐件售价列，只有「档位→形状→数值」是逐件配的。
`GridShapeSO` 形状库现有 5 个：`domino_2(2格) / corner_3(3格) / line_3(3格) / block_4(4格) / slab_6(6格)`，
`MIN_CELLS=2`、`MAX_CELLS=6`、必须四连通。

已上架四件的实测档位（读 `.asset` 得到，非设计文档抄录）：

```text
line_extension  Mod0 LineLength     domino_2 / corner_3 / slab_6  +6 / +12 / +24
lure_bait       Mod3 LurePull       domino_2 / line_3   / block_4 +0.2 / +0.4 / +0.8
reel_handle     Mod1 ReelSpeed      domino_2 / corner_3 / block_4 +0.8 / +1.6 / +3.2
rod_training    Mod2 MaxCarryCount  domino_2 / line_3   / slab_6  +1 / +2 / +4
```

`Assets/Editor/BackpackItemLibraryBuilder.cs`（菜单「Tools/钓鱼游戏/生成默认装备库」）
里四张并行数组 `m_itemIds / m_itemLabels / m_itemMods / m_tierShapeIds / m_tierValues`
就是藏品表的落地位置：表定稿后**只需往这四个数组追加**即可批量生成资产，
`writeStartingItem` 只在 `StartingItemList` 为空时写一次，不会覆盖用户手配。

---

## 六、工作区状态

```text
HEAD  b85338c AI归档（未变，本轮未 commit）
 M Assets/Data/Config/BackpackConfig.asset              ← 用户自己的编辑，别动别提交
 M Assets/Scripts/Module/Backpack/Data/BackpackStatMod.cs
 M Assets/Scripts/Module/Fish/FishingController.cs
 M Assets/Scripts/Module/Fish/Model/DefaultFishingStats.cs
 M Assets/Scripts/Module/Fish/Model/HookState/DescendingHookState.cs
 M Assets/Scripts/Module/Fish/Model/HookState/HookLateralMover.cs
 M Assets/Scripts/Module/Fish/Model/HookState/TangledHookState.cs
 M Assets/Scripts/Module/Fish/Model/IFishingStats.cs
?? 设计文档/AI归档记录/2026-10-9/2-...md 3-...md 4-...md 5-...md
用户在 Rider 并行编辑过工程，本轮改动前已确认 3 小时内无 .cs 落盘改动，无冲突
```

---

## 七、下一步

```text
1 等用户砍藏品表（哪几件不要 / 哪件换阵营 / 数值改多少）
2 定稿后：扩 BackpackItemLibraryBuilder 的四张数组 → 跑菜单生成资产
3 P1 板块：新建 BackpackPlateSO（阵营 + 能力线 + 每格额度），板块入包 API
4 P1 商店：Module/Shop（库存 + 随机刷新 + 定价）、RunState.TrySpend、商品卡 Button、UI 拖拽 + 右键旋转
5 P2 聚合：BackpackFishingStats 实现 IFishingStats，替换 FishingController.cs:116 那一行
6 P2 背包侧：StashCapacity 单独属性包
7 上一轮欠的验证回执仍未收到：关掉 Profiler 窗口后连续改 SO 十几分钟还卡不卡
```
