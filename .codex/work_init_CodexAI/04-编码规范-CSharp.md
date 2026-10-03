# Unity C# 开发规范

## 一、注释规范（必读）

# 文件头注释（强制要求）

- **每个 `.cs` 文件的最顶部**必须添加文件头注释块
- `描述` 填写该类的用途与职责简介
- `类名` 填写与文件名一致的类名（含 `.cs` 后缀）
- `创建` 固定署名 `By qiqizizzz`
- 格式严格保持如下，不得随意更改边框样式

```csharp
/*
* ┌──────────────────────────────────┐
* │  描    述: 玩家控制器，负责处理移动、跳跃与受击逻辑
* │  类    名: PlayerController.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
*/
```

### 方法与类注释（强制要求）

- **每个方法义上方**戳情添加 `//` 单行注释，主要简要描述该类方法的职责与用途，但是如果这个类名非常简洁明了，或者类方法本身很简单，那么则不需要添加，比如`OnInit`，`UnInit`，`OnOpen`，`OnClose`等
- 注释结尾不要加句号
- **除了 Unity 生命周期外，自定义的每个方法定义上方**必须添加注释，方式二选一：
    - 方式1：`//` 单行注释 —— 用于简单方法
    - 方式2：XML 文档注释 `<summary>` —— 用于公共 API 或需要参数说明的方法
- **不要同时写** `//` 和 XML，二选一即可
- 复杂逻辑块内部使用行内注释说明意图，避免写显而易见的注释

### 注释精简原则

- 注释用于解释职责、意图和不易从代码看出的原因，不重复描述代码本身
- 不要为属性、字段、构造函数、简单转换方法或简单赋值添加显而易见的注释
- 不要在每个类中机械添加“属性”“构造函数”“Public Function”等成员分区注释
- 成员分组只有在类规模较大、确实能改善阅读时才使用，优先保持代码紧凑
- 需要标记成员分组时使用简短注释，例如 `// 运动状态`，不要使用 `// ==================== 运动状态 ====================` 形式的装饰性分隔线
- 公共 API 只有在用途、参数约束或使用时机不明显时才添加 XML 注释
- 复杂算法、关键生命周期边界和重要设计取舍应保留简短注释
- 文件头注释仍然保留，已有注释不因本规则进行无关的批量清理

```csharp
/*
* ┌──────────────────────────────────┐
* │  描    述: 玩家控制器，负责处理移动、跳跃与受击逻辑
* │  类    名: PlayerController.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
*/

// 玩家控制器，负责处理移动、跳跃和受击逻辑
public class PlayerController : MonoBehaviour
{
    private void Awake() { }
    private void Update() { }

    /// <summary>
    /// 对玩家造成伤害
    /// </summary>
    /// <param name="damage">伤害数值</param>
    /// <param name="damageType">伤害类型</param>
    public void TakeDamage(float damage, DamageType damageType)
    {
        if (m_isInvincible) return;
        float finalDamage = CalculateFinalDamage(damage, damageType);
        m_currentHealth -= finalDamage;
        OnHealthChanged?.Invoke(m_currentHealth);
    }
}
```

### 代码变更说明（强制要求）

每次修改代码后，必须在回复末尾附上变更摘要，格式如下：

```
📝 代码变更摘要
├─ 修改类：PlayerController
│   ├─ 新增方法：TakeDamage() —— 处理受击逻辑
│   └─ 修改方法：Update() —— 新增跳跃输入检测
└─ 新增类：BulletPool
    └─ 新增方法：GetBullet()、ReturnBullet() —— 对象池核心逻辑
```

---

## 二、命名规范

### 类与接口

| 类型             | 规则                     | 示例                              |
| ---------------- | ------------------------ | --------------------------------- |
| 普通类           | PascalCase               | `PlayerController`, `GameManager` |
| 接口             | `I` + PascalCase         | `IDamageable`, `IInteractable`    |
| 抽象类           | PascalCase + `Base` 后缀 | `CharacterBase`, `PlayerStateBase` |
| ScriptableObject | PascalCase + `SO` 后缀   | `ItemDataSO`, `GameConfigSO`      |
| 枚举             | PascalCase               | `DamageType`, `GameState`         |

