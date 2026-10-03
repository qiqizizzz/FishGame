# FishGame 设置界面与 UI 嵌套 Canvas 修复记录

## 一、记录范围

本记录接续：无（`设计文档/AI归档记录/` 目录首条记录）
主设计文档：`设计文档/01-主设计文档.md`、`设计文档/03-玩法扩展设计-肉鸽背包.md`
编码规范：`.codex/work_init_CodexAI/04-编码规范-CSharp.md`、`Assets/Scripts/README.md`

覆盖内容：设置界面 `UISetting` 从 0 到可用（预制体 + 脚本 + 事件绑定 + 寻址注册）、音量条改为美术分段刻度、**嵌套 Canvas 导致整个面板不可交互**的根因与修复、`Assets/Scripts/README.md` 中错误的 Canvas 指引订正。

不含内容：玩法层（钩子状态机、鱼群、背包、商店）任何实现，本条记录期间一行 C# 玩法代码都没写。

---

## 二、本次确认的设计 / 协作偏好

### 设置界面交互设计

- 标题「设置」+ 右上角 `Btn_Close`（圆形关闭钮，三态 sprite + `×` 文本）
- 主体三行，左侧汉字标签、右侧 UI 资源：主音量 → 音量条；音效 → 音量条；是否静音 → 开关按钮
- **音量条用美术的分段刻度图（9 段）而非连续滑条 + 圆形手柄**：`Slider` 直接以「段数」为值域（`minValue = 0`、`maxValue = 9`、`wholeNumbers = true`），填充用 `RectMask2D` 遮罩，无手柄
- 静音按钮点击即翻转 `SoundManager.IsStop` 并切换 sprite，不做确认弹窗
- 音量写入用 `slider.normalizedValue`（= `value / maxValue`），直接落到 `SoundManager` 的 0~1 音量字段，不做额外映射

### 协作偏好（延续，务必遵守）

- 优先用 Unity MCP 操作编辑器，**不手改 `.unity` / `.prefab` YAML**
- **不在工程里留辅助 Editor 工具脚本**；一次性改动用 `script-execute` 现场执行完即销毁
- 范围严格执行「其他的别做」，不做未要求的重构与顺手优化
- 用户会在 Rider 里实时编辑文件：动手前必须重读当前磁盘内容，改完提醒 `File → Reload All from Disk`
- 改了 `.cs` 才需要在回复末尾附「📝 代码变更摘要」；**纯归档工作不附，但要明说未修改代码**
- 不涉及模型来源 / 授权 / 合规讨论（非商业项目）

---

## 三、当前实现状态

### 新增 / 改动文件（已全部提交，工作区干净）

```text
Assets/Scripts/UI/Setting/UISetting.cs          新增，设置界面逻辑（本次核心）
Assets/Prefabs/UI/UISetting.prefab              新增，55 GameObject，根节点无 Canvas
Assets/Art/UI/Setting/*.png + .meta              设置界面美术（切片导入）
Assets/Scripts/Framework/QTower/Common/Defines/UIDefines.cs:14    UISetting 常量
Assets/Scripts/Game/GameController.cs:33         Register<UISetting>
Assets/Scripts/UI/MainMenu/UIMainMenu.cs:46      Btn_Setting → Open<UISetting>().Forget()
Assets/AddressableAssetsData/AssetGroups/UI.asset  新增 address「UISetting」label「UI」
Assets/Scripts/README.md                         订正 4 处 Canvas 指引 + 坑表新增 1 行
Assets/Scripts/UI/Setting/UISetting.cs           私有字段 _ 前缀 -> m_（5 个字段）
Assets/Scripts/Framework/QTower/MVC/View/UIAudio.cs        _button -> m_button
Assets/Scripts/Framework/QTower/Common/Res/ResManager.cs   _pool/_prefabPool -> m_pool/m_prefabPool
```

提交记录：`d63b172 设置界面初步` → `fe8e1b2 修复设置界面设置界面完成` → `7a102e3 归档`（本文件初版）。`m_` 前缀统一改动与本文件的这条修订**尚未提交**。

