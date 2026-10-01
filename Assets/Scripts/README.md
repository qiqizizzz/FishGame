# QTower —— Unity 轻量 MVC 框架

> `Assets/Scripts` 是 FishGame 的底层目录，也是可以直接搬进新项目的框架代码。
> 它与玩法无关，只负责四件事：**全局事件总线、UI 生命周期、资源加载与对象池、计时器**。

- 引擎版本：Unity **2022.3.52f1 LTS**
- 渲染管线无关：框架代码不引用 URP / Built-in 任何东西，两种工程都能用
- 规模：24 个 `.cs` + 3 个 `.asmdef`
- 用法：整个目录拷进新项目 `Assets/Scripts`，照下面的清单配包、配场景即可跑

---

## 一、导入需求

### 1.1 必装包

| 包 | 当前版本 | 为什么是必需 |
| --- | --- | --- |
| `com.unity.addressables` | 1.22.3 | `ResManager` 直接调 `UnityEngine.AddressableAssets.Addressables`，缺包**编译都过不了** |
| `com.cysharp.unitask` | git | `UIManager.Open` / `ResManager` 的返回类型是 `UniTask`，暴露在公开 API 上 |
| `com.unity.ugui` | 1.0.0 | `Canvas` / `GraphicRaycaster` / `Button`，UI 层的地基 |
| `com.unity.textmeshpro` | 3.0.7 | 框架本身不引用，但业务界面基本都要用 |

`Packages/manifest.json` 可以直接粘这几行：

```json
{
  "dependencies": {
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
    "com.unity.addressables": "1.22.3",
    "com.unity.textmeshpro": "3.0.7",
    "com.unity.ugui": "1.0.0"
  }
}
```

UniTask 用 Asset Store 的 unitypackage 也行，但必须连同它自带的 `UniTask.asmdef` / `UniTask.Addressables.asmdef` 一起导入 —— 只要目标工程里有 `QTower.asmdef`，就绕不开这两个程序集。

### 1.2 不要顺手装的

- `com.unity.inputsystem`：**本工程没装**，输入走旧 Input Manager，`EventSystem` 用 `StandaloneInputModule`。新项目如果装了 Input System 并且切了 active input handler，场景里的 `EventSystem` 必须换成 `InputSystemUIInputModule`，否则 UI 全部点不动
- `com.ivanmurzak.unity.mcp`：AI 调试插件，只服务于开发期，交付出去的工程不要带
- DOTween：框架没有依赖。想要补间就在 `QTower.asmdef` 里加 `DG.Tweening` 引用，或者只在 `Assembly-CSharp` 层用

### 1.3 程序集边界

| asmdef | 覆盖目录 | 引用 | 说明 |
| --- | --- | --- | --- |
| `QTower` | `Framework/QTower`（不含 `Editor/`） | `Utils`、`Unity.Addressables`、`Unity.ResourceManager`、`UniTask`、`UniTask.Addressables` | `autoReferenced = true` |
| `QTower.Editor` | `Framework/QTower/Editor` | 仅 `QTower` | `includePlatforms: ["Editor"]`，不进包体 |
| `Utils` | `Utils` | 空 | 零依赖叶子层 |
| `Assembly-CSharp` | `Game`、`Module`、`UI` | 自动引用以上全部 | **故意不建 asmdef**，业务层不用维护引用列表 |

> **两个迁移必踩的坑**
>
> 1. `Framework/QTower/QTower.asmdef` 的 `references` 里写的是 **3 个 GUID**。其中 `1e6a16ebd49fdb74fad1843f95457608` 在当前工程已经找不到对应程序集（历史遗留空引用），跨工程迁移时另外两个也可能失配。换工程后第一件事就是打开这个文件，把 GUID 换成按名字引用：
>    `"references": ["Utils", "Unity.Addressables", "Unity.ResourceManager", "UniTask", "UniTask.Addressables"]`
> 2. `QTower` 与 `Utils` 的 `autoReferenced` 必须保持 `true`。改成 `false` 之后 `Game/`、`Module/`、`UI/` 会立刻找不到 `GameApp`、`UIManager`，报一堆 `CS0246`