抽象类的 `Base` 固定放在类型语义之后，例如 `PlayerStateBase`、`UIBase`。ScriptableObject 只要求以 `SO` 结尾；作为配置资产时优先使用 `ConfigSO`，例如 `PlayerStateConfigSO`，不因为抽象或继承关系额外强制改成 `BaseSO`。既有命名不在此规则落地时批量重命名；新建类型和后续触及的同族类型必须遵守。

### 架构职责后缀

项目类型名统一采用：

```text
{主体}{能力}{职责后缀}
```

例如：

```text
Player + Input + Reader = PlayerInputReader
Player + Move + State = PlayerMoveState
Combat + Hitbox = CombatHitbox
```

`主体` 是 Player、Enemy、Combat、Game 等领域；`能力` 优先使用完整英文单词，不在类型名中随意新增未经确认的缩写。项目已经确认并成族使用的缩写可以沿用，例如 `Anim` 可用于 Animator / Animation 相关类型，但同一族命名必须保持一致，不要在同一职责里混用 `Anim` 与 `Animation`。

只有下表中的职责后缀可以用于新增架构类型。选择后缀前，先按真实职责归类；不要因为“看起来能工作”而引入新的同义后缀。

| 后缀 | 唯一职责 | 示例 |
| --- | --- | --- |
| `Controller` | 组合、初始化、调度一个完整模块或子模块 | `PlayerController`、`PlayerViewController`、`PlayerAnimController` |
| `System` | QF 管理的全局或跨场景服务 | `TimerSystem` |
| `Machine` | 长生命周期的状态执行引擎 | `PlayerStateMachine` |
| `State` | HFSM 中的一个状态节点 | `PlayerMoveState` |
| `Context` | 单个角色跨子系统共享的运行时事实 | `PlayerContext` |
| `Reader` | 读取外部输入并写入当前事实 | `PlayerInputReader` |
| `Interpreter` | 将一种输入解释为语义行为 | `PlayerSprintInterpreter` |
| `Buffer` | 保存可延迟消费的时序数据 | `PlayerInputBuffer` |
| `Resolver` | 根据输入计算结果，不直接操作外部对象 | `PlayerMoveDirectionResolver` |
| `Writer` | 向 Animator、UI 等外部目标写入结果 | `PlayerAnimationWriter` |
| `Motor` | 角色最终物理移动或旋转执行 | `PlayerMotor` |
| `Receiver` | Unity 或外部系统的回调接收点 | `PlayerRootMotionReceiver` |
| `Selector` | 从多个候选项中选择一个结果 | `PlayerTransitionSelector` |
| `Rule` | 一条声明式条件或映射 | `PlayerTransitionRule` |
| `Rules` | 静态的同类规则定义集合 | `PlayerMoveTransitionRules` |
| `Data` | 以数据承载为主的运行时或配置值对象 | `DamageData`、`GameTimerData` |
| `ConfigSO` | Inspector 中编辑的设计数据资产 | `PlayerMoveConfigSO` |

补充约束：

- `Data` 可用于以数据承载为主的类型；是否包含轻量运行时状态由模块语义决定，不因存在计时字段或回调字段自动判定为错名
- `Rules` 只用于静态规则定义集合；单条规则使用 `Rule`；需要装配同族规则时优先放到对应子模块 `Controller` 中，新增代码不再使用含义宽泛的 `Binder`
- `Type` 表示语义分类，`Id` 表示稳定标识，`Mode` 表示互斥运行模式，`Priority` 表示排序等级，不能互相替代
- `App` 只用于游戏应用入口，`Architecture` 只用于 QF 架构入口，`Dummy` 只用于测试对象
- 领域实体或 Unity 组件本身不强加职责后缀，例如 `CombatHitbox`，不要新增 `CombatHitboxRunner`、`CombatHitboxManager` 或 `CombatHitboxExecutor`
- 新增类型禁止使用 `Manager`、`Handler`、`Helper`、`Processor`、`Runner`、`Executor`、`Runtime`、`Profile` 等泛后缀；确有新职责无法归类时，先确认命名规则再创建

