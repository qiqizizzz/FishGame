# FishGame 音效与 BGM 全量接入

> 本记录接续：2026-10-11/10-河豚定幅巡守与挂账口径纠正.md　　日期：2026-10-11
> 覆盖三轮改动：① 素材全量接入（基线 HEAD `dd0faab`）② 水底 BGM 只允许在水下响 + 商店「开始下潜 / 关闭」点击音 ③ 商店「刷新 / 一键合成」点击音

## 一、记录范围

用户自己把素材下载进 `Assets/Audio`，本轮只做一件事：**把这批音效与 BGM 接进游戏**（找网站的环节已结束）。
挂账的东西一律没碰：**任何数值配置（`.asset` / SO 参数）**、合成模块功能本身。
改过的非代码资源只有三类：Addressables 的 `SFX.asset` 条目表、四个音频的 `.meta`（`loadType` 与改名后的文件名）、以及两个 Water 素材的**改名**。

## 二、用户诉求原话

> 「全部准备好了，你帮我一键替换吧：D:\\CCodes\\unity\\FishGame\\Assets\\Audio，我有一个通用的组件是 UIAudio，不过 bgm 不能用这个，你看情况用一下吧，开始吧」
> 「Assets/Audio/Water，钓鱼的你没用啊，这是钓鱼的BGM，一个是刚潜入水下的音效，一个是在水底中的BGM_Loop」
> 「还有就是fish那个bgm只允许在水下的时候播放啊（入水音效没问题），在水面上面的时候就别播放主界面那个bgm吧，不过主界面那个bgm需要降低一下音量，现在声音太大了」
> 「这俩按钮也需要接入音效」（指 `Btn_Refresh` 与 `Btn_CraftAll`）

第一轮我漏接了 `Assets/Audio/Water` 两枚，把 `loop_underwater` 当成「待决策的环境音」挂起，实际用户已经明确它们是钓鱼用的。第二轮补齐。
同一轮里我还把「出水也不切回、全程硬切」当成定论写进过本文件，**那是被用户推翻的旧设计**，现在 §五 是最终口径。

早前已定口径（沿用）：

> 「音效的话晚点再找。先修复数值吧」→ 数值前几轮完成并经实测通过
> 「勾到鱼还需要一个音效，就简单的音效就行」→ 用户下载了 `SFX_catchfish.ogg`

## 三、素材清单与用途分配

| 文件 | 大小 | GUID | 用途 |
| --- | --- | --- | --- |
| `Audio/BGM/BGM_mainMenu.ogg` | 1964.7KB | `08063d9f6bdd57b4ba37a6389d665c8c` | 仅主菜单（音量压到 0.6） |
| `Audio/BGM/BGM_shop.mp3` | 4492.9KB | `963b43e7ead9c8144a30bbca28222137` | 商店 BGM |
| `Audio/Water/BGM_underwaterLoop.mp3` | 2561.2KB | `70dc359bdb67c9c44967d402b8c4aa31` | 水底 BGM 循环（原名 `loop_underwater`） |
| `Audio/Water/SFX_dive.mp3` | 206.2KB | `ac96bcfb58007ff4f99cb7a2bde90387` | 钓钩入水（原名 `large-underwater-explosion`） |
| `Audio/Fishing/SFX_catchfish.ogg` | 4.9KB | `326af99c00780ba43824e3a4cde20f14` | 咬钩；兼作河豚受罚（降调） |
| `Audio/Fishing/SFX_coin.wav` | 140.5KB | `689a425a93a397a4da78078cfdb72d3e` | 出水结算进账 |
| `Audio/Shop/SFX_buy.wav` | 94.1KB | `c2fb3ee8109a10e428111ac09dab8e24` | 购买成功 / 解锁槽位 |
| `Audio/Shop/SFX_sellout.wav` | 140.5KB | `dd6293f365fa0204bbc6fcb5171d4053` | 卖出布 / 卖出装备 |
| `Audio/Shop/SFX_takeItem.wav` | 216.6KB | `3e80a2c8b21282d489a19b9f7e601888` | 抓起物品 |
| `Audio/Shop/SFX_takeItemDown.wav` | 324.3KB | `51748d167ccc42a478d5f73751472d31` | 放下物品 / 购买作废 |
| `Audio/UI/Common/SFX_UIClick.ogg` | 4.5KB | `2420b8d8d6ccdb4498249933c914062c` | UI 点击：主界面走 UIAudio 直引，商店四个按钮走代码 |
| `Audio/UI/Common/SFX_UISwitch.ogg` | 7.5KB | `50a4708183a72a64d890bb570fafd845` | 刷新货架重抽成功 |

