# FishGame 改 SO 进程卡死取证与商店占位现状

## 一、记录范围

本记录接续：`设计文档/AI归档记录/2026-10-9/1-会话交接快照与回收判定复查线索.md`

覆盖内容：**2026-10-09 上午后半段**。用户报「每次改一下这个 SO 就直接进程卡死」，本轮只做取证与定性，**没有改任何 `.cs` / `.prefab` / `.asset` / `.unity`**。同时把上一条归档里欠用户的「商店是不是占位符」「逐级解锁到底在解锁什么」两条口径一次说清。

不含内容：任何实现改动。修哪几处、按哪种格子模型重做商店，等用户点头。

> 更正上一条：10-9/1 第五节第 4 条写的 `UnlockMaskList` 只有 `0x04040703`（7 格锚形）已过期，现值见本条第六节。

---

## 二、卡死取证：结论是 Unity Profiler 解析，不是 SO 逻辑

### 决定性证据（用户截图）

弹窗原文：

```text
Profiling Progress
Parsing Profiler Data: 0/17 KB processed        [Cancel]
```

截图时间 09:24，右侧 Inspector 正是 `Backpack Config (Backpack Config SO)`，画布 6×5、第 1 级 5 格。
这条模态进度条属于 **Unity 编辑器自己的 Profiler 数据解析**，跑在主线程上，卡住就等于整个进程无响应。
它**不是**资源导入框（导入框文案是 `Asset Pipeline` / `Importing`），所以「改 SO 触发导入死循环」这个方向可以排掉。

### 佐证链

```text
1  Windows 事件日志（Application）里 Unity.exe 的 AppHangB1 至少 6 次，跨 10-07 / 10-08 / 10-09，
   签名完全一致（P1 Unity.exe  P2 2022.3.52.8444  P3 67215a1e  P4 b4a3  P5 67246080），
   哈希桶 1692291705555856341 也相同 → 每次挂在同一处，不是偶发
   最近一次 10-09 09:27:13（Unity Hub.exe 在 09:27:07 一起挂过一次），随后 09:27:21 重启编辑器
2  取证时 UnityEditor.ProfilerWindow 处于打开状态（Resources.FindObjectsOfTypeAll<EditorWindow> 共 14 个窗口）
3  EditorPrefs：Profiler.DefaultRecordState = 1 → Profiler 窗口一开就自动开始录制
4  ProfilerDriver.usedHeapSize 约 221 MB；上一会话退出时的内存统计里
   ALLOC_PROFILER 132.7 MB / ALLOC_PROFILER_EDITOR 261.9 MB 常驻 27956 帧
5  物理内存 15.2 GB，本轮实测空闲仅 1.8 GB（Unity 私有 3.3 GB + Rider 1.9 GB + Rider.Backend 0.5 GB）
   → 解析期撞上换页风暴的概率不低，这会让「卡住」更像「死锁」
```

### 已经排除的（都是本轮亲自读盘 / 实测，不是二手结论）

```text
X  SO 或自定义 Inspector 自激循环
   Assets 全量 114 个 .cs：InitializeOnLoad=0  AssetPostprocessor=0  DidReloadScripts=0
   MenuItem 只有 6 个手动菜单 Builder/Validator；SaveAssets/Refresh 只出现在这些手动入口里
   BackpackConfigSO.OnValidate 只读校验 + QLog.Error，不写回字段、不 SetDirty → 不会自激
   MaskPainterControl.paint 只有 for/foreach，无 while/goto；不合规时退回 committed 不写脏
X  MCP 插件（com.ivanmurzak.unity.mcp@0.93.2）触发解析
   全包 531 个 .cs：LoadProfile=0  SaveProfile=0  .Wait()=0  .Result=0  GetAwaiter().GetResult()=0
   Thread.Sleep=0  Monitor.Enter=0  lock(=0  Join(=0
   只有 ProfilerDriver.ClearAllFrames 一个入口，且日志采集走 logMessageReceivedThreaded
   → 注意：上一轮 grep 只扫了 Assets 和 Packages/，UPM 注册表包实际在 Library/PackageCache/，本轮补扫
X  「无限重绘」假说（外部 AI 给的：注释掉 OnValidate 试试）
   选中资源、自定义 Inspector 在画、空转 6 s，进程 CPU 增量 62 ms / 6000 ms → 不成立
   且 OnValidate 反射直调 5 次 = 0 ms，new StackTrace(false) 5 次 = 0 ms
```