### 方法与属性

| 类型     | 规则                                  | 示例                                      |
| -------- | ------------------------------------- | ----------------------------------------- |
| 公共方法 | PascalCase                            | `TakeDamage()`, `MoveToPosition()`        |
| 私有方法 | PascalCase                            | `CalculateDamage()`, `UpdateUI()`         |
| 保护方法 | PascalCase                            | `OnInit()`, `BuildStateTree()`            |
| 属性     | PascalCase                            | `public int Health { get; private set; }` |
| 事件     | `On` + PascalCase                     | `OnDamageTaken`, `OnItemCollected`        |
| 协程     | PascalCase + `Coroutine` 后缀（可选） | `SpawnEnemyCoroutine()`                   |

项目自有方法无论使用 `public`、`private` 还是 `protected`，名称都统一使用 PascalCase。Unity 生命周期函数继续使用 Unity 官方定义的 PascalCase 名称。`Assets/Plugins/` 中的第三方源码和工具生成的 `.cs` 文件不做批量改名，避免破坏插件升级或被重新生成覆盖。

### 字段命名（个人规范）

| 类型                       | 规则                                | 示例                            |
| -------------------------- | ----------------------------------- | ------------------------------- |
| **纯私有字段**             | `m_` + camelCase                    | `m_animator`, `m_currentHealth` |
| **SerializeField private** | PascalCase                          | `MoveSpeed`, `JumpForce`        |
| **protected 字段**         | camelCase                           | `stats`, `config`, `animator`   |
| 常量                       | UPPER_SNAKE_CASE                    | `MAX_HEALTH`, `DEFAULT_SPEED`   |
| 静态字段                   | `m_` + camelCase                    | `m_events`, `m_clipDic`         |
| 公共字段                   | 尽量避免，改用属性或 SerializeField | —                               |

> 私有字段一律 `m_` + camelCase，`static`、`readonly`、`static readonly` 也不例外。早期的 `S_` + PascalCase 写法已废弃：工程内 `Singleton.cs` 的 `m_instance`、`EventManager.cs` 的 `m_events`、`SoundManager.cs` 的 `m_clipDic` 已全部统一。

```csharp
// ✅ 纯私有字段 —— m_ 前缀
private Rigidbody m_rigidbody;
private bool m_isGrounded;
private float m_currentHealth;

// ✅ SerializeField private
[SerializeField] private float MoveSpeed = 5f;
[SerializeField] private LayerMask GroundLayer;

// ❌ 错误：SerializeField 用了 m_ 前缀
[SerializeField] private float m_MoveSpeed;

// ❌ 错误：纯私有字段没有 m_ 前缀
private Rigidbody rigidbody;
```

### Unity 特定命名

- **Layer 变量**：`m_enemyLayer`（纯私有）
- **Tag 字符串常量**：`const string PLAYER_TAG = "Player";`
- **Scene 名称**：PascalCase，如 `MainMenu`, `Level01`
- **Animator 参数**：camelCase，如 `isWalking`, `attackTrigger`

---

## 三、代码组织

### 类内成员顺序

```csharp
public class PlayerController : MonoBehaviour
{
    // ==================== 常量与静态字段 ====================
    private const float MAX_HEALTH = 100f;
    private static readonly WaitForSeconds m_respawnDelay = new WaitForSeconds(3f);

    // ==================== 字段[外部设置] ====================
    [Header("移动设置")]
    [Tooltip("角色移动速度（单位/秒）")]
    [SerializeField, Range(0f, 20f)] private float MoveSpeed = 5f;

    // ==================== 属性 ====================
    public float Health { get; private set; }
    public bool IsGrounded { get; private set; }

    // ==================== 字段[私有] ====================
    private Rigidbody m_rigidbody;
    private Animator m_animator;

    // ==================== 事件 ====================
    public event Action<float> OnHealthChanged;
    public event Action OnPlayerDeath;

    // ==================== 生命周期 ====================
    private void Awake() { }
    private void OnEnable() { }
    private void Start() { }
    private void FixedUpdate() { }
    private void Update() { }
    private void LateUpdate() { }
    private void OnDisable() { }
    private void OnDestroy() { }

    // ==================== Public Function ====================
    public void TakeDamage(float damage) { }

    // ==================== Private Function ====================
    private void HandleMovement() { }

    // ==================== Coroutine ====================
    private IEnumerator RespawnCoroutine() { yield return null; }

    // ==================== Gizmos ====================
    private void OnDrawGizmosSelected() { }
}
```