### 1.4 依赖方向

```text
        Game                       业务层：组合根 + 场景入口
       /     \
    UI       Module                UI = 界面，Module = 可复用玩法系统
       \     /
           v
   Framework/QTower    <   QTower/Editor
           v
        Utils                      零依赖工具层
```

- 引用只允许自上而下：业务层 → 框架层 → 工具层
- **禁止** `Framework/QTower`、`Utils` 反向引用 `Game`、`UI`、`Module`
- 新玩法系统放 `Module`，新界面放 `UI`，通用能力才放 `Framework/QTower`

### 1.5 Addressables 配置（在 `Assets/Scripts` 之外，但必须一起处理）

框架只管"按地址取资源"，地址表属于工程配置：

- 全新工程：`Window → Asset Management → Addressables → Groups`，Unity 会自动生成 `Assets/AddressableAssetsData`
- Addressables 1.22 的 Profile 是**内联序列化**在 `Assets/AddressableAssetsData/AddressableAssetSettings.asset` 的 `m_Profiles` 段里的。工程内**不存在** `Profiles/` 目录属于正常状态，不要去找 `Default Profile.asset`，更不要因为缺它去重建配置
- 校验方式：`m_ActiveProfileId` 的值能在 `m_Profiles` 中找到同 `m_Id` 的条目（`m_ProfileName: Default`）即为正确
- 编辑器里不打包直接运行：`Window → Asset Management → Addressables → Mode → Use Asset Database (fastest)`
- 出 APK：Mode 切到 `Use Existing Build`，并先 `Build → New Build` 生成 Content
- `Assets/AddressableAssetsData` 必须进版本库，只提交 `Assets/Scripts` 的话队友拉下来一定加载失败

---

## 二、场景怎么搭

层级不是审美问题，是被 `GameScene` 和 `UIManager` 的代码写死的。以下每条都有代码依据。

### 2.1 目标层级

```text
Game                      [常驻根] 只挂 GameScene
├── Main Camera           Camera + AudioListener
├── Directional Light     Light（URP 工程会自动带 UniversalAdditionalLightData）
└── EventSystem           EventSystem + StandaloneInputModule

UIRoot                    [常驻根] Canvas + CanvasScaler + GraphicRaycaster，不挂脚本
├── HUD                   RectTransform 全屏拉伸（常驻信息层，可选）
├── Panel                 RectTransform 全屏拉伸（功能面板层，可选）
└── Popup                 RectTransform 全屏拉伸（弹窗结算层，可选）
```

- `Game` 与 `UIRoot` 是**兄弟根节点**，`GameScene.Awake` 里各调一次 `DontDestroyOnLoad`，一起常驻
- `EventSystem` 放在 `Game` 之下，跟着常驻；留在场景根节点上，切场景后 UI 会突然点不动
- **玩法物体不要挂在 `Game` 之下**：整棵 `Game` 子树都会跨场景常驻，切关卡时上一关的物体会全部残留

### 2.2 组件清单

| 节点 | 组件 | 必设参数 |
| --- | --- | --- |
| `Game` | `GameScene` | 场景里唯一，别再挂第二个 |
| `Main Camera` | `Camera`、`AudioListener` | 全工程**只允许一个** `AudioListener` |
| `EventSystem` | `EventSystem`、`StandaloneInputModule` | Action Asset 留空（没装 Input System） |
| `UIRoot` | `Canvas` | Render Mode = `Screen Space - Overlay` |
| `UIRoot` | `CanvasScaler` | `Scale With Screen Size`；横屏 1920x1080 / 竖屏 1080x1920；Match = 0.5 |
| `UIRoot` | `GraphicRaycaster` | `Blocking Objects = None`，`Block Resolution = 0` |
| `HUD`/`Panel`/`Popup` | 仅 `RectTransform` | Anchor 全屏拉伸，四边偏移 0 |

- 给物体加 `Canvas` 时 Unity 会自动补齐 `CanvasScaler` 与 `GraphicRaycaster`，缺了再手动加
- `UIRoot` 上**不要**加 `CanvasGroup`：它会统一控制透明度与射线拦截，干扰单个界面的显隐

