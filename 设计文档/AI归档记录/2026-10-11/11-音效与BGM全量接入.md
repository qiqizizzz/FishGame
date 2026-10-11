# FishGame 音效与 BGM 全量接入

> 本记录接续：2026-10-11/10-河豚定幅巡守与挂账口径纠正.md　　本轮日期：2026-10-11　　基线 HEAD：dd0faab「调整异形块的大小」

## 一、记录范围

用户自己把素材下载进 `Assets/Audio`，本轮只做一件事：**把这批音效与两首 BGM 接进游戏**（找网站的环节已结束）。
四件挂账的东西一律没碰：**任何数值配置（`.asset` / SO 参数）**、合成模块、UIAudio 在 prefab 上的手动挂载（无编辑器通道，见 §六）。
本轮唯一被修改的非代码资源是「接入必需」的两类：Addressables 的 `SFX.asset` 条目表、两个 BGM 的 `.meta`（`loadType`）。

## 二、用户诉求原话

> 「全部准备好了，你帮我一键替换吧：D:\\CCodes\\unity\\FishGame\\Assets\\Audio，我有一个通用的组件是 UIAudio，不过 bgm 不能用这个，你看情况用一下吧，开始吧」

早前已定口径（本轮沿用）：

> 「音效的话晚点再找。先修复数值吧」→ 数值已在前几轮完成并经用户实测通过
> 「勾到鱼还需要一个音效，就简单的音效就行」→ 用户最终下载了 `SFX_catchfish.ogg`

## 三、素材清单与用途分配

| 文件 | 大小 | GUID | 用途 |
| --- | --- | --- | --- |
| `Audio/BGM/BGM_mainMenu.ogg` | 1964.7KB | `08063d9f6bdd57b4ba37a6389d665c8c` | 主菜单/水下场景 BGM |
| `Audio/BGM/BGM_shop.mp3` | 4492.9KB | `963b43e7ead9c8144a30bbca28222137` | 商店 BGM |
| `Audio/Fishing/SFX_catchfish.ogg` | 4.9KB | `326af99c00780ba43824e3a4cde20f14` | 咬钩（鱼上钩瞬间） |
| `Audio/Fishing/SFX_coin.wav` | 140.5KB | `689a425a93a397a4da78078cfdb72d3e` | 出水结算，进账金币时 |
| `Audio/Shop/SFX_buy.wav` | 94.1KB | `c2fb3ee8109a10e428111ac09dab8e24` | 购买成功 / 解锁商店槽位 |
| `Audio/Shop/SFX_sellout.wav` | 140.5KB | `dd6293f365fa0204bbc6fcb5171d4053` | 卖出布 / 卖出装备 |
| `Audio/Shop/SFX_takeItem.wav` | 216.6KB | `3e80a2c8b21282d489a19b9f7e601888` | 抓起物品（背包/暂存架/商品） |
| `Audio/Shop/SFX_takeItemDown.wav` | 324.3KB | `51748d167ccc42a478d5f73751472d31` | 放下物品（落位/回架） |
| `Audio/UI/Common/SFX_UIClick.ogg` | 4.5KB | `2420b8d8d6ccdb4498249933c914062c` | 原有，UIAudio 点击（prefab 直引） |
| `Audio/UI/Common/SFX_UISwitch.ogg` | 7.5KB | `50a4708183a72a64d890bb570fafd845` | 原有，兼作「刷新货架」反馈 |
| `Audio/Water/loop_underwater.mp3` | — | `70dc359bdb67c9c44967d402b8c4aa31` | **未接**，水下环境循环，无人调用 |
| `Audio/Water/large-underwater-explosion.mp3` | — | `ac96bcfb58007ff4f99cb7a2bde90387` | **未接**，不贴合玩法语义 |

**注意：用户没有下载「河豚阻碍」专音。** `SFXDefines.HAZARD` 暂借咬钩音 `SFX_catchfish` 并以 `pitch 0.62 / vol 0.9` 降调顶替，
换素材时**只需改 `SFXDefines.HAZARD` 一行地址**，不必动调用点。

## 四、接入总表（触发时机 → 代码位置 → 地址）

| 触发时机 | 代码位置 | Addressable 地址 |
| --- | --- | --- |
| 主菜单打开（含从商店退回） | `UIMainMenu.cs:30` | `BGM_mainMenu` |
| 商店面板打开 | `UIShopPanel.cs:383` | `BGM_shop` |
| 商店面板关闭 | `UIShopPanel.cs:395` | `BGM_mainMenu` |
| 鱼咬钩挂上钓钩 | `FishingController.cs:445` | `SFX_catchfish` |
| 钓钩撞到河豚被定身 | `FishingController.cs:476` | `SFX_catchfish`（降调顶替） |
| 出水结算且本趟有金币 | `FishingController.cs:499` | `SFX_coin` |
| 购买成功（结算回调） | `UIShopPanel.cs:840` | `SFX_buy` |
| 解锁商店槽位成功 | `UIShopPanel.cs:2096` | `SFX_buy` |
| 点击刷新货架成功 | `UIShopPanel.cs:2125` | `SFX_UISwitch` |
| 卖出布料 | `UIShopPanel.cs:503` | `SFX_sellout` |
| 卖出装备 | `UIShopPanel.cs:2031` | `SFX_sellout` |
| 抓起背包物品 | `UIShopPanel.cs:565` | `SFX_takeItem` |
| 放下背包物品 | `UIShopPanel.cs:574` | `SFX_takeItemDown` |
| 抓起布料 | `UIShopPanel.cs:684` | `SFX_takeItem` |
| 放下布料 | `UIShopPanel.cs:705` | `SFX_takeItemDown` |
| 从货架拎起商品（尚未扣钱） | `UIShopPanel.cs:780` | `SFX_takeItem` |
| 购买作废（钱不够） | `UIShopPanel.cs:2102` | `SFX_takeItemDown` |
| 购买作废（铺不开 / 架子满） | `UIShopPanel.cs:2108` | `SFX_takeItemDown` |

