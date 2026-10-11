# FishGame 音效与 BGM 全量接入

> 本记录接续：2026-10-11/10-河豚定幅巡守与挂账口径纠正.md　　本轮日期：2026-10-11　　基线 HEAD：dd0faab「调整异形块的大小」

## 一、记录范围

用户自己把素材下载进 `Assets/Audio`，本轮只做一件事：**把这批音效与 BGM 接进游戏**（找网站的环节已结束）。
挂账的东西一律没碰：**任何数值配置（`.asset` / SO 参数）**、合成模块、UIAudio 在 prefab 上的手动挂载（无编辑器通道，见 §六）。
本轮改过的非代码资源只有三类：Addressables 的 `SFX.asset` 条目表、四个音频的 `.meta`（`loadType` 与改名后的文件名）、以及两个 Water 素材的**改名**。

## 二、用户诉求原话

> 「全部准备好了，你帮我一键替换吧：D:\\CCodes\\unity\\FishGame\\Assets\\Audio，我有一个通用的组件是 UIAudio，不过 bgm 不能用这个，你看情况用一下吧，开始吧」
> （补）「Assets/Audio/Water，钓鱼的你没用啊，这是钓鱼的BGM，一个是刚潜入水下的音效，一个是在水底中的BGM_Loop」

第一轮我漏接了 `Assets/Audio/Water` 两枚，把 `loop_underwater` 当成「待决策的环境音」挂起，实际用户已经明确它们是钓鱼用的。本轮补齐。

早前已定口径（沿用）：

> 「音效的话晚点再找。先修复数值吧」→ 数值前几轮完成并经实测通过
> 「勾到鱼还需要一个音效，就简单的音效就行」→ 用户下载了 `SFX_catchfish.ogg`

## 三、素材清单与用途分配

| 文件 | 大小 | GUID | 用途 |
| --- | --- | --- | --- |
| `Audio/BGM/BGM_mainMenu.ogg` | 1964.7KB | `08063d9f6bdd57b4ba37a6389d665c8c` | 主菜单与水上场景 BGM |
| `Audio/BGM/BGM_shop.mp3` | 4492.9KB | `963b43e7ead9c8144a30bbca28222137` | 商店 BGM |
| `Audio/Water/BGM_underwaterLoop.mp3` | 2561.2KB | `70dc359bdb67c9c44967d402b8c4aa31` | 水底 BGM 循环（原名 `loop_underwater`） |
| `Audio/Water/SFX_dive.mp3` | 206.2KB | `ac96bcfb58007ff4f99cb7a2bde90387` | 钓钩入水（原名 `large-underwater-explosion`） |
| `Audio/Fishing/SFX_catchfish.ogg` | 4.9KB | `326af99c00780ba43824e3a4cde20f14` | 咬钩 |
| `Audio/Fishing/SFX_coin.wav` | 140.5KB | `689a425a93a397a4da78078cfdb72d3e` | 出水结算进账 |
| `Audio/Shop/SFX_buy.wav` | 94.1KB | `c2fb3ee8109a10e428111ac09dab8e24` | 购买成功 / 解锁槽位 |
| `Audio/Shop/SFX_sellout.wav` | 140.5KB | `dd6293f365fa0204bbc6fcb5171d4053` | 卖出布 / 卖出装备 |
| `Audio/Shop/SFX_takeItem.wav` | 216.6KB | `3e80a2c8b21282d489a19b9f7e601888` | 抓起物品 |
| `Audio/Shop/SFX_takeItemDown.wav` | 324.3KB | `51748d167ccc42a478d5f73751472d31` | 放下物品 / 购买作废 |
| `Audio/UI/Common/SFX_UIClick.ogg` | 4.5KB | `2420b8d8d6ccdb4498249933c914062c` | 原有，UIAudio 点击（prefab 直引） |
| `Audio/UI/Common/SFX_UISwitch.ogg` | 7.5KB | `50a4708183a72a64d890bb570fafd845` | 原有，兼作「刷新货架」反馈 |

**河豚专音缺失**：`SFXDefines.HAZARD` 暂借咬钩音 `SFX_catchfish`，以 `pitch 0.62 / vol 0.9` 降调顶替，换素材只改这一行地址。

### 3.1 为什么改名两个 Water 素材