### 命名空间

- 格式：`公司名.项目名.模块名`，如 `MyStudio.RPGGame.Player`
- 按功能模块划分：`Core`, `Player`, `Enemy`, `UI`, `Audio`, `Utils`
- 每个文件只定义一个类，文件名与类名保持一致

---

## 四、Unity API 使用规范

### SerializeField 最佳实践

```csharp
[Header("战斗属性")]
[Tooltip("最大生命值")]
[SerializeField, Range(1f, 500f)] private float MaxHealth = 100f;

[Tooltip("攻击冷却时间（秒）")]
[SerializeField, Min(0f)] private float AttackCooldown = 0.5f;
```

### 组件获取

- **Awake()** 中获取并缓存所有组件引用到 `m_` 字段
- 优先使用 `TryGetComponent` 避免空引用异常
- 禁止在 `Update()` 等高频方法中调用 `GetComponent`

```csharp
private void Awake()
{
    m_rigidbody = GetComponent<Rigidbody>();
    if (!TryGetComponent<Animator>(out m_animator))
    {
        QLog.Error($"未找到 Animator：{gameObject.name}");
        return;
    }
}
```

### 空检查

```csharp
if (m_target == null) return;       // ✅ 正确
if (m_target is null) return;       // ❌ 错误，无法检测已销毁对象
```

### 事件订阅与取消

```csharp
private void OnEnable()
{
    GameManager.Instance.OnGameOver += HandleGameOver;
}

private void OnDisable()
{
    if (GameManager.Instance != null)
        GameManager.Instance.OnGameOver -= HandleGameOver;
}
```

---

## 五、性能优化规范

### 缓存原则

```csharp
private Transform m_transform;

private void Awake() { m_transform = transform; }

private void Update()
{
    m_transform.position += m_inputDirection * (MoveSpeed * Time.deltaTime);
}
```

### 物理操作

```csharp
// 所有物理操作必须放在 FixedUpdate 中执行
private void FixedUpdate()
{
    m_rigidbody.MovePosition(m_rigidbody.position + m_velocity * Time.fixedDeltaTime);
}
```

### 对象池

```csharp
private void Fire()
{
    GameObject bullet = BulletPool.Instance.GetBullet();
    bullet.transform.SetPositionAndRotation(m_firePoint.position, m_firePoint.rotation);
    bullet.SetActive(true);
}
```

### 字符串与 Tag 比较

```csharp
if (other.CompareTag("Enemy")) { }   // ✅ 正确
if (other.tag == "Enemy") { }        // ❌ 错误，产生 GC Alloc

private const string ENEMY_TAG = "Enemy";
if (other.CompareTag(ENEMY_TAG)) { }
```

### 协程优化

```csharp
private static readonly WaitForSeconds m_waitOneSecond = new WaitForSeconds(1f);

private IEnumerator CountdownCoroutine(int seconds)
{
    for (int i = seconds; i > 0; i--)
    {
        yield return m_waitOneSecond;
        OnCountdownTick?.Invoke(i);
    }
}
```

---

## 六、代码风格

### 括号与格式

```csharp
if (isReady)   // ✅ 正确，对于括号内只有一行代码,优先省略括号
    StartGame();

if (isReady)   // ✅ 正确，括号内多行使用大括号
{
    StartGame();
    StartAudio();
}
```

### 访问修饰符

- 普通类、结构体等类型中的成员**必须**显式声明访问修饰符
- 接口成员默认即为 `public`，不要求重复显式声明访问修饰符
- 能用 `private` 就不用 `protected`，能用 `protected` 就不用 `public`