「空手出水不响」：`settleLanded` 里判 `dive.Coins > 0` 才播金币音，避免每趟都响。

**为什么商品的落位音挂在失败分支而不是 `EndGoodsDrag`**：`UIShopGoodsItem.OnEndDrag`（`246` 行）是先 `EndGoodsDrag` 再试成交的，
在 `EndGoodsDrag` 里无脑播 `SFX_takeItemDown` 会和紧接着的 `SFX_buy` 同一帧双响。买成败负由服务端决定，
所以作废的两条分支各播一次落位音，语义正好是「东西放回货架」；完全拖空（松手在货架与网格之外）仍留无声，没为它加新接口。

## 五、SoundManager 的三个坑与本轮改动

`Assets/Scripts/Framework/QTower/Common/Sound/SoundManager.cs`

1. **BGM 反复重启**：`PlayBGM` 原来无条件重载。商店 `OnOpen`/`OnDisable` 会在两首歌之间来回切，若不加闸门，每次开关商店都把曲子顶回开头。
   新增 `private static string m_bgmRes;`，同首且 `isPlaying` 时直接 return；主菜单/商店的互相接管因此变成幂等的「喊一嗓子」，不用判来源。
2. **异步竞态**：`PlayBgmAsync` 是 await 加载的，快速切页时旧请求可能后到、把新 BGM 盖掉。await 之后补 `if (m_bgmRes != res) return;` 让过期请求作废。
3. **旧 `PlayEffect(string res)` 注释与实现不符**：注释写「非空间化」，实现却是 `PlayEffect(res, Vector3.zero)` → `spatialBlend = 1f` 且钉在世界原点，
   钓钩下到几十米深时 `minDistance 1 / maxDistance 18` 的线性衰减直接把它压成静音（本轮之前它零调用点，所以从没暴露）。
   现在新增真正的 2D 通道 `PlaySFX(res, volumeScale, pitch)` → `PlayEffect(clip, Vector3.zero, vol, pitch, false)`，
   旧签名改为转发 `PlaySFX`，语义与注释对齐。

新增 API 均带 XML `<summary>`；`PlaySFX` 支持 `pitch` 正是为了让河豚顶替音不必另找素材。

## 六、Addressables 与 `.meta` 的取舍

- **BGM 没新建独立 group**，复用 `SFX` group 并以 Label 区分（6 条 SFX 用 `SFX`，2 条 BGM 用 `BGM`）。
  手写新 group 要同时改 `AddressableAssetSettings.asset` + 两个 Schema 文件，而本轮**没有 Unity 编辑器 / MCP 通道可验证**，风险不值得。
- `Assets/AddressableAssetsData/AssetGroups/SFX.asset` 现共 10 条地址，追加前已断言锚点唯一、且确认 `SFX_catchfish` 等此前不存在。
- 两个 BGM 的 `.meta`：`loadType: 0 → 2`（Streaming）。约 6.4MB 的长音乐若用 Decompress On Load 会全程占内存；短音效保持 `0` 不动。
- **UIAudio 完全没动**：它是 prefab 上手动挂的、`[SerializeField] AudioClip` 直引 clip（不走 Addressables），只处理 `Button.onClick` 与 `IPointerEnter`。
  目前工程里只有 `UIMainMenu.prefab`（2 处）与 `UISetting.prefab`（2 处）挂了它，`UIShopPanel.prefab` 一处都没有。
  本轮把商店反馈全走代码里的 `PlaySFX`，BGM 按用户要求完全没走 UIAudio。

## 七、待用户决策（不要自行开工）

1. **河豚专音**：现在借咬钩音降调。有合适的「电击 / 阻碍」音就换 `SFXDefines.HAZARD` 一行地址。
2. **水下环境循环音** `loop_underwater.mp3`：要不要接（`SoundManager.PlayLoop` 已具备能力，但需要一个「入水 / 出水」的开关时机），且它尚未注册进 Addressables。
3. **惊散鱼**（`Scatter`）与**合成**目前无音。合成模块本身暂缓。
4. **UIAudio 手动挂载**（编辑器里点，比改 prefab YAML 快且零风险）：建议给 `Btn_Refresh`、`Btn_Leave`、商品卡 `Btn` 挂 UIAudio + `ClickClip = SFX_UIClick`。
   **别在已经会播 `SFX_buy` / `SFX_sellout` 结果音的按钮上再挂点击音**，会双响。

## 八、验证状态

- `dotnet build Assembly-CSharp.csproj`：**0 错误**，7 个 MSB3277（`System.IO.Compression` 版本冲突）为工程既有噪声。
- 代码里 `PlaySFX(` / `PlayBGM(` 共 22 处命中（含定义与内部转发），地址与 `SFX.asset` 的 10 条一一对上。
- **未做实机播放验证**：需要回 Unity 让 `AssetDatabase` 刷新（新条目与 `.meta` 改动要重导入、Addressables 需重建 catalog），然后 Play 一次商店与一趟完整钓鱼。

## 九、下一轮建议

优先级按「10-13 交作品」倒排：先实机验一遍音效有没有哑火 → 再决定要不要接水下循环音 → UIAudio 由用户在编辑器里手点。
其余未做项（合成、BUFF 数值微调）保持挂账，不再自行扩范围。