### 预制体层级

```text
UISetting                    RectTransform + UISetting（无 Canvas / 无 CanvasScaler / 无 GraphicRaycaster）
├─ Img_BG                    全屏 stretch，无精灵，色 (0.525,0.351,0.351,1)
├─ Img_Panel                 精灵 Setting menu_1，848x976 @ (0,60)
├─ Txt_Title                 「设置」96 号 @ (0,-530)
├─ Btn_Close                 精灵 _19，180x190 @ (320,-520)；SpriteSwap(pressed=_20 / disabled=_18) + UIAudio
│  └─ Text                   「×」90 号
└─ Bg_Rows                   760x700 @ (0,-49)，VerticalLayoutGroup spacing=50（childControl/ForceExpand 全 false）
   ├─ Row_MainVolume         Txt_MainVolume「主音量」+ Sld_MainVolume   450x120
   ├─ Row_EffectVolume       Txt_EffectVolume「音效」   + Sld_EffectVolume 450x120
   └─ Row_Mute               Txt_Mute「是否静音」 + Btn_Mute（_27/_29，280x180，ColorTint）+ UIAudio

Sld_* 内部：
  Bg_Segments   9 x Img_Tick_0..8（精灵 _31 暗段）
  Bg_Fill       RectMask2D + 9 x 精灵 _30（亮段）  → Slider.fillRect
```

字体全部显式 `MaoKenTangYuan-beta0.12 SDF`（guid `f1e2ac522e2e8854496798d55de6de58`），文字色 `(0.196,0.196,0.196,1)`。该字体是 `Dynamic` + 带 sourceFontFile，所以未烘焙的中文也能渲染。

### UISetting.cs 结构

```text
namespace UI.Setting / public sealed class UISetting : UIBase

  4 个路径常量（UPPER_SNAKE）: MAIN_VOLUME_SLIDER_PATH / EFFECT_VOLUME_SLIDER_PATH / MUTE_BUTTON_PATH / CLOSE_BUTTON_PATH
  [SerializeField] MuteOffSprite / MuteOnSprite          （PascalCase）
  纯私有字段 _mainVolumeSlider _effectVolumeSlider _muteButton _closeButton _muteImage
  OnInit()   FindComponent<T> 取 Slider x2 / Button x2 / Image x1 + AddListener
  OnOpen()   refreshFromSoundManager()
  handleMainVolumeChanged / handleEffectVolumeChanged → normalizedValue 写 SoundManager.TotalVolume / EffectVolume
  handleMuteClicked  → SoundManager.IsStop = !IsStop + refreshMuteVisual()
  handleCloseClicked → GameApp.UIManager.Close<UISetting>()
  refreshFromSoundManager() → SetValueWithoutNotify(音量 * slider.maxValue)，无 _isRefreshing 标志位
```

用户已在 Play Mode 实测：面板可打开、两条音量条可拖动、静音按钮可切换、关闭可返回 —— 「设置界面没问题了」。

---

## 四、关键架构边界

### 嵌套 Canvas 陷阱（本次唯一的真 Bug，必须记住）

```text
现象：面板显示完全正常，但所有按钮点不动、滑条拖不动、整块区域拒绝交互。

根因：Unity 的每个 Graphic 只把自己注册到「离它最近」的 Canvas。
      界面根上挂 Canvas 后，面板的 45 个图元全部注册进面板自己的 Canvas；
      而 UIMainMenu 的 Canvas 只声明了 overrideSorting，没有配套 GraphicRaycaster，
      于是 UISetting 的面板在「射线注册表」里等于不存在。
      于是 UIRoot 上的 GraphicRaycaster 一个都看不见 → 射线恒 0 命中。
      代码位置：GraphicRaycaster.cs:131 取 GraphicRegistry.GetRaycastableGraphicsForCanvas(canvas)。

对照：UIMainMenu.prefab 根上只有 RectTransform（Canvas 计数 0），所以一直正常。

修法：删掉 UISetting 根上的 Canvas。层级顺序交给兄弟次序 —— Open 时刚实例化，天然排在 UIRoot 末尾。
```