### 3.1 为什么改名两个 Water 素材

`SoundManager` 的约定是 **Addressable 地址 = 资源名**。要让 `SFXDefines` 里出现 `large-underwater-explosion` 这种既不符 `SFX_`/`BGM_` 前缀、语义又完全不对（文件名写着爆炸，实际是入水）的常量，不如改名。
改名走的是「文件与 `.meta` 成对搬走」，GUID 不变（`ac96bcfb…` / `70dc359b…` 已复核），且全工程（`Assets` / `ProjectSettings` / `Packages`）没有任何按路径引用这两个 mp3 的地方，Addressables 条目本身按 GUID 索引，因此零副作用。
若用户不喜欢这两个新名字，只需连 `.meta` 一起改回去，再同步 `SFXDefines` 两行与 `SFX.asset` 两条 `m_Address`。

## 四、接入总表（触发时机 → 代码位置 → 地址）

行号是本文件写成时 `Assets/Scripts` 下的实际位置，改动后请重新 grep，不要照抄。

| 触发时机 | 代码位置 | 声音 |
| --- | --- | --- |
| 主菜单打开 | `UIMainMenu.cs:33` | `BGM_mainMenu`，音量 0.6 |
| 钓钩入水（Idle→Descending） | `FishingController.cs:431` | `SFX_dive` |
| 入水后接管水底循环 | `FishingController.cs:433` | `BGM_underwaterLoop` |
| 钓钩出水（→Landed）淡停 | `FishingController.cs:437` | `StopBGM()`，0.25s |
| 鱼咬钩挂上钓钩 | `FishingController.cs:463` | `SFX_catchfish` |
| 撞河豚被定身 | `FishingController.cs:494` | `SFX_catchfish`（vol 0.9 / pitch 0.62 顶替） |
| 出水结算且本趟有金币 | `FishingController.cs:517` | `SFX_coin` |
| 商店面板打开 | `UIShopPanel.cs:383` | `BGM_shop` |
| 商店面板关闭 | `UIShopPanel.cs:395` | `StopBGM()`，0.25s |
| 点「开始下潜」/「关闭」 | `UIShopPanel.cs:2144` | `SFX_UIClick` |
| 点「刷新」（按下即响，钱不够也响） | `UIShopPanel.cs:2123` | `SFX_UIClick` |
| 刷新真换了货 | `UIShopPanel.cs:2135` | `SFX_UISwitch` |
| 点「可合成 N 组」（按下即响，没得合也响） | `UIShopPanel.cs:1219` | `SFX_UIClick` |
| 购买成功 | `UIShopPanel.cs:2099` | `SFX_buy` |
| 解锁商店槽位成功 | `UIShopPanel.cs:840` | `SFX_buy` |
| 购买作废（钱不够） | `UIShopPanel.cs:2105` | `SFX_takeItemDown` |
| 购买作废（铺不开 / 架子满） | `UIShopPanel.cs:2111` | `SFX_takeItemDown` |
| 卖出布料 | `UIShopPanel.cs:503` | `SFX_sellout` |
| 卖出装备 | `UIShopPanel.cs:2034` | `SFX_sellout` |
| 抓起 背包物品 / 布料 / 商品 | `UIShopPanel.cs:565` / `684` / `780` | `SFX_takeItem` |
| 放下 背包物品 / 布料 | `UIShopPanel.cs:574` / `705` | `SFX_takeItemDown` |

口径说明：

- 「空手出水不响」：`settleLanded` 判 `dive.Coins > 0` 才播金币音，免得每下起竿都响成收钱。
- **商品落位音为什么不挂 `EndGoodsDrag`**：`UIShopGoodsItem.cs:246` 是先 `EndGoodsDrag` 再试成交，挂在收尾会和紧接着的 `SFX_buy` 同帧双响；
  于是挂在失败分支，语义正好是「整笔作废、东西放回货架」。完全拖空（松手在货架与网格之外）仍留无声，没为它加新接口。
- **合成成功没有专属音**：现在只有按下那一声 `SFX_UIClick`，飞行动画是哑的。缺素材，见 §十。

## 五、BGM 规则（最终口径，推翻过一版）

`SoundManager` 有两条独立通道：`m_bgmSource`（单曲、循环、流式）与 `m_effectSource` + 一次性 runtime source。
`loop_underwater` 用户称它是「BGM_Loop」，所以走 **BGM 通道**，而不是 `PlayLoop()` 的环境音分层。