### 2.3 硬性约束

| 约束 | 代码依据 | 违反后果 |
| --- | --- | --- |
| `UIRoot` 必须是**场景根节点**，名字逐字符一致 | `HierarchyFindTool.FindSceneRoot` 只遍历 `GetRootGameObjects()` | `GameApp` 完全不初始化，整个框架不启动 |
| `UIRoot` 上不放任何业务脚本 | `GameScene` 只把它当父节点用 | 挂的脚本生命周期不受框架管理 |
| 界面预制体根节点必须挂 `Canvas` | `UIManager.Open` 只在 `GetComponent<Canvas>() != null` 时写 `sortingOrder` | 界面层级压不住，弹窗被面板盖住 |
| 每个界面一个**嵌套 Canvas** | 同上 | 被迫给 `UIRoot` 挂 `CanvasGroup` 才生效 |
| 同一场景不要放两个 `GameScene` | `GameScene.Awake` 的 `S_instance` 判重会 `Destroy(gameObject)` | 后加载的那棵根物体被整个销毁 |
| 资源地址必须已在 Addressables 注册 | `ResManager` 全部走 `Addressables.*` | `Open<T>()` 返回 null，Console 报"资源加载失败" |

### 2.4 搭建步骤

1. 新建场景 `Assets/Scenes/Game.unity`
2. 建根空物体命名 `Game`，Add Component 搜 `GameScene`
3. 在 `Game` 下建 `Main Camera`、`Directional Light`、`EventSystem`（`Create → UI → Event System` 生成的节点记得拖进来）
4. Hierarchy 空白处右键 `Create Empty`，命名 `UIRoot`，**保持根节点层级**
5. `UIRoot` 加 `Canvas`（Overlay），确认 `CanvasScaler` 参考分辨率与 Match 已按目标屏幕取向设好
6. 按需建 `HUD` / `Panel` / `Popup` 三个子容器（不建也能跑，只是层级全挤在一起）
7. `File → Build Settings`：加入 `Game.unity` 并勾选，移除模板自带的 `SampleScene`
8. `Window → Asset Management → Addressables → Groups` 建一个 `UI` 组，登记界面预制体
9. 运行验证下面的清单

### 2.5 验收清单

- Console 没有 `查找场景根节点失败：场景 Game 中未找到 UIRoot`
- 运行后 `Game` 与 `UIRoot` 都出现在 `DontDestroyOnLoad` 场景下
- `GameApp.UIManager.UIRoot` 非空；`ControllerManager` 里存在 `ControllerType.Game`
- 点击 UI 按钮有响应；切场景后 HUD 不消失、也不重复出现
- 场景里**不要**手动创建 `ResPoolRoot` / `AddressablesPool` / `PrefabPool` / `SoundManager` 节点，这些由 `ResManager`、`SoundManager` 在运行时自建
---

## 三、启动链路

```text
GameScene.Awake
  ├─ HierarchyFindTool.FindSceneRoot("UIRoot")
  ├─ DontDestroyOnLoad(Game) + DontDestroyOnLoad(UIRoot)
  ├─ GameApp.Instance.Init()      → new TimeManager / ControllerManager / UIManager
  └─ GameApp.UIManager.SetRoot(uiRoot)

GameScene.Start
  └─ ControllerManager.Register(ControllerType.Game, new GameController())
        └─ GameController 构造函数里 Register<界面>，OnInit 里订阅事件

GameScene.Update
  └─ GameApp.Instance.Tick(Time.deltaTime)
        ├─ TimeManager.Tick
        └─ ControllerManager.Tick → BaseController.Tick

GameScene.OnDestroy
  └─ GameApp.Instance.Destroy()
        UIManager → ControllerManager → TimeManager → ResManager
```

要点：

- 帧驱动只有一个入口，就是 `GameScene.Update`。业务不要自己挂 `MonoBehaviour.Update`，把逻辑写在 `BaseController.Tick` 里
- `GameApp` 是纯 C# 单例（`Singleton<T>` + `Lazy`），`Instance` 随时可取，但 `GameApp.UIManager` 等静态属性在 `Init()` 之前是 null。`GameScene.Awake` 是所有初始化的起点，别比它更早访问
- 域重载（改脚本后重进 Play）会重置静态字段，所以 Play 模式下反复进出不需要手动清理