### 证据链的缺口（诚实记录）

```text
a  卡死那次的现场日志没留下：Editor-prev.log 结尾是 10-08 18:51 的干净退出，
   09:00~09:27 那个会话的 Editor.log 已被覆盖/轮转，拿不到「改 SO → 弹解析框」的时序
b  WER 报告目录（C:\ProgramData\Microsoft\Windows\WER\ReportArchive\Critical_Unity.exe_*）
   普通权限 Access Denied，Report.wer 与栈信息读不到 → 需要管理员权限或配置 LocalDumps
c  本轮没有做「开着录制再改 SO」的受控复现，因为一旦命中就是再杀一次用户的编辑器
   已做的差分只到「录制关着改 SO = 187 ms 正常返回」这一侧
```

### 最可能的链路（标注为推断）

改 SO → 资源导入 + Inspector 连续重绘，产生一帧特别长的编辑器帧 → 自动录制中的 Profiler 窗口要吞下并解析这帧数据
→ 在 221 MB 常驻 profiler 堆 + 1.8 GB 空闲内存的条件下解析卡在主线程。
所以「改 SO」只是扳机，炸药是 Profiler 窗口。

---

## 三、本轮对编辑器运行时状态的改动（全部可逆，未落盘到工程）

```text
ProfilerDriver.enabled                    True → False（上一轮已改，本轮确认仍为 False）
EditorPrefs Profiler.DefaultRecordState   1 → 0   ← 本轮新改，取消「Profiler 窗口一开就自动录制」
ProfilerDriver.ClearAllFrames()           本轮调用一次，丢弃已积累的帧（usedHeapSize 是分配器高水位，不会回落）
Selection.activeObject                    BackpackConfig.asset（上一轮）
对该 asset 做过一次 SetDirty + SaveAssets + Refresh，资源 MD5 前后一致（上一轮）
没动 .cs / .prefab / .asset / .unity，没跑任何菜单 Builder
想恢复自动录制：Window → Analysis → Profiler 里把 Auto Recording 勾回来，
或 EditorPrefs Profiler.DefaultRecordState 写回 1
```

---

## 四、商店现状：确实全是占位符

```text
Assets/Scripts/UI/Shop/UIShopPanel.cs  rebuildGoods() 注释原文
   「占位：商品池与售价都还没定，这里只是把一排卡片铺出来看版式」
   四张卡硬编码：钓竿升阶 120 / 延长鱼线 +8 米 30 / 背包扩容 +1 格 60 / 金币加成 +10% 90
Assets/Scripts/UI/Shop/UIShopGoodsItem.cs  只有 Setup(name, price, color)，没有 Button、没有 onClick
   → 商品卡点不动是事实，不是 bug，是根本没接
handleRefreshClicked()  只把同样四张卡重铺一遍，刷不出新内容
面板绑定的按钮只有三个：Btn_Dive、Btn_Close（都发 EventDefines.ShopExitRequested）、Btn_Refresh
RunState.cs  Coins { get; private set; }，只有 Coins += dive.Coins
   全工程 grep：TrySpend / SpendCoins / void Spend = 0 命中 → 扣款 API 不存在
Assets/Scripts/Module 下只有 Backpack / Fish / Timer，没有 Shop 模块 → 没有库存、没有随机刷新
UIShopSlotItem.cs  只有 SetEmpty / SetLocked / SetStored，不能点不能拖
UIShopItemView.cs  只负责按格子画物品方块，没有输入
→ 用户设想的「拖拽 + 右键旋转 + 位移」一行都没实现
```

## 五、「逐级解锁」当初的口径 vs 用户的心智

现有配置层口径（上一条归档沿用下来的设计）：

```text
UnlockMaskList      一整个「级」= 一整片预画好的掩码，买一块扩容板就推进一级、一次开一整片
ExpansionCostList   长度 = 级数 - 1，第 i 项是从 i+1 级买到 i+2 级的价钱
BackpackModel.TrySetUnlockMask(long)   全工程只有 Editor 自检脚本 BackpackShapeValidator.cs 调过，玩法侧零调用
BackpackConfigSO.ExpansionCostAt()     零调用
→ 所以「购买 → 开格」这条闭环整体不存在，用户看不到它工作是对的
```