**结论：界面预制体根节点只放 `RectTransform`。确实需要独立 `sortingOrder` 时，`Canvas` 与 `GraphicRaycaster` 必须成对添加。**

### UIManager 相关

- `UIManager.Open<T>()` 只查 prefab **根节点** 的 `GetComponent<TView>()`，取不到才 `AddComponent` → 脚本引用**不能用 Inspector 拖**，必须挂在预制体根上
- 缓存分支带 `if (!view.gameObject.activeSelf)` → 面板已开着时重复 `Open` 不会再次触发 `OnOpen`，传入的 `args` 会被丢弃
- `SortingOrder` 只在界面根**有** `Canvas` 时才写入 `overrideSorting` + `sortingOrder`。所以现在 `UIMainMenu`(0) 与 `UISetting`(100) 的 `SortingOrder` 实际都不生效，靠兄弟顺序

### UI 基类

- `UIItemBase.FindNode(path)` 带 `m_nodes` 缓存；`FindComponent<T>(path)` 缺失时 `QLog.Error` 主动报错，不静默跳过（符合规范第八节）
- `UIBase`：`Init()`（`m_isInitialized` 防重入 → `OnInit` 全流程只跑一次）/ `Open(args)` / `Hide()` / `Close(args)`

### 音频

- `SoundManager` 是静态类（`Framework.QTower.Common.Sound`）：`IsStop`（setter 会 Pause/Play BGM）、`TotalVolume` / `BgmVolume` / `VoiceVolume` / `EffectVolume` 全部 `Clamp01`
- `PlayUIEffect(clip, volumeScale = 1f)` 内含 `if (s_isStop) return;`，走常驻 `s_effectSource.PlayOneShot`，不创建临时 AudioSource
- `EnsureAudioSources` 懒建 DontDestroyOnLoad 根 → **场景里不需要手搭任何音频节点**
- `UIAudio`（`Framework.QTower.View`，`[DisallowMultipleComponent]`）：`ClickClip` / `HoverClip` / `VolumeScale`，`Awake` 里 `TryGetComponent(out _button)` + `onClick.AddListener`，自身**不持有 AudioSource**。挂组件即生效，无需在界面脚本里写一行播放代码

---

## 五、场景 / 资源 / 配置状态

### 场景 `Assets/Scenes/Game.unity`

```text
Game      GameScene 脚本；内含 EventSystem（StandaloneInputModule，工程无 Unity.InputSystem 程序集）
UIRoot    RectTransform + Canvas(ScreenSpace-Overlay, sortOrder 0)
                 + CanvasScaler(ScaleWithScreenSize, 参考分辨率 1080x1920, Match 0.5)
                 + GraphicRaycaster          —— 不挂脚本，edit 模式下 childCount = 0
World     第三个根，世界内容
```

- Main Camera `orthographic: 1`
- `GameScene.cs` 对 `Game` 与 `UIRoot` 做 `DontDestroyOnLoad`，`World` 不参与

### Addressables 组（按 YAML `m_GUID:` 条目计数）

```text
Built In Data 3 | Default Local Group 1 | Fish 11 | Packed Assets 1
SFX 3 | Tackle 3 | UI 3（UIMainMenu + UISetting） | Water 3
```

`Library/com.unity.addressables/` 下**无 `aa/` 构建产物**（只有 `AddressablesBuildTEP.json`）→ PlayMode 推断为 Asset Database 模拟模式（**未正面确认服务实例**）。

### 美术

- 音量条素材：`Assets/Art/UI/Setting/UI Settings Buttons.png`（128x240，Point 过滤）。`_0`/`_1` 是 44x12 的整条刻度，pattern `####.####.` = 9 段（每段 4px 实心 + 1px 间隙）；`_30`/`_31` 是单段亮/暗；`_19`/`_20`/`_18` 是关闭钮三态；`_27`/`_29` 是静音 off/on；`_21` 圆钮与 `_25`/`_26` 胶囊条本次弃用
- 鱼 / 钓杆 / 水面模型已导入并按稀有度上色，提交于 `8e116d3`；未用到的贴图已删除