---

## 四、加一个界面（完整流程）

以新增 `UIMainHUD` 为例，六步。**不要**把界面脚本直接拖进场景，一律走"预制体 + 地址 + Register"。

### 4.1 定义地址常量

`Framework/QTower/Common/Defines/UIDefines.cs`：

```csharp
public static class UIDefines
{
    public const string UIMainHUD = "UIMainHUD";
}
```

同一界面要用到的事件名一并登记进 `Framework/QTower/Common/Defines/EventDefines.cs`，业务代码里不要散写字符串：

```csharp
public static class EventDefines
{
    public const string DepthChanged = "DepthChanged";
}
```

### 4.2 做预制体

- 路径：`Assets/Prefabs/UI/UIMainHUD.prefab`
- 根节点命名 = 地址 = 视图类名，`RectTransform` 全屏拉伸、四边偏移 0
- 根节点加一个**嵌套 `Canvas`**（只加 `Canvas`，不要再加 `CanvasScaler` / `GraphicRaycaster`，缩放与射线统一由 `UIRoot` 承担）
- 把视图脚本挂在根节点上，Inspector 里把 `SerializeField` 引用拖好
- 拖进 `Addressables Groups` 的 `UI` 组，确认 Address 与 `UIDefines` 常量完全一致

> 视图脚本最好显式挂在预制体上。`UIManager.Open` 在取不到组件时会 `AddComponent<TView>()` 兜底，但运行时补挂的组件拿不到任何 Inspector 配置，所有引用都是 null。

### 4.3 写 View

```csharp
/*
 * ┌──────────────────────────────────┐
 * │  描    述: 主 HUD 视图，显示深度与收获
 * │  类    名: UIMainHUD.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 */

using Framework.QTower.Common.Defines;
using Framework.QTower.View;
using TMPro;
using UnityEngine;
using Utils.Find;

namespace UI
{
    public sealed class UIMainHUD : UIBase
    {
        [Header("引用")]
        [Tooltip("当前深度文本")]
        [SerializeField] private TextMeshProUGUI Txt_Depth;

        // 引用优先在预制体 Inspector 上拖好，这里演示用查找兜底
        protected override void OnInit()
        {
            Txt_Depth = transform.FindChildComponent<TextMeshProUGUI>("Bg/Txt_Depth");

#if UNITY_EDITOR
            if (Txt_Depth == null) Debug.LogError($"[{nameof(UIMainHUD)}] Txt_Depth 未找到，请检查预制体层级");
#endif
        }

        protected override void OnOpen(params object[] args)
        {
            refreshDepth(args.Length > 0 ? (int)args[0] : 0);
        }

        protected override void OnClose(params object[] args)
        {
        }

        // 订阅全局事件，UIBase 在 OnDestroy 时统一反注册
        protected override void SubscribeViewEvents()
        {
            RegisterEvent<int>(EventDefines.DepthChanged, handleDepthChanged);
        }

        protected override void OnDispose()
        {
        }

        private void handleDepthChanged(int depth)
        {
            refreshDepth(depth);
        }

        private void refreshDepth(int depth)
        {
            Txt_Depth.text = depth.ToString();
        }
    }
}
```

### 4.4 写 Controller

