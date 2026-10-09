# FishGame 路线 B 定稿与板块 BUFF 地基核查

## 一、记录范围

本记录接续：`设计文档/AI归档记录/2026-10-9/2-改SO进程卡死取证与商店占位现状.md`

覆盖内容：**2026-10-09 上午末段**。用户在上一条归档第九节的两条路线里选定**路线 B**（商店刷异形多格板块，玩家旋转 + 位移拼进背包），并提出两件事：板块**颜色要不一样**、参考图里的纸页都带 BUFF，**咱们要不要加**。本轮只做读盘核查与口径定稿，**没有改任何 `.cs` / `.prefab` / `.asset` / `.unity`**。

主设计文档：`设计文档/03-玩法扩展设计-肉鸽背包.md`

不含内容：任何实现改动。板块 BUFF 的**生效时机**仍待用户拍板（见第八节），拍板后才开工。

---

## 二、用户本轮原话要点

```text
1 「B」→ 放弃路线 A（买单格），采用异形多格板块 + 旋转 + 位移
2 「异形多格版块就是颜色需要不一样」→ 板块要按某种语义分色
3 「我才发现他这些纸页都有 BUFF 啊，咱们也要加吗」→ 参考图 12 张纸页每张带一个 BUFF，问本项目是否跟进
```

参考图为某游戏的道具图鉴「纸页」页签，12 种纸页形状各异（2x2 空白纸页、1x2 迅捷黄沙、2x2 精准星星、2x3 血量面包、L 形火焰燃燃 / 砰砰爆爆、1x3 咔嚓光噪 / 嗞嗞带电、2x2 迅捷剑士 / 勇猛拳手 / 损伤术士、2x2 金币猎人），**每张底色不同 = 不同 BUFF 阵营色**，详情栏写「空白纸页 / 冒险家 / 用于承载道具的纸页」。

---

## 三、路线 B 定稿：逐级解锁模型退役，改成「板块铺地板」双层掩码

### 已定的口径变更

```text
选 B 之后，「一次买推进一级、一次开一整片预画掩码」这套模型必须退役，
不能再和「买板块自己拼」并存，否则同一个背包有两套扩容权威。

退役内容：
  BackpackConfigSO.UnlockMaskList   只留 [0]，降级为单一字段「开局底板掩码」
  BackpackConfigSO.ExpansionCostList 整条删除（长度 = 级数 - 1 的语义随之消失）
  BackpackConfigSO.UnlockMaskAt(stage) / ExpansionCostAt(i) 去掉 stage 维度
  编辑器绘制器「增级 / 删末级 / 级切换」整排工具条（B1 的 bug 宿主一起消失）
保留内容：
  开局底板 = 参考图里「已经发给你的第一张纸页」，仍然需要
```

### 为什么改动比看上去小（本轮读盘确认）

`Assets/Scripts/Module/Backpack/Model/BackpackModel.cs` 里两层掩码本来就是分开的：

```csharp
private long m_unlockMask;    // 哪些格「能放东西」
private long m_occupiedMask;  // 哪些格「已经被物品占着」
public int UnlockedCells => countBits(m_unlockMask);
public int UsedCells => countBits(m_occupiedMask);
public int FreeCells => UnlockedCells - UsedCells;
```

且已具备三条路线 B 直接可用的能力：

```text
1 TrySetUnlockMask(long) 已带两条校验：新掩码不得收缩旧掩码、必须盖住全部已放物品
   → 板块入包 = 对这张方法再喂一个「板块位图并集」，校验逻辑一行不用改
2 TryPlace / TryMove / TryRotate 已为物品实现，网格合法性只在这一处裁决
   → 用户要的「旋转 + 位移」操作乐趣，数据层现成，缺的是 UI 输入
3 GridShapeSO 已预计算 4 个朝向（OrientationAt 自动回绕），MIN_CELLS=2 / MAX_CELLS=6 / 必须四连通
   → 板块形状直接复用同一批 GridShapeSO 资产，不必新建形状体系
```

### 落地后的双层掩码语义（待实现）

```text
底板/板块层：m_unlockMask = 开局底板掩码 | 所有已入包板块的位图并集
物品层：    m_occupiedMask 照旧只统计装备
同一格允许「底下是板块、上面是装备」，这是两层的正交性，也是参考图纸页的做法
板块之间不得重叠；装备只能落在 m_unlockMask 覆盖的格子上
→ 「让格子得到充分利用」从口号变成位运算约束
```