```csharp
[SerializeField] private float MoveSpeed;  // ✅ 正确
float MoveSpeed;                            // ❌ 缺少访问修饰符

public interface IDamageable
{
    void TakeDamage(float damage);          // ✅ 正确，接口成员无需重复声明 public
}
```

### 其他风格约定

- 使用 `var` 仅当类型从右侧赋值可明显推断时
- 三元运算符只用于简单赋值，不嵌套使用
- 每个文件末尾保留一个空行

---

## 七、调试与日志

### 日志规范

- 项目日志统一使用 `QLog.Info` / `QLog.Warning` / `QLog.Error`
- 使用日志的文件必须引用 `Utils.log`
- 禁止在业务代码中直接使用 `Debug.Log` / `Debug.LogWarning` / `Debug.LogError`
- `QLog` 已通过 `[Conditional("UNITY_EDITOR")]` 保证 Release 构建自动移除日志调用，调用处不再额外包裹 `#if UNITY_EDITOR`
- `QLog` 会自动补充调用类名前缀，消息中不要手写 `[类名]`

```csharp
using Utils.log;

QLog.Info("玩家已死亡");
QLog.Warning($"血量低于阈值：{m_currentHealth}");
QLog.Error($"未找到 Animator：{gameObject.name}");
```

### 个人项目错误处理策略

本项目是个人项目。配置错误、非法参数和状态异常优先在 Unity Console 中清晰暴露，不为常规校验主动终止整个运行流程。

- 常规校验失败统一使用 `QLog.Error`，不使用 `throw`、`throw new` 或 `QLog.Throw`
- 记录错误后必须显式选择安全流程，例如 `return`、返回 `false`、跳过无效项或将对象标记为不可用
- 不能只记录日志后继续执行必然产生空引用或脏状态的代码
- Release 构建会移除日志调用，因此正确性不能依赖 `QLog.Error` 本身，日志后的控制流保护必须始终保留
- 除非用户明确要求或第三方 API 自身抛出异常，项目代码不主动引入异常作为普通分支控制

```csharp
if (targetId == PlayerStateId.None)
{
    QLog.Error("创建状态转换规则失败：目标状态不能是 PlayerStateId.None");
    return;
}
```

### 必要引用校验（Awake 中强制检查）

对于**必须存在**的引用（SerializeField 拖拽、Find 查找、资源加载），  
**不要用判空静默跳过**，要使用 `QLog.Error` 主动报错，并阻止依赖该引用的初始化继续执行：

```csharp
private void Awake()
{
    Txt_Title  = transform.Find("Bg/Txt_Title")?.GetComponent<TextMeshProUGUI>();
    Btn_Battle = transform.Find("Bg/Btn_Battle")?.GetComponent<Button>();
    Btn_Shop   = transform.Find("Bg/Btn_Shop")?.GetComponent<Button>();

    if (Txt_Title == null || Btn_Battle == null || Btn_Shop == null)
    {
        QLog.Error("必要 UI 引用缺失，请检查 Bg 下的层级路径");
        return;
    }

    bindEvents();
}
```

> **关键区别**：`QLog.Error` 负责让错误在 Editor 中可见，随后的显式控制流负责保证所有构建都不会继续进入无效逻辑。

### Gizmos 调试绘制

```csharp
private void OnDrawGizmosSelected()
{
    Gizmos.color = Color.yellow;
    Gizmos.DrawWireSphere(transform.position, AttackRange);

    Gizmos.color = Color.red;
    Gizmos.DrawRay(transform.position, transform.forward * ViewDistance);
}
```

---

## 八、判空规范

### 核心原则

> **该记录错误的地方必须使用 `QLog.Error` 明确记录，并通过显式控制流保护后续逻辑。**

判空的目的是**保护运行时逻辑**，而不是**隐藏开发期错误**。  
错误的判空会让 Bug 静默消失，导致调试时无从下手；只写日志却继续执行也会制造后续连锁错误。

### ❌ 禁止：用判空掩盖「应当存在的引用」

#### 1. 组件引用初始化