```csharp
/*
 * ┌──────────────────────────────────┐
 * │  描    述: 主界面控制器，负责 HUD 的注册与开关
 * │  类    名: UIMainController.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 */

using Cysharp.Threading.Tasks;
using Framework.QTower;
using Framework.QTower.Common.Defines;
using Framework.QTower.Controller;
using Game;

namespace UI
{
    public sealed class UIMainController : BaseController
    {
        // UI 注册放在构造函数：此时 GameScene.Awake 已经 SetRoot 完，UIRoot 可用
        public UIMainController()
        {
            GameApp.UIManager.Register<UIMainHUD>(new UIData
            {
                Address = UIDefines.UIMainHUD,
                Parent = GameApp.UIManager.UIRoot,
                Controller = this,
                SortingOrder = 0
            });
        }

        protected override void OnInit()
        {
            showHud();
        }

        protected override void RegisterModuleEvent()
        {
            RegisterEvent<int>(EventDefines.PlayerDied, handlePlayerDied);
        }

        public override void Tick(float deltaTime)
        {
        }

        protected override void OnDestroy()
        {
            GameApp.UIManager.UnregisterByController(this);
        }

        private void showHud()
        {
            GameApp.UIManager.Open<UIMainHUD>().Forget();
        }

        // 结算界面需要深度参数，args 会透传到 OnOpen；UISummary 换成你自己的界面类
        private void handlePlayerDied(int depth)
        {
            GameApp.UIManager.Open<UISummary>(depth).Forget();
        }
    }
}
```

### 4.5 注册 Controller

```csharp
// GameScene.RegisterModules 或某个已有 Controller 的 OnInit 里
GameApp.ControllerManager.Register(ControllerType.None, new UIMainController());
```

`ControllerType.Game` 一个类型只允许注册一个控制器，重复注册会返回已存在的那个；无标识的普通控制器统一用 `ControllerType.None`（不进 `m_controllerMap`，但照样被 `Tick` 驱动、照样被逆序销毁）。

### 4.6 生命周期对应关系

| 框架调用 | View 回调 | Controller 回调 | 说明 |
| --- | --- | --- | --- |
| `Register<T>` | — | — | 只登记 `UIData`，不加载资源 |
| `Open<T>(args)` 首次 | `OnInit` → `SubscribeViewEvents` → `OnOpen` | `OnViewLoaded` → `OnViewOpened` | 异步加载 + 实例化 + 缓存 |
| `Open<T>(args)` 再次 | `OnOpen` | `OnViewOpened` | 命中缓存，仅 `SetActive(true)` |
| `Hide<T>()` | — | — | 只 `SetActive(false)` |
| `Close<T>(args)` | `OnClose` → 隐藏 | `OnViewClosed` | **不销毁实例**，只是关掉 |
| `UnregisterByController(c)` / `UIManager.Destroy()` | `OnDispose` | — | 走 `Addressables.ReleaseInstance` 真正释放 |
---

## 五、API 速查

### 5.1 框架层 `Framework/QTower`

| 类（命名空间） | 关键成员 | 用途 |
| --- | --- | --- |
| `EventManager`<br>(`Framework.QTower.Event`) | `RegisterEvent` / `UnregisterEvent` / `PublishEvent`，各有无参与 `<TEvent>` 两版 | 全局事件总线，按字符串名分发 |
| `UIManager`<br>(`Framework.QTower`) | `SetRoot` / `UIRoot` / `Register<TView>` / `Open<TView>` / `Hide<TView>` / `Close<TView>` / `UnregisterByController` / `Destroy` | 界面注册、加载、缓存、显隐、释放 |
| `UIData`<br>(`Framework.QTower`) | `Address` / `Parent` / `Controller` / `SortingOrder` | 注册一条 UI 所需的全部信息 |
| `BaseController`<br>(`Framework.QTower.Controller`) | `OnInit` / `Tick` / `OnDestroy` / `RegisterModuleEvent` / `OnViewLoaded` / `OnViewOpened` / `OnViewClosed` / `RegisterEvent` / `UnregisterEvent` | 业务控制器基类，事件自动反注册 |
| `ControllerManager`<br>(`Framework.QTower.Controller`) | `Register<T>(controllerType, controller)` / `Register<T>(controller)` / `Get<T>` / `Unregister` / `Tick` / `Destroy` | 按类型登记并统一驱动控制器 |
| `UIBase`<br>(`Framework.QTower.View`) | `OnInit` / `OnOpen` / `OnClose` / `SubscribeViewEvents` / `OnDispose` / `RegisterEvent` / `Controller` | 界面视图基类 |
| `UIItemBase`<br>(`Framework.QTower.View`) | `FindNode` / `FindComponent<T>` / `SetVisible` | 列表子项基类，带节点缓存 |
| `Singleton<T>`<br>(`Framework.QTower.Common`) | `Instance` / `Init` / `Tick` / `Destroy` / `IsInitialized` | 纯 C# 单例基类，三段生命周期 |
| `ResManager`<br>(`Common.Res`) | 见 5.2 | Addressables 封装 + 引用计数缓存 + 对象池 |
| `SoundManager`<br>(`Framework.QTower.Common.Sound`) | `PlayBGM(res)` / `PlayEffect(res)` / `PlayEffect(res, pos)` / `PlayEffect(clip, pos, volumeScale, pitch, spatial)` / `PlayLoop(clip, parent, localPos, ...)` / `Stop(audioSource)` / `TotalVolume` / `BgmVolume` / `VoiceVolume` / `EffectVolume` | BGM、一次性音效、循环音效 |