现在的完整规则，一句话：**水面上不放任何 BGM**。

| 场景 | BGM |
| --- | --- |
| 主菜单 | `BGM_mainMenu`（0.6） |
| 游戏内水面（开局、出水后） | **静音** |
| 钓钩入水 → 收线 → 缠绕 | `BGM_underwaterLoop` 一路续着 |
| 出水（`Landed`） | 0.25s 淡停 |
| 商店面板 | `BGM_shop`；关面板 0.25s 淡停 |

被推翻的旧版是「出水不切回、水底循环一路续到商店之外、全程硬切无淡出」，理由当时是「不加新 API 也能连续」。用户明确否掉了水面放主界面曲，于是：

1. `FishingController.handlePhaseAudio` 改成只认两个阶段：`Descending` 起水底循环，`Landed` 停掉；仍挂在 `publishPhaseChanged` 的 `if (phaseChanged)` **块内**，一次下潜只触发一次。
2. `UIShopPanel.OnDisable` 从「播回主界面曲」改成 `StopBGM()`。
3. 淡出是新增能力，见 §六。

**连续下潜会从曲子开头重放**，这是有意的：中间水面本来就静音，从头起比续着半截自然。`PlayBGM` 的「同首在响就跳过」闸门只在没停过的情况下生效（例如商店反复开关不会把 `BGM_shop` 顶回开头）。

## 六、SoundManager 的坑与改动

`Assets/Scripts/Framework/QTower/Common/Sound/SoundManager.cs`

1. **BGM 反复重启**：`PlayBGM` 原为无条件重载，面板反复开关会把曲子顶回开头。新增 `private static string m_bgmRes;`，同首且 `isPlaying` 直接 return。
2. **异步竞态**：`PlayBgmAsync` 是 await 加载，快速切页时旧请求可能后到盖掉新 BGM。await 之后补 `if (m_bgmRes != res) return;` 让过期请求作废。
3. **旧 `PlayEffect(string res)` 注释与实现不符**：注释写「非空间化」，实现是 `PlayEffect(res, Vector3.zero)` → `spatialBlend = 1f` 且钉在世界原点，
   `minDistance 1 / maxDistance 18` 的线性衰减在几十米深会把音效压成静音（本轮之前零调用点，所以从没暴露）。
   新增真正的 2D 通道 `PlaySFX(res, volumeScale, pitch)` → `PlayEffect(clip, Vector3.zero, vol, pitch, false)`，旧签名改为转发，语义与注释对齐。
4. **新增 `StopBGM(fadeSeconds = 0.25f)`**：没有它就没法实现「水面静音」。配套两个私有字段：
   - `m_bgmSeq`：**代际号**。起停都 `++`，排队中的旧淡出与旧加载靠比对它自行作废，不会出现「淡出把新曲子音量抹成零」；
   - `m_bgmScale`：单首曲子的音量倍率，最终音量 = `m_bgmVolume * m_totalVolume * m_bgmScale`。主界面那首压到 0.6 靠它，**不动全局音量设置**。
   `FadeOutBgmAsync` 逐帧 `Lerp` 到零再 `Stop()`；`fadeSeconds <= 0` 或本来没在播就是硬停。淡出期间被新的 `PlayBGM` 接管则本次淡出静默退出，音量由对方决定。

## 七、Addressables 与 `.meta` 的取舍

- **BGM 没新建独立 group**，复用 `SFX` group 并以 Label 区分（SFX 用 `SFX`，音乐用 `BGM`）。手写新 group 要连改 `AddressableAssetSettings.asset` + 两个 Schema，而本轮**没有编辑器通道可验证**，风险不值当。
- `Assets/AddressableAssetsData/AssetGroups/SFX.asset` 现共 **12 条地址**（含补的 `SFX_dive` 与 `BGM_underwaterLoop`），追加前均断言锚点唯一、且确认目标地址此前不存在。
- `.meta` 的 `loadType: 0 → 2`（Streaming）共三处：`BGM_mainMenu`、`BGM_shop`、`BGM_underwaterLoop`。三条都是长音乐，常驻解压白占十几 MB 内存；短音效保持 `0`。

## 八、UI 点击音为什么走代码而不是 UIAudio