用户本轮明确表达的心智：

```text
格子本身是商品：商店随机刷出格子 → 花钱买 → 买来的格子落到背包的哪个位置由玩家自己定
   → 再通过旋转 / 位移把有限空间用满
现状是「一次白送一整片、位置由配置写死」，没有取舍，也没有操作乐趣
→ 两者不是一回事，这一条要在下一轮正面回答，不能再按旧口径往下做
```

---

## 六、工作区状态

```text
HEAD  b85338c AI归档（用户自己提交，10-9/1 归档已入库）
 M Assets/Data/Config/BackpackConfig.asset   ← 用户自己的编辑，别动别提交
   UnlockMaskList = 0x00040C0C → 5 格：(r0,c2)(r0,c3)(r1,c2)(r1,c3)(r2,c2)
   ExpansionCostList 空；TierPriceList = 80 / 200 / 560；StashCapacityLimit = 5；Grid 6×5
   即：只有 1 级、5 格，与截图 Inspector 显示的「第 1 级 · 5 格」一致
```

---

## 七、已发现未修的真 bug（等用户点头再动）

```text
B1  Assets/Editor/BackpackConfigSOEditor.cs  drawStageListToolbar()「删末级」
    masks 删空后 costs.arraySize - 1 == -1 → DeleteArrayElementAtIndex(-1) 抛异常
    用户当前正是「1 级 + costs 空」，这个按钮一点就中
B2  BackpackConfigSOEditor.OnInspectorGUI 每趟 GUI 结尾无条件 serializedObject.ApplyModifiedProperties()
    drawStages 里 Paint(...) 用 out _ 丢弃 edited，painted != committed 时写 element.longValue + SetDirty
    → 一次点击 = SetDirty + 写盘；且全程没有 Undo.RecordObject，Ctrl+Z 撤不回格子编辑
B3  MaskPainterControl 的 m_paintMode 是静态字段，多 Inspector 同时拖会串（当前只有一处用，风险低）
B4  商店购买闭环缺失（第四节），拖拽 + 右键旋转未实现
```

## 八、下一步

1. 用户按第三节口径验证：关掉 Profiler 窗口（或保持 Auto Recording 关）后连续改 SO 十几分钟
   - 不再卡 → 定性成立，后续把 Profiler 窗口当外部工具用，别常驻
   - 仍卡 → 本条判错，下一批嫌疑对象：内存压力（1.8 GB 空闲）、Addressables/Animator/Animation/Audio Mixer/Lighting/UVCSTool 六个常驻窗口
2. 要拿栈就配 WER LocalDumps（`HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps`，DumpType=2）
   或用管理员权限读 `ReportArchive\Critical_Unity.exe_*\Report.wer`
3. 商店按用户心智重做还是保留旧口径，等回答后再定范围（见第九节问题）
4. B1 一行修复随时可做，等点头

## 九、留给用户的问题

背包格子的模型二选一，两条路工作量差很多，需要用户定：

- 路线 A：商店每次随机刷出**单个格子**，买一格就在背包里多一格的可放置位，**位置由玩家自己选**
- 路线 B：商店刷出**异形多格板块**（现有 `GridShapeSO` 的 line_3 / corner_3 / domino_2 / block_4 等），买下来后由玩家**旋转 + 位移**拼进背包

---

## 十、工具与环境坑

```text
PowerShell：foreach 语句不能直接接管道；带 Test-Path 的 if 块结果要先收进 List 再 Out-String
Get-Content -Raw 输出会被截断，长文件用 -TotalCount / -Skip+-First 分段
WER 的 ReportArchive 子目录默认拒绝普通权限读取
script_execute：using System.Diagnostics 与 UnityEngine.Debug 撞 CS0104；
  Object.FindObjectsOfType<EditorWindow>() 返回 0，要用 Resources.FindObjectsOfTypeAll<EditorWindow>()
UPM 注册表包的源码在 Library/PackageCache/<pkg>@<ver>/，不在 Packages/ 下，grep 第三方代码时别漏
单引号 here-string 结尾的 '@ 必须顶格；写成 '@ | Out-Null 会把整段内容丢掉，不会报错
```