> `ResManager` 的命名空间是 `Common.Res`，与其余框架类的 `Framework.QTower.*` 不一致，属于历史遗留。新代码建议统一，改动时注意 `GameApp.cs` 的 `using`。

### 5.2 资源 `ResManager`

| 分类 | 方法 | 备注 |
| --- | --- | --- |
| 资源加载 | `LoadAsset<T>(path)` / `LoadAssetAsync<T>(path)` / `PreLoadAssets(List<string>)` | 内部带引用计数缓存 |
| 资源释放 | `ReleaseAsset(path)` / `ReleaseAllAssets()`（`Destroy` 时自动调用） | 计数归零才真正 `Addressables.Release` |
| 实例化 | `Instantiate(key, parent)` / `InstantiateAsync(key, parent)` | 同步版走 `WaitForCompletion`，只适合启动期 |
| 实例释放 | `UnLoadInstance(GameObject)` | 即 `Addressables.ReleaseInstance` |
| 按地址池 | `InstantiateFromPool(key, parent)` / `InstantiateFromPoolAsync(key, parent)` / `ReleaseToPool(key, obj, maxPoolSize = 20)` / `ClearPool(key)` / `ClearAllPools()` | 常见于子弹、特效 |
| 按 Prefab 池 | `InstantiateFromPool(prefab, parent)` / `ReleaseToPool(prefab, obj, maxPoolSize)` / `ClearPrefabPool(prefab)` / `ClearAllPrefabPools()` | 已有 Prefab 引用时用，省一次地址解析 |

> 实例化出来的物体名字会被改成地址（`go.name = key`）。做同名多实例时，靠 `name` 找物体不可靠，用返回的引用或 `Transform`。

### 5.3 模块层与工具层

| 类 | 关键成员 | 用途 |
| --- | --- | --- |
| `TimeManager`<br>(`Module.Timer`) | `Register(duration, callback, isLooping = false)` → `int id` / `Cancel(id)` / `ClearAll()` / `Count` | 延迟与循环计时，由 `GameApp.Tick` 驱动 |
| `DurationTimer`<br>(`Utils.Timer`) | `Start(duration)` / `Tick(dt)` / `Pause` / `Resume` / `Stop` / `Complete` / `Reset` / `NormalizedTime` / `RemainingTime` | 无事件的手动计时器，做进度插值用 |
| `HierarchyFindTool`<br>(`Utils.Find`) | `FindSceneRoot(name)`；`GameObject` / `Component` / `Transform` 三套 `FindChild`、`FindChildGo`、`FindChildComponent<T>` 扩展 | 层级查找，替代 `GameObject.Find` |
| `QLog`<br>(`Utils.log`) | `Info` / `Warning` / `Error` / `Throw` | 前三者 `[Conditional("UNITY_EDITOR")]`，Release 零开销且自动带 `[类名]` 前缀 |

---

## 六、资源与命名约定

| 类型 | 约定 | 强制度 |
| --- | --- | --- |
| UI 预制体 | `Assets/Prefabs/UI/<视图类名>.prefab`，地址 = 类名 = `UIDefines` 常量 | **必须一致**，否则 `Open<T>` 加载失败 |
| 普通预制体 | `Assets/Prefabs/<模块>/<名字>`，地址走各自 `Defines` 常量 | 建议 |
| 音频 | `Assets/Audio/<BGM 或 Effect>/<名字>`，按地址播放 | 建议 |
| 场景 | `Assets/Scenes/Game.unity` 等 PascalCase | 建议 |
| 常量类 | `EventDefines` 放跨模块事件名，`UIDefines` 放 UI 地址 | **不要**再往业务代码里散写字符串 |
| `SortingOrder` | 0–99 常驻 HUD，100–999 功能面板，1000–1999 模态弹窗与结算，9000+ 全局 Loading | 团队内部保持一致即可 |