`UIAudio`（`Framework.QTower.MVC.View`）是 prefab 手动挂、`[SerializeField] AudioClip` **直引 clip 不走 Addressables**，只处理 `Button.onClick` 与 `IPointerEnter`。
工程里只有 `UIMainMenu.prefab`（2 处）与 `UISetting.prefab`（2 处）挂了它，`UIShopPanel.prefab` 一处都没有。

用户说「这俩可以参考主界面的那些按钮」，指的是**听感对齐**（同一枚 `SFX_UIClick`），不是要求实现方式一致。
本轮没有 Unity 编辑器通道，手改 prefab YAML 加组件风险高（`UIShopPanel.prefab` 是用户自己在编辑的资产，撞车概率大），
所以商店四个按钮（`Btn_Dive` / `Btn_Close` / `Btn_Refresh` / `Btn_CraftAll`）统一在点击回调里 `SoundManager.PlaySFX(SFXDefines.UI_CLICK)`，效果与 UIAudio 等价。

**若用户想统一成 UIAudio 组件**，在编辑器里给这四个按钮挂 `UIAudio` + `ClickClip = SFX_UIClick`，然后把代码里对应的四行删掉，否则会双响。

一个连带改动：`Btn_CraftAll` 原先「0 组就 `interactable = false`」，而 Unity 的禁用按钮会连 `onClick` 一起吞掉 —— 玩家点了既没反应也没声音，只会以为按钮坏了。
现在改成**按钮常开**，按下必响一声，有没有得合由文案（`可合成 N 组` / `无合成`）和 `PlanMerges` 的结果说话。

## 九、本轮踩到并当场抓住的错（教训）

用 Node 打补丁时，把已经 `join()` 成字符串的锚点又按 `a2[0]`/`a2[1]` 当「行」取，实际取到的是**空格字符**，
结果 `canCatchFish` 的注释与方法签名被替换成两行空格，编译报出 4 个 CS1519。
另外第一次插 `handlePhaseAudio(phase)` 时插在了 `if (phaseChanged)` **块外**，那会在整个下潜期间每帧触发，入水音变成机关枪。
两处都是**写完立刻读回相关区域 + 编译**才发现的，不是靠运气：改完不重读落点、不编译，就是给自己埋雷。

还有一次是把「出水不切回」当成定论写进了本文件，下一轮就被用户否掉 —— **设计口径没确认前别写成结论**，否则归档会变成误导源。

## 十、待用户决策（不要自行开工）

1. **河豚专音**：现借咬钩音降调（`SFXDefines.HAZARD` 一行地址）。有合适的「电击 / 阻碍」音就换掉。
2. **两个 Water 素材的新名字**是否满意（`SFX_dive` / `BGM_underwaterLoop`）。
3. **主界面 BGM 的 0.6** 与 **淡出 0.25s** 是我替用户拍的板（他只说「太大了」「只允许在水下响」，没给数字）。嫌响改 `MAIN_MENU_BGM_VOLUME`，嫌淡改 `StopBGM` 实参，想硬停传 `0f`。
4. **惊散鱼**（`Scatter`）与**合成成功**无音；合成模块功能本身仍是「暂缓」，别自行扩。
5. 布操作条上的**「旋转」按钮无音**（`handleRotateClicked`），暂存 / 卖出已经分别有 `SFX_takeItemDown` / `SFX_sellout`。要不要给旋转配一声，等用户点头。

## 十一、验证状态

- `dotnet build Assembly-CSharp.csproj`：**0 错误**，7 个 MSB3277（`System.IO.Compression` 版本冲突）为工程既有噪声。
- 全工程 `SoundManager.*` 调用点 **25 处**（`PlaySFX` 19 / `PlayBGM` 3 / `StopBGM` 2 / `UIAudio` 内部 `PlayUIEffect` 1），地址与 `SFX.asset` 的 12 条一一对应。
- **未做实机播放验证**：需回 Unity 让 `AssetDatabase` 刷新（改名、新条目、`.meta` 都要重导入，Addressables 需重建 catalog），然后 Play 一次完整流程：主菜单 → 入水 → 咬钩 → 出水（**这里应该静音**）→ 商店 → 购买 → 刷新 → 拖拽 → 卖出 → 关店（**这里也应该静音**）。

## 十二、下一轮建议

按「10-13 交作品」倒排：先实机验音效有没有哑火（重点验水面是否真的安静、四个按钮点击音是否各只响一声）→ 再定 §十 的 1/2/3 → UIAudio 要不要统一由用户在编辑器手点。
其余（合成、BUFF 数值微调）保持挂账，**数值一律不再自行改动**。