```csharp
// ❌ 错误：判空后 Find，掩盖了「忘记拖拽 / 路径写错」的问题
if (Txt_Title == null)
    Txt_Title = transform.Find("Bg/Txt_Title")?.GetComponent<TextMeshProUGUI>();

// ✅ 正确：直接赋值，找不到时记录错误并安全退出
Txt_Title = transform.Find("Bg/Txt_Title")?.GetComponent<TextMeshProUGUI>();
if (Txt_Title == null)
{
    QLog.Error("Txt_Title 未找到");
    return;
}
```

#### 2. 事件绑定

```csharp
// ❌ 错误：按钮没拖拽也不报错，功能静默失效
if (Btn_Battle != null)
    Btn_Battle.onClick.AddListener(onBattleClick);

// ✅ 正确：明确记录配置错误并阻止绑定继续执行
if (Btn_Battle == null)
{
    QLog.Error("Btn_Battle 未配置");
    return;
}

Btn_Battle.onClick.AddListener(onBattleClick);
```

#### 3. 资源加载

```csharp
// ❌ 错误：加载失败直接 return，调用方完全不知道发生了什么
if (ResKit.LoadGameObj("Prefabs/UI/UI_AllCardPanel") == null)
    return;

// ✅ 正确：加载后校验，失败时明确记录并退出
var prefab = ResKit.LoadGameObj("Prefabs/UI/UI_AllCardPanel");
if (prefab == null)
{
    QLog.Error("资源加载失败：Prefabs/UI/UI_AllCardPanel");
    return;
}
```

### ✅ 允许：真正需要判空的场景

| 场景               | 说明                     | 示例                                |
| ------------------ | ------------------------ | ----------------------------------- |
| **可选引用**       | 该字段本身就是可有可无的 | 可选特效、可选音效组件              |
| **运行时动态对象** | 对象可能已被销毁         | `if (m_target == null) return;`     |
| **外部传入参数**   | 调用方可能传 null        | 公共 API 的参数校验                 |
| **单例访问**       | 场景切换时单例可能不存在 | `if (GameManager.Instance != null)` |

```csharp
// ✅ 可选特效，允许判空
if (m_hitEffect != null)
    m_hitEffect.Play();

// ✅ 运行时目标可能已销毁
if (m_target == null) return;

// ✅ 事件取消订阅，单例可能已销毁
private void OnDisable()
{
    if (GameManager.Instance != null)
        GameManager.Instance.OnGameOver -= HandleGameOver;
}
```

---

## 九、常见错误速查

| ❌ 错误做法                         | ✅ 正确做法                                        |
| ---------------------------------- | ------------------------------------------------- |
| 构造函数中使用 Unity API           | 改用 `Awake()` 或 `Start()`                       |
| `OnDestroy` 中访问其他 GameObject  | 销毁顺序不确定，应提前解引用                      |
| `Update` 中调用 `FindObjectOfType` | 在 `Awake/Start` 中缓存引用                       |
| `is null` 检查 Unity 对象          | 使用 `== null`                                    |
| 协程中 `new WaitForSeconds()`      | 提前缓存为静态只读字段                            |
| `other.tag == "xxx"` 比较 Tag      | 使用 `other.CompareTag("xxx")`                    |
| 公共字段暴露给外部                 | 使用 `[SerializeField] private` + 属性            |
| 普通类型成员缺少访问修饰符         | 显式声明访问修饰符；接口成员可省略 `public`       |
| 缺少文件头注释块                   | 每个 `.cs` 文件顶部必须添加标准文件头             |
| SerializeField 字段用 `m_` 前缀    | SerializeField 统一使用 PascalCase                |
| 纯私有字段无前缀                   | 纯私有字段统一使用 `m_` 前缀                      |
| 必要引用判空后静默跳过             | 使用 `QLog.Error` 记录并显式安全退出               |
| 使用 `throw` / `QLog.Throw` 做常规校验 | 使用 `QLog.Error` 并返回、跳过或标记无效           |
| 直接调用 `Debug.Log*`              | 统一调用 `QLog.Info/Warning/Error`                 |
| 手动包裹 `#if UNITY_EDITOR` 日志    | 由 `QLog` 的 `Conditional` 自动处理                |