---

## 七、常见坑

| 症状 | 原因 | 修法 |
| --- | --- | --- |
| 运行后 Console 报 `GameApp 未初始化` | `UIRoot` 不是场景根节点，或名字不一致（大小写、空格） | 把 `UIRoot` 提到根节点，名字逐字符对齐 |
| UI 全部点不动，但显示正常 | 缺 `EventSystem`；或 `EventSystem` 留在场景根节点上、切场景被销毁；或装了 Input System 却还挂 `StandaloneInputModule` | 把 `EventSystem` 放到常驻的 `Game` 下；Input System 工程换 `InputSystemUIInputModule` |
| 弹窗被面板盖住 | 预制体根节点没挂 `Canvas`，`UIManager` 没机会写 `sortingOrder` | 每个界面预制体根加嵌套 `Canvas` |
| 引用配置丢了 | 视图脚本没挂在预制体上，运行时被 `AddComponent` 兜底 | 脚本显式挂在预制体根，Inspector 拖好引用 |
| 切关卡后上一关物体还在 | 玩法物体挂在了 `Game` 下，跟着 `DontDestroyOnLoad` 常驻 | 玩法物体放独立根节点 |
| `NullReferenceException: GameApp.UIManager` | 在 `GameScene.Awake` 之前访问了框架 | 业务初始化一律从 `Controller.OnInit` 开始 |
| `Open<T>()` 返回 null，日志"资源加载失败" | 地址未在 Addressables 注册；或 PlayMode Script 选了 `Use Existing Build` 但没打包 | Groups 里补地址；编辑器调试切 `Use Asset Database (fastest)` |
| AA 报 profile 相关错误 | `AddressableAssetSettings.asset` 里 `m_ActiveProfileId` 在 `m_Profiles` 中找不到对应 `m_Id`，多为跨工程合并冲突 | 把 `m_ActiveProfileId` 改回 `m_Profiles` 中 `Default` 那条的 `m_Id`；**不是**缺 `Profiles/` 目录 |
| 编译报 `CS0246: UIManager` | `QTower.asmdef` 的 `autoReferenced` 被改成 false，或业务层自己建了 asmdef 又没引用 `QTower` | 恢复 `autoReferenced`；给业务 asmdef 补 `QTower`、`Utils` 引用 |
| 迁移后编译报找不到 `Addressables` / `UniTask` | `QTower.asmdef` 用的是 GUID 引用，跨工程失配 | 改成按名字引用，见 1.3 |
| 退出 Play 后报错、或音效还在响 | `Singleton<T>` 静态实例不随场景销毁 | 保证 `GameScene.OnDestroy` 正常触发；不要手动 `Destroy` `SoundManager` 节点 |
| 计时器不生效 | `GameApp.TimeManager.Tick` 只由 `GameScene.Update` 驱动，`GameScene` 被禁用就没帧了 | 别禁用 `Game` 根节点；`Time.timeScale = 0` 时 `deltaTime` 也为 0 |

---

## 八、代码规范

完整规范见仓库根目录 `AGENTS.md`，这里只列框架内必须遵守的：

- 每个 `.cs` 顶部必须有文件头注释块（描述 / 类名 / 创建 By qiqizizzz）
- 纯私有字段 `_camelCase`，`[SerializeField]` 用 `PascalCase`，方法 `PascalCase`（公共）/ `camelCase`（私有）
- 所有日志包在 `#if UNITY_EDITOR` 里，格式 `[类名] 描述`；框架内部统一走 `QLog`
- 必要引用缺失要在编辑器下 `Debug.LogError` 主动暴露，**禁止**用判空静默跳过
- 一个文件一个类，文件名与类名一致
- 帧逻辑写在 `BaseController.Tick`，不要在 View 上挂 `Update`