---

## 四、板块要不要挂 BUFF：要加，地基早就是为它挖的

用户（原作者）当初留下的三处证据，本轮逐一读盘核实：

```text
1 Assets/Scripts/Module/Fish/Model/IFishingStats.cs 文件头原文
   「下潜属性包接口，下潜层与背包层之间的唯一接口」
   「背包物品聚合出这个只读属性包，下潜层禁止读任何背包概念」
   「接口先立起来是为了防止下潜层散落硬编码深度与容量常量，背包接上时零返工」
2 Assets/Scripts/Module/Fish/Data/HookConfigSO.cs 文件头原文
   「能力基线段是 IFishingStats 的默认值来源：常量实现从这里取初值，
     背包层接上后由背包物品在其上叠加修改」
3 Assets/Scripts/Module/Backpack/Data/BackpackStatMod.cs（本轮新发现，上一条归档没细看）
   枚举已存在：LineLength / ReelSpeed / MaxCarryCount / LurePull
   文件头原文「背包物品可修改的能力线，与 IFishingStats 一一对应」
   「只列当前真正有消费方的四条线，张力与显形半径等无消费方的能力先不进表」
   「新增枚举项前必须先在玩法层找到读取点，避免配了没人读的死能力」
```

结论：板块挂 BUFF **不新增接口、不碰下潜层**，只是给同一张 `BackpackStatMod` 多一个来源。唯一替换点是
`Assets/Scripts/Module/Fish/FishingController.cs:116  m_stats = new DefaultFishingStats(m_scene.HookConfig);`
消费方全部走接口：`HookViewController.cs:45`、`FishCarryService.cs:46`、`FishSpawnService.cs:95/201`、`ReelingHookState.cs:31`。

另一条理由：商店里那四张占位卡（钓竿升阶 / 延长鱼线 +8 米 / 背包扩容 +1 格 / 金币加成 +10%）本来就是一份现成的 BUFF 词表，
`UIShopPanel.rebuildGoods()` 里硬编码无处落地。板块带 BUFF 之后，它们才第一次有了归属。
参考图 12 张纸页的名字（迅捷 / 精准 / 血量 / 金币猎人）本质也是「能力线 + 花色」，和这套枚举同构。

### 必须同时定的分层规则（否则两层 BUFF 会打架）

```text
现在 BUFF 挂在装备上（BackpackItemSO.ModType + TierValues），板块再加一层就出现两个来源。
建议分层，不是简单叠加：
  装备 = 主力强度，走 TierValues 绝对值，仍按档位换形状（设计文档 5.3 已配 4 件）
  板块 = 方向与偏向，给小额 / 百分比 / 上限类修正，走阵营色
  保留 board_blank「空白板块」= 纯容量无 BUFF，对应参考图第一张「空白纸页 · 用于承载道具的纸页」
        → 让玩家能只买空间不被迫买 build，这一点参考图自己也做了
  建议板块不做品质档位，档位只留给装备，少一个维度就少一层数值膨胀风险
铁律沿用设计文档第八章：「所有藏品只能修改 IFishingStats 的字段，不得引入任何新机制」
  → 板块同受此约束，只准动那四条线
```

---

## 五、颜色语义：用户提的「颜色要不一样」要拆成两条通道

本轮读盘确认现有颜色通道**已被「档位」占用**：

```text
Assets/Scripts/UI/Shop/UIShopQuality.cs
  White #C9CDD2 / Blue #3E7BFA / Purple #8B5CF6 / Gold #F5B93B
  TierColor(tier) 1..3 走白蓝紫，4+ 给金；LabelColor 按亮度切深棕 #4A3220 / 浅米 #F5E6C8
Assets/Scripts/UI/Shop/UIShopItemView.cs
  物品按形状逐格铺方块：底色 = 品质色压暗 28% 当暗边，内缩 4 px 亮面 = 品质色本身
```

若板块底色也走 TierColor，玩家就分不清「这块板强不强」和「这块板管哪条线」。定稿分层：