### 配置资产

- **工程内配置类 `*SO.cs` 脚本 0 个、配置 `.asset` 0 个**（只有字体 TMP asset）。M0 的「配置 SO 定稿」完全未开始

---

## 六、当前需要注意的问题

1. **私有字段前缀已定稿：统一 `m_` + camelCase**（以 `.codex/work_init_CodexAI/04-编码规范-CSharp.md:169` 为准）
   工程内所有 `_` 前缀私有字段已改名，共 8 个字段 / 50 处引用：`UISetting.cs` 的 `m_mainVolumeSlider` / `m_effectVolumeSlider` / `m_muteButton` / `m_closeButton` / `m_muteImage`、`UIAudio.cs:29` 的 `m_button`、`ResManager.cs:36-37` 的 `m_pool` / `m_prefabPool`。`[SerializeField]` 保持 PascalCase、`const` 保持 UPPER_SNAKE 未动；非序列化私有字段不进预制体/场景 YAML，改名不影响已接线引用。Unity 重编译后 Console 零错误
   **未处理的残留**：① 全局指令 `C:\Users\wuqian\.codex\AGENTS.md:102` 仍写着 `_` + camelCase（本次冲突的真正来源，属用户全局配置，未经确认不改）；② `SoundManager.cs:25-33` 用 `s_` 前缀的非只读静态字段、`ResManager.cs:35` 的 `caches`（裸 camelCase 静态只读）不在「纯私有字段」这一条内，是否一并统一待定
2. **本机内存压到 89~93%**，Unity 反复报 `The system is running out of memory ... Discarding profiler frames data`。之后 Unity 主进程对 MCP `script-execute` 停止响应（`Failed to invoke after 10 retries`）。那次 Play Mode 里 `UISetting` 未实例化，但 `Editor.log`（`$env:LOCALAPPDATA\Unity\Editor\Editor.log`，约 46MB）**没有**「打开 UI 失败 / 未注册 / 资源加载失败」→ 判定是内存抖动卡住而非加载链路故障（用户此前截图能正常打开面板）。**做 Play Mode 验证前先关多余程序或重启 Unity**
3. **`console_get_logs` 是有界缓存**，会被 OOM 日志刷掉。查真实日志请直接读 `Editor.log`
4. **编辑模式做不了射线测试**：`GraphicRegistry` 在 edit mode 不建表，`GraphicRaycaster.Raycast` 恒返回 0 hits（连全屏图也是 0）。验证嵌套 Canvas 问题要改验 `graphic.canvas == rootCanvas`，或进 Play Mode
5. `Btn_Mute` 用 `Transition.ColorTint` 而**不是 SpriteSwap**：SpriteSwap 在指针移出时会把 `Image.sprite` 复位成序列化值，会覆盖运行时切换的静音精灵
6. **音量与静音不持久化**：没接 PlayerPrefs，退出 Play Mode / 重启游戏后回到默认值
7. `.codex/功能汇总.md:13` 链接了 `../AGENTS.md`，但**仓库根实际不存在 `AGENTS.md`**；同一文件第 22 行称权威规范「9 篇」，实际 `work_init_CodexAI/` 只有 4 篇（01–04）。索引信息过时
8. 本条归档是 `AI归档记录/` 首条，之后接续时按 `YYYY-M-D` 建目录、按 `N-主题.md` 排序号

---

## 七、当前尚未完成