---

## 九、新项目导入清单

```text
[ ] 1. 拷贝 Assets/Scripts 到工程
[ ] 2. Packages/manifest.json 补 UniTask + Addressables + uGUI + TMP（见 1.1）
[ ] 3. QTower.asmdef 引用改成按名字，清掉失效 GUID（见 1.3）
[ ] 4. Window → Asset Management → Addressables → Groups 能正常打开，Profile 下拉里有 Default
[ ] 5. Addressables Mode 设为 Use Asset Database (fastest)
[ ] 6. 建 Assets/Scenes/Game.unity，按 2.2 摆 Game + UIRoot 两棵根
[ ] 7. Game 下补 Main Camera / Directional Light / EventSystem
[ ] 8. Build Settings 勾选 Game.unity，移除 SampleScene
[ ] 9. 按项目取向设好 CanvasScaler 参考分辨率（竖屏就改成 1080x1920）
[ ] 10. GameController 里注册第一个界面的 Register，跑通 Open → OnOpen
```

---

## 十、目录结构

```text
Assets/Scripts
├── Framework                                  框架层：与玩法无关的通用基础设施
│   └── QTower                                 框架主命名空间 Framework.QTower
│       ├── QTower.asmdef                      独立程序集，引用 Utils / Addressables / UniTask
│       ├── EventManager.cs                    全局事件总线：RegisterEvent / UnregisterEvent / PublishEvent
│       ├── Common                             基础设施
│       │   ├── Singleton.cs                   纯 C# 单例基类，Init / Tick / Destroy 三段生命周期
│       │   ├── Defines
│       │   │   ├── EventDefines.cs            跨模块事件名常量
│       │   │   └── UIDefines.cs               UI 的 Addressables 地址常量
│       │   ├── Res
│       │   │   └── ResManager.cs              Addressables 加载卸载、实例管理、对象池
│       │   └── Sound
│       │       └── SoundManager.cs            BGM、一次性音效、循环音效播放
│       ├── MVC                                框架核心：控制器 + 视图
│       │   ├── UIManager.cs                   UI 注册、加载、缓存、显示、隐藏、关闭
│       │   ├── Type/ControllerType.cs         全局控制器类型标识（None / Game）
│       │   ├── Data/UIData.cs                 UI 注册数据：Address / Parent / Controller / SortingOrder
│       │   ├── Controller                     BaseController（生命周期 + 事件托管 + OnView* 回调）、ControllerManager（按类型注册、统一 Tick、逆序销毁）
│       │   └── View                           UIBase（Init / Open / Hide / Close）、UIItemBase（节点缓存与显隐）
│       └── Editor                             编辑器层：仅编辑器编译，不进包体
│           ├── QTower.Editor.asmdef           includePlatforms Editor，仅引用 QTower
│           ├── Controller/BaseEditorController.cs   编辑器控制器基类，挂 EditorApplication.update / Selection / Undo / SceneView 回调
│           └── View/UIBaseEditor.cs                  编辑器 UI 视图基类，统一生命周期与事件订阅
├── Module                                     模块层：可复用玩法系统，不依赖具体界面
│   └── Timer                                  TimeManager（门面）+ GameTimer（任务集合）+ GameTimerData（单任务）
├── Game                                       业务层：组合根与场景入口
│   ├── GameScene.cs                           MonoBehaviour 入口，找 UIRoot、DontDestroyOnLoad、驱动 Tick
│   ├── GameApp.cs                             组合根 Singleton，装配 Time / Controller / UI 三大管理器
│   └── GameController.cs                      场景控制器，注册默认界面、订阅事件、销毁时反注册
├── UI                                         业务界面层（当前为空，待玩法填充）
└── Utils                                      工具层：无状态扩展与轻量工具，零依赖
    ├── Utils.asmdef                           独立程序集，references 为空
    ├── log/QLog.cs                            日志封装，Release 构建自动抹除
    ├── Find/HierarchyFindTool.cs              场景根节点与子节点查找扩展
    └── Timer/DurationTimer.cs                 无事件轻量计时器，负责局部时间进度计算
```