```text
板块 / 纸页底色  = BUFF 阵营色（新增一张 UIBuffPalette，一条能力线一个色，空白板给米白）
装备底色        = 品质色（UIShopQuality.TierColor 原样不动）
板块上的稀有度   不用颜色表达，靠格数 / 价钱 / 刷新权重区分
```

---

## 六、本轮读盘新发现：四条 IFishingStats 字段目前是「声明了但没人读」

grep 全 `Assets/Scripts`（`stats.XXX` / `Stats.XXX` 两种写法）实测结果：

```text
有消费方（BackpackStatMod 已收录，板块 BUFF 首批只能用这四条）
  ReelSpeed      ReelingHookState.cs:31
  MaxCarryCount  FishCarryService.cs:46
  LineLength     FishSpawnService.cs:95 / HookViewController.cs:45
  LurePull       FishSpawnService.cs:201

零消费方（只在 HookConfigSO 暴露、DefaultFishingStats 转发，下游无人读）
  TensionMax / TensionRelief / DetectRadius / CoinBonus
  → 「金币加成 +10%」这条占位卡背后的 CoinBonus 今天接上也不会长金币，
     要真生效得先在 FishingController 的产出公式里乘一次（477 / 485 行的 depthBonus · combo 那条链）
  → 这与 BackpackStatMod 文件头「避免配了没人读的死能力」的自订规矩一致，首批不要往里塞 CoinBonus
```

---

## 七、工作区状态

```text
HEAD  b85338c AI归档（未变）
 M Assets/Data/Config/BackpackConfig.asset      ← 用户自己的编辑，别动别提交
?? 设计文档/AI归档记录/2026-10-9/2-改SO进程卡死取证与商店占位现状.md   ← 上一条归档，未入库
?? 设计文档/AI归档记录/2026-10-9/3-路线B定稿与板块BUFF地基核查.md     ← 本条
本轮没有修改任何 .cs / .prefab / .asset / .unity，没跑任何菜单 Builder → 无「代码变更摘要」
编辑器侧仍保持上一条归档第三节的运行时改动：ProfilerDriver.enabled = False、
  Profiler.DefaultRecordState = 0、已 ClearAllFrames
```

---

## 八、待用户拍板的一个分叉：板块 BUFF 的生效时机

这是唯一会显著改变实现复杂度的取舍，选完就开工：

```text
① 常驻生效    板块放入背包即加 BUFF
             最省，聚合只多扫一层板块；缺点是「形状」和「BUFF」两件事互相不产生张力
② 覆盖生效    板块被装备盖住的格子数决定 BUFF 额度（空格不给）
             把「让格子得到充分利用」直接变成数值收益，异形 + BUFF 互相成就
             实现只比①多一句「数板块位图与 occupiedMask 的交集」
③ 相邻生效    板块与某类装备相邻 / 同色才激活
             空间解谜深度最高（背包战斗的正味），但要邻接计算 + 匹配规则表，属于另一个量级
本条归档作者的推荐：②，性价比最高且最贴用户原话「通过旋转、位移等操作让格子得到充分的利用」
```

---

## 九、下一步

```text
1 用户回答第八节的生效时机 → 定范围开工（预计顺序：
   a 数据层：板块 SO（形状 + 阵营 + 数值 + 价）、RunBackpack 板块入包 API、BackpackModel 解锁掩码改并集
   b 玩法层：新增 IFishingStats 聚合实现，替换 FishingController.cs:116 一行
   c 商店层：新建 Module/Shop（库存 + 随机刷新 + 定价），RunState 补 TrySpend
   d UI 层：UIShopGoodsItem 补 Button/onClick，UIShopSlotItem + UIShopItemView 补拖拽与右键旋转
2 顺带问用户：关掉 Profiler 窗口后连续改 SO 十几分钟还卡不卡（上一条归档第八节第 1 条的验证回执）
3 B1 / B2 两个编辑器 bug：B1 的宿主（删末级按钮）会随逐级解锁退役一起消失，B2 的
   无条件 ApplyModifiedProperties 与无 Undo.RecordObject 仍在，改绘制器时一并处理
4 设计文档 03 需要跟着改写：第五章「解锁范围」那一行、5.1 开局锚形的口径、
   第九章 P1/P2 的交付判定，都要从「推进解锁阶段」改成「板块铺地板」
```