1. **M0 配置 SO 全缺**：`RarityTableSO` → `FishDataSO` → `HookConfigSO` → `FishingLevelConfigSO`（依赖顺序不可颠倒）
2. **`IFishingStats` 接口未立** —— `01-主设计文档.md:314` 明确「必须在 M1 就立起来，哪怕全返回常量」，否则 P1 接背包要返工
3. M1–M5 未开始，M1 = HookController 状态机 + 输入 + 下潜/回收 + LineRenderer 鱼线
4. 玩法音效未导入（`D:\!AI\GameArt\Assets` 有约 453 个可用 OGG）
5. `SoundManager.Preload` 未实现成 `ResManager.PreLoadAssetsByLabel<AudioClip>("SFX")` → `S_ClipDic` 仍为空，只有 `PlayEffect(string)` 路径会被动填充，首次点击有异步延迟
6. 音量/静音持久化（PlayerPrefs）
7. Addressables PlayMode 后端未正面确认；从未做过完整 Play Mode 端到端实测（钓一条鱼的整条链路）

---

## 八、下一步建议

```text
1. 把全局 C:\Users\wuqian\.codex\AGENTS.md:102 的 _ 前缀规则改成 m_，顺手定夺 s_* / caches 这类静态字段前缀
2. 补 M0 配置 SO：RarityTableSO → FishDataSO → HookConfigSO → FishingLevelConfigSO
3. 立 IFishingStats（全常量实现也要立），作为表现层与数值层的唯一口子
4. 开 M1：HookController 状态机 + 输入 + 下潜/回收 + LineRenderer
5. 有空再做两件小事：SoundManager.Preload 按 SFX 标签预载；音量/静音接 PlayerPrefs
```

先重启 Unity / 释放内存再做 Play Mode 相关验证，否则 MCP 会卡住。

---

## 九、工作区注意事项

- 工作区 `D:\CCodes\unity\FishGame`，分支上四次提交均已落库，`git status --porcelain` 为空
- Unity 2022.3.52f1 / URP 14.0.11 / Addressables 1.22.3 / ugui 1.0.0；shell 是 PowerShell 5.1（**不支持 `??` 运算符**）
- 程序化建预制体：`EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive)` 里搭 → `PrefabUtility.SaveAsPrefabAsset(root, path, out ok)` → `DestroyImmediate(root)` → `CloseScene(temp, true)`，不污染 Game 场景
- 一次性改预制体字段：`PrefabUtility.LoadPrefabContents` + `SerializedObject` + `SaveAsPrefabAsset` + `UnloadPrefabContents`（注意是 `UnloadPrefabContents`，不是 `UnloadContents`）
- 渲染截图验证：Canvas 必须 `RenderMode.ScreenSpaceCamera` + `worldCamera` + `cam.Render()`（Overlay 不进 `cam.Render()`）；先 `Canvas.ForceUpdateCanvases()` + `LayoutRebuilder.ForceRebuildLayoutImmediate` + 每个 `TMP_Text.ForceMeshUpdate()`
- `script-execute`：full-code 模式（`className` + `methodName` + `isMethodBody=false`），方法体里**不能用 `return`**；把结果写到 `$env:TEMP\*.txt` 再用 `Get-Content -Encoding UTF8` 读回来
- `script-execute` 里的 API 陷阱（均实测踩过）：避免裸 `Object`（CS0104，用 `UnityEngine.Object`）；`Selectable.Transition` 枚举是 **`ColorTint`**；`AddressableAssetSettingsDefaultObject` 需 `using UnityEditor.AddressableAssets;`；`ExecuteEvents.GetEventHandler<T>` 返回 **GameObject**；`HorizontalOrVerticalLayoutGroup` 属性名 `childControlWidth` / `childForceExpandWidth` / `reverseArrangement`；`Texture2D` 无 `compressionQuality`、`CanvasScaler` 无 `matchMode`、`TMP_FontAsset` 无 `pointSize/fontStyleList`、`PlayerSettings` 无 `activeInputHandler`；`settings.CreateOrMoveEntry(stringGuid, group)` 第一参是 **string**
- `assets-refresh` 返回 `[Success]` 即编译干净
- Unity sprite rect 是**左下角原点**，PowerShell `System.Drawing` 读像素时 `y = texH - (rect.y + rect.h)`；`GetPixel` 判暗棕阈值注意 R 约 140