`SoundManager` 的约定是 **Addressable 地址 = 资源名**。要让 `SFXDefines` 里出现 `large-underwater-explosion` 这种既不符 `SFX_`/`BGM_` 前缀、语义又完全不对（文件名写着爆炸，实际是入水）的常量，不如改名。
改名走的是「文件与 `.meta` 成对搬走」，GUID 不变（`ac96bcfb…` / `70dc359b…` 已复核），且全工程（`Assets` / `ProjectSettings` / `Packages`）没有任何按路径引用这两个 mp3 的地方，Addressables 条目本身按 GUID 索引，因此零副作用。
若用户不喜欢这两个新名字，只需连 `.meta` 一起改回去，再同步 `SFXDefines` 两行与 `SFX.asset` 两条 `m_Address`。

## 四、接入总表（触发时机 → 代码位置 → 地址）

| 触发时机 | 代码位置 | Addressable 地址 |
| --- | --- | --- |
| 主菜单打开（含从商店退回） | `UIMainMenu.cs:30` | `BGM_mainMenu` |
| 钓钩入水（Idle→Descending） | `FishingController.cs:431` | `SFX_dive` |
| 钓钩入水后接管水底音乐 | `FishingController.cs:434` | `BGM_underwaterLoop` |
| 鱼咬钩挂上钓钩 | `FishingController.cs:458` | `SFX_catchfish` |
| 钓钩撞河豚被定身 | `FishingController.cs:489` | `SFX_catchfish`（降调顶替） |
| 出水结算且本趟有金币 | `FishingController.cs:512` | `SFX_coin` |
| 商店面板打开 | `UIShopPanel.cs:383` | `BGM_shop` |
| 商店面板关闭 | `UIShopPanel.cs:395` | `BGM_mainMenu` |
| 购买成功 | `UIShopPanel.cs:2096` | `SFX_buy` |
| 解锁商店槽位成功 | `UIShopPanel.cs:840` | `SFX_buy` |
| 购买作废（钱不够） | `UIShopPanel.cs:2102` | `SFX_takeItemDown` |
| 购买作废（铺不开 / 架子满） | `UIShopPanel.cs:2108` | `SFX_takeItemDown` |
| 点击刷新货架成功 | `UIShopPanel.cs:2128` | `SFX_UISwitch` |
| 卖出布料 | `UIShopPanel.cs:503` | `SFX_sellout` |
| 卖出装备 | `UIShopPanel.cs:2031` | `SFX_sellout` |
| 抓起 背包物品 / 布料 / 商品 | `UIShopPanel.cs:565` / `684` / `780` | `SFX_takeItem` |
| 放下 背包物品 / 布料 | `UIShopPanel.cs:574` / `705` | `SFX_takeItemDown` |

口径说明：

- 「空手出水不响」：`settleLanded` 判 `dive.Coins > 0` 才播金币音，免得每下起竿都响成收钱。
- **商品落位音为什么不挂 `EndGoodsDrag`**：`UIShopGoodsItem.cs:246` 是先 `EndGoodsDrag` 再试成交，挂在收尾会和紧接着的 `SFX_buy` 同帧双响；
  于是挂在失败分支，语义正好是「整笔作废、东西放回货架」。完全拖空（松手在货架与网格之外）仍留无声，没为它加新接口。

## 五、水底 BGM 的通道选择（本轮的关键设计）

`SoundManager` 有两条独立通道：`m_bgmSource`（单曲、循环、流式）与 `m_effectSource` + 一次性 runtime source。
`loop_underwater` 用户称它是「BGM_Loop」，所以走 **BGM 通道**，而不是 `PlayLoop()` 的环境音分层。理由：

1. 若按「每次下潜起、每次出水停」挂在 `PlayLoop`，一次回合要起停数次，`AudioSource` 建销与音量管理都要另写一套淡入淡出；
2. 走 BGM 通道则天然复用 `PlayBGM` 已有的「同首还在响就跳过」闸门：**第一潜切入水底循环，后续每潜直接命中闸门不重开**，出水也不切回，音乐连续；
   回合间进商店自然切 `BGM_shop`，出商店回 `BGM_mainMenu`，下一次入水再自然切回水底循环 —— 全程不需要新增任何 API。

唯一的代价是水上↔水底切换是硬切（无淡入淡出）。要淡的话在 `PlayBGM` 里加一小段音量插值即可，但那是全局行为，本轮没动，等用户点头。

## 六、SoundManager 的三个坑与本轮改动

`Assets/Scripts/Framework/QTower/Common/Sound/SoundManager.cs`

1. **BGM 反复重启**：`PlayBGM` 原为无条件重载，面板反复开关会把曲子顶回开头。新增 `private static string m_bgmRes;`，同首且 `isPlaying` 直接 return。
2. **异步竞态**：`PlayBgmAsync` 是 await 加载，快速切页时旧请求可能后到盖掉新 BGM。await 之后补 `if (m_bgmRes != res) return;` 让过期请求作废。
3. **旧 `PlayEffect(string res)` 注释与实现不符**：注释写「非空间化」，实现是 `PlayEffect(res, Vector3.zero)` → `spatialBlend = 1f` 且钉在世界原点，
   `minDistance 1 / maxDistance 18` 的线性衰减在几十米深会把音效压成静音（本轮之前零调用点，所以从没暴露）。
   新增真正的 2D 通道 `PlaySFX(res, volumeScale, pitch)` → `PlayEffect(clip, Vector3.zero, vol, pitch, false)`，旧签名改为转发，语义与注释对齐。

## 七、Addressables 与 `.meta` 的取舍

- **BGM 没新建独立 group**，复用 `SFX` group 并以 Label 区分（SFX 用 `SFX`，音乐用 `BGM`）。手写新 group 要连改 `AddressableAssetSettings.asset` + 两个 Schema，而本轮**没有编辑器通道可验证**，风险不值当。
- `Assets/AddressableAssetsData/AssetGroups/SFX.asset` 现共 **12 条地址**（含本轮补的 `SFX_dive` 与 `BGM_underwaterLoop`），追加前均断言锚点唯一、且确认目标地址此前不存在。
- `.meta` 的 `loadType: 0 → 2`（Streaming）共三处：`BGM_mainMenu`、`BGM_shop`、`BGM_underwaterLoop`。三条都是长音乐，常驻解压白占十几 MB 内存；短音效保持 `0`。
- **UIAudio 完全没动**：它是 prefab 手动挂、`[SerializeField] AudioClip` 直引 clip（不走 Addressables），只处理 `Button.onClick` 与 `IPointerEnter`。
  工程里只有 `UIMainMenu.prefab`（2 处）与 `UISetting.prefab`（2 处）挂了它，`UIShopPanel.prefab` 一处都没有。BGM 按用户要求完全没走 UIAudio。

## 八、本轮踩到并当场抓住的错（教训）

用 Node 打补丁时，把已经 `join()` 成字符串的锚点又按 `a2[0]`/`a2[1]` 当「行」取，实际取到的是**空格字符**，
结果 `canCatchFish` 的注释与方法签名被替换成两行空格，编译报出 4 个 CS1519。
另外第一次插 `handlePhaseAudio(phase)` 时插在了 `if (phaseChanged)` **块外**，那会在整个下潜期间每帧触发，入水音变成机关枪。
两处都是**写完立刻读回相关区域 + 编译**才发现的，不是靠运气：改完不重读落点、不编译，就是给自己埋雷。

## 九、待用户决策（不要自行开工）

1. **河豚专音**：现借咬钩音降调。有合适的「电击 / 阻碍」音就换 `SFXDefines.HAZARD` 一行。
2. **两个 Water 素材的新名字**是否满意（`SFX_dive` / `BGM_underwaterLoop`）。
3. **水底 BGM 要不要淡入淡出**，以及出水是否真的该继续水底循环（当前是不切回，见 §五）。
4. **惊散鱼**（`Scatter`）与**合成**无音；合成模块本身暂缓。
5. **UIAudio 手动挂载**（编辑器里点，比改 prefab YAML 安全）：建议 `Btn_Refresh`、`Btn_Leave`、商品卡 `Btn` 挂 UIAudio + `ClickClip = SFX_UIClick`。
   **别在已会播 `SFX_buy` / `SFX_sellout` 结果音的按钮上再挂点击音**，会双响。

## 十、验证状态

- `dotnet build Assembly-CSharp.csproj`：**0 错误**，7 个 MSB3277（`System.IO.Compression` 版本冲突）为工程既有噪声。
- 代码里 `SoundManager.PlaySFX(` / `PlayBGM(` 共 **20 处调用点**，地址与 `SFX.asset` 的 12 条一一对应；`SFX_UIClick` / `SFX_UISwitch` 另有 UIAudio 直引一路。
- **未做实机播放验证**：需回 Unity 让 `AssetDatabase` 刷新（改名、新条目、`.meta` 都要重导入，Addressables 需重建 catalog），然后 Play 一次完整流程：主菜单 → 入水 → 咬钩 → 出水 → 商店 → 购买 → 刷新 → 拖拽 → 卖出。

## 十一、下一轮建议

按「10-13 交作品」倒排：先实机验音效有没有哑火（尤其水底 BGM 切入是否自然）→ 再定 §九 的 1/2/3 → UIAudio 由用户在编辑器手点。
其余（合成、BUFF 数值微调）保持挂账，不再自行扩范围。
