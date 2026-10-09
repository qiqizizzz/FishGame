/*
* ┌──────────────────────────────────┐
* │  描    述: 商店与背包面板，回合内展示金币、背包网格、仓库暂存与可购商品
* │  类    名: UIShopPanel.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 数值只读不写：金币与剩余下潜次数取自传入的 RunState，面板不回查任何玩法对象
* 背包与仓库读 RunBackpack 的真实数据，商品来自 ShopService 抽出来的货架库存
* 买、刷新、取回都由面板转给服务与模型，面板自己不算价格也不改库存
* 拖拽与旋转的落点校验由本面板作为宿主转给背包模型，成不成都重铺一次，让摆歪的物品弹回原位
* 布与装备分两层画：布由运行时建的一层垫在装备底下，装备仍走预制体里的模板，两层各自一套手势
* 布的样子就是底层格子染上布的底色，最上面再叠一层选中框，一格一圈角标，框不吃射线
* 选中一块布才浮出操作条，旋转、拆到暂存架、卖成金币三件事都在条上按，不给单击直达的机会
* 格子上不再常驻名称，点中布或装备都在面板最下方的详情区报两行：第一行名字，第二行能力线
* 拖一块布起来时它铺过的格子先空成锁格，手里那块布由视图的幽灵格跟着手指画
* 单格边长与间距从网格组件读，改版式只动预制体不动脚本
* 物品视图与商品卡由模板复制并留用，只开关不销毁，销毁会延到帧尾被布局算进内容高度
* 面板不决定流程走向，按钮只广播「离开商店」，切阶段由控制器负责
*/

using System.Collections.Generic;
using System.Linq;
using Framework.QTower.Common.Defines;
using Framework.QTower.Event;
using Framework.QTower.View;
using Module.Backpack.Data;
using Module.Backpack.Model;
using Module.Fish.Model;
using Module.Shop.Model;
using Module.Shop.Service;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utils.log;

namespace UI.Shop
{
    public sealed class UIShopPanel : UIBase, IShopBackpackHost
    {
        private const string COINS_PATH = "Bg/Panel_Info/Txt_Coins";
        private const string TARGET_PATH = "Bg/Panel_Info/Txt_Target";
        private const string DIVES_PATH = "Bg/Panel_Info/Txt_Dives";
        private const string GRID_PATH = "Bg/Panel_Backpack/Grid_Slots";
        private const string ITEMS_PATH = "Bg/Panel_Backpack/Layer_Items";
        private const string ITEM_TEMPLATE_PATH = ITEMS_PATH + "/Item_Template";
        private const string STORAGE_PATH = "Bg/Panel_Storage/Grid_Storage";
        private const string SHOP_PANEL_PATH = "Bg/Panel_Shop";
        private const string GOODS_PATH = SHOP_PANEL_PATH + "/Row_Goods";
        private const string GOODS_TEMPLATE_PATH = GOODS_PATH + "/Goods_Template";
        private const string REFRESH_PATH = "Bg/Panel_Shop/Btn_Refresh";
        private const string DIVE_PATH = "Bg/Btns/Btn_Dive";
        private const string CLOSE_PATH = "Bg/Btns/Btn_Close";

        // 布层与板块操作条都由运行时代码创建，预制体里没有这两个节点
        private const string CLOTH_LAYER_NAME = "Layer_Cloths";
        private const string SELECT_LAYER_NAME = "Layer_Select";
        private const string PLATE_VIEW_NAME = "Plate_View";
        private const string PLATE_BAR_NAME = "Bar_PlateActions";
        private const string PLATE_ROTATE_NAME = "Btn_Rotate";
        private const string PLATE_STASH_NAME = "Btn_Stash";
        private const string PLATE_SELL_NAME = "Btn_Sell";
        private const string SELECT_FRAME_NAME = "Img_SelectFrame";

        // 详情区也由运行时创建，预制体里没有这个节点，它卡在货架面板与底部按钮之间那条空隙
        private const string DETAIL_PANEL_NAME = "Panel_Detail";
        private const string DETAIL_NAME_TEXT_NAME = "Txt_DetailName";
        private const string DETAIL_DESC_TEXT_NAME = "Txt_DetailDesc";
        private const float DETAIL_TOP = -1522f;
        private const float DETAIL_WIDTH = 940f;
        private const float DETAIL_HEIGHT = 146f;
        private const float DETAIL_SIDE_PADDING = 36f;
        private const float DETAIL_NAME_TOP = -22f;
        private const float DETAIL_NAME_HEIGHT = 46f;
        private const float DETAIL_DESC_TOP = -74f;
        private const float DETAIL_DESC_HEIGHT = 44f;
        private const float DETAIL_NAME_FONT_SIZE = 30f;
        private const float DETAIL_DESC_FONT_SIZE = 24f;

        // 操作条的高取单格的不到一半，宽由三个按钮加内边距凑出来，横排自己排
        private const float BAR_HEIGHT_RATIO = 0.48f;
        private const float BAR_BUTTON_HEIGHT_RATIO = 0.74f;
        private const float BAR_BUTTON_WIDTH = 150f;
        private const float BAR_BUTTON_SPACING = 8f;
        private const float BAR_PADDING = 10f;
        private const float BAR_GAP = 10f;
        private const float BAR_FONT_SIZE = 24f;

        // ==================== 字段[外部设置] ====================
        [Tooltip("选中一块布时套在每一格上的外框图，取 Button_select_brown 这种四角标，中心要留空")]
        [SerializeField] private Sprite PlateSelectSprite;

        // ==================== 属性 ====================
        public int Columns => m_backpack != null ? m_backpack.Model.Columns : 0;

        public int Rows => m_backpack != null ? m_backpack.Model.Rows : 0;

        // ==================== 字段[私有] ====================
        private TMP_Text m_coinsText;
        private TMP_Text m_targetText;
        private TMP_Text m_divesText;
        private GridLayoutGroup m_grid;
        private UIShopSlotItem[] m_slots;
        private UIShopSlotItem[] m_storageSlots;
        private RectTransform m_itemsRect;
        private UIShopItemView m_itemTemplate;
        private RectTransform m_goodsRect;
        private UIShopGoodsItem m_goodsTemplate;
        private Button m_refreshButton;
        private Button m_diveButton;
        private Button m_closeButton;

        // 本局金币与次数，面板只读
        private RunState m_run;

        // 本局背包，面板只读它，不替它保管状态
        private RunBackpack m_backpack;

        // 本局商店，货架与刷新涨价都由它算
        private ShopService m_shop;

        // 从网格读出来的单格尺寸，摆物品时算跨格用
        private float m_cellSize;
        private float m_cellSpacing;

        private readonly List<UIShopItemView> m_itemViews = new();
        private readonly List<UIShopGoodsItem> m_goodsItems = new();
        private int m_itemsShown;
        private int m_goodsShown;

        // 布层的视图与计数，和装备层一样只开关不销毁
        private RectTransform m_clothsRect;

        private readonly List<UIShopPlateView> m_plateViews = new();
        private int m_platesShown;

        // 选中框层与它的格子框，同样只开关不销毁
        private RectTransform m_selectRect;
        private readonly List<Image> m_selectFrames = new();
        private int m_selectFramesUsed;

        // 选中一块布才浮出来的操作条，底板与三个按钮都在运行时建好
        private RectTransform m_plateBar;
        private Button m_plateRotateButton;
        private Button m_plateStashButton;
        private Button m_plateSellButton;
        private TMP_Text m_plateSellText;

        // 当前选中的布只有面板这一份，视图铺布色时来问，拆掉卖掉之后由面板清空
        private BackpackItemInstance m_selectedPlate;

        // 量一块布的外框用，操作条定位靠它
        private readonly List<Vector2Int> m_plateCells = new();

        // 被抓到手上的那块布，它铺过的格子在这期间显示成锁格，因为底板确实跟着手指走了
        private BackpackItemInstance m_plateInHand;

        // 幽灵格要抄底层格子的底图，整张网格同源，读第一格就够
        private Sprite m_cellSprite;

        // 详情区挂在整块背景上，两行文本与它报的是哪一件
        private RectTransform m_backgroundRect;
        private RectTransform m_detailRect;
        private TMP_Text m_detailNameText;
        private TMP_Text m_detailDescText;
        private BackpackItemInstance m_detailTarget;
        private readonly List<BackpackBuff> m_detailBuffs = new();

        // ==================== 生命周期 ====================
        protected override void OnInit()
        {
            m_coinsText = FindComponent<TMP_Text>(COINS_PATH);
            m_targetText = FindComponent<TMP_Text>(TARGET_PATH);
            m_divesText = FindComponent<TMP_Text>(DIVES_PATH);

            RectTransform gridRect = FindComponent<RectTransform>(GRID_PATH);
            m_grid = gridRect.GetComponent<GridLayoutGroup>();
            m_slots = gridRect.GetComponentsInChildren<UIShopSlotItem>();
            m_storageSlots = FindComponent<RectTransform>(STORAGE_PATH).GetComponentsInChildren<UIShopSlotItem>();
            m_itemsRect = FindComponent<RectTransform>(ITEMS_PATH);
            m_itemTemplate = FindComponent<UIShopItemView>(ITEM_TEMPLATE_PATH);
            m_goodsRect = FindComponent<RectTransform>(GOODS_PATH);
            m_goodsTemplate = FindComponent<UIShopGoodsItem>(GOODS_TEMPLATE_PATH);
            m_refreshButton = FindComponent<Button>(REFRESH_PATH);
            m_diveButton = FindComponent<Button>(DIVE_PATH);
            m_closeButton = FindComponent<Button>(CLOSE_PATH);

            // 布层与物品层都挂在背包面板下，再往上一层就是整块背景，详情区挂它才卡得进面板缝里
            m_backgroundRect = m_itemsRect.transform.parent.parent as RectTransform;

#if UNITY_EDITOR
            if (m_grid == null)
                QLog.Error($"网格上没有 GridLayoutGroup，读不到单格尺寸：{GRID_PATH}");
            if (m_slots.Length == 0)
                QLog.Error($"背包网格里没有格子，请检查 {GRID_PATH}");
            if (m_storageSlots.Length == 0)
                QLog.Error($"仓库网格里没有格子，请检查 {STORAGE_PATH}");
            if (PlateSelectSprite == null)
                QLog.Error($"没有指定 {nameof(PlateSelectSprite)}，选中一块布不会有外框");
            if (m_backgroundRect == null)
                QLog.Error("背景节点没找到，详情区无处可挂");
#endif

            m_cellSize = m_grid.cellSize.x;
            m_cellSpacing = m_grid.spacing.x;

            m_cellSprite = m_slots.Length > 0 ? m_slots[0].EmptyCellSprite : null;

            // 模板本身不上架，留着当复制源，否则布局里会多出一块空位
            m_itemTemplate.gameObject.SetActive(false);
            m_goodsTemplate.gameObject.SetActive(false);

            m_refreshButton.onClick.AddListener(handleRefreshClicked);
            m_diveButton.onClick.AddListener(handleLeaveShopClicked);
            m_closeButton.onClick.AddListener(handleLeaveShopClicked);

            createClothLayer();
            createSelectLayer();
            createPlateBar();
            createDetailPanel();
        }

        protected override void OnOpen(params object[] args)
        {
            m_run = args.Length > 0 ? args[0] as RunState : null;
            m_backpack = args.Length > 1 ? args[1] as RunBackpack : null;
            m_shop = args.Length > 2 ? args[2] as ShopService : null;

#if UNITY_EDITOR
            if (m_run == null)
                QLog.Error("没有传入 RunState，面板留空");
            if (m_backpack == null)
                QLog.Error("没有传入 RunBackpack，背包与仓库不会填充");
            if (m_shop == null)
                QLog.Error("没有传入 ShopService，货架留空");
#endif

            if (m_run == null)
                return;

            // 重开面板不该带着上一眼的选中态走，先把布层的选择清干净
            m_selectedPlate = null;
            m_plateBar.gameObject.SetActive(false);
            m_plateInHand = null;
            refreshSelectionFrames();
            clearDetail();

            refreshAll();
        }

        // ==================== Public Function ====================
        // 宿主手势入口，转给背包模型校验，成不成都要重铺，让落点歪掉的物品弹回原位
        public bool TryRotate(BackpackItemInstance item)
        {
            if (m_backpack == null || item == null)
                return false;

            bool moved = m_backpack.Model.TryRotate(item);
            rebuildItemViews();
            refreshStorage();
            return moved;
        }

        public bool TryMove(BackpackItemInstance item, int anchorColumn, int anchorRow)
        {
            if (m_backpack == null || item == null)
                return false;

            bool moved = m_backpack.Model.TryMove(item, anchorColumn, anchorRow);
            rebuildItemViews();
            refreshStorage();
            return moved;
        }

        public bool TryRecallFromStash(int stashIndex)
        {
            if (m_backpack == null)
                return false;

            bool recalled = m_backpack.TryRecallFromStash(stashIndex);
            if (recalled)
                refreshBackpack();
            return recalled;
        }

        // 布的落点同样交给模型裁决，成不成都重铺：摆歪的布弹回原位，被挤掉的装备也在这一步进架
        public bool TryMovePlate(BackpackItemInstance plate, int anchorColumn, int anchorRow)
        {
            if (m_backpack == null || plate == null)
                return false;

            bool moved = m_backpack.TryMovePlate(plate, anchorColumn, anchorRow);
            refreshBackpack();
            return moved;
        }

        public bool TryRotatePlate(BackpackItemInstance plate)
        {
            if (m_backpack == null || plate == null)
                return false;

            bool rotated = m_backpack.TryRotatePlate(plate);
            refreshBackpack();
            return rotated;
        }

        // 拆进暂存架的布既不铺格也不给 BUFF，取回来靠暂存架那套入口
        public bool TryStashPlate(BackpackItemInstance plate)
        {
            if (m_backpack == null || plate == null)
                return false;

            bool stashed = m_backpack.TryRemovePlateToStash(plate);
            if (stashed)
                m_selectedPlate = null;

            refreshBackpack();
            return stashed;
        }

        // 卖布先拆后收钱：拆不动就一分不收，不会出现钱到手了布还在格上
        public bool TrySellPlate(BackpackItemInstance plate)
        {
            if (m_backpack == null || plate == null)
                return false;

            if (m_shop == null)
            {
                QLog.Error("没有传入 ShopService，布算不出回收价");
                return false;
            }

            int price = m_shop.SellPriceOf(plate.Definition);
            if (!m_backpack.TryRemovePlate(plate))
            {
                QLog.Info("这块布上压着装备，暂存架又接不住，先把装备挪开再卖");
                return false;
            }

            m_run.AddCoins(price);
            m_selectedPlate = null;
            QLog.Info($"{UIShopGoodsName.Of(plate.Definition, plate.Tier)} 已卖出，回收 {price} 金币");
            refreshBackpack();
            refreshCounters();
            return true;
        }

        // 点同一块布是取消选中，点另一块是换选中，选中态只存在面板这一份
        public void SelectPlate(BackpackItemInstance plate)
        {
            bool cancel = plate == null || m_selectedPlate == plate;
            m_selectedPlate = cancel ? null : plate;

            if (cancel)
                clearDetail();
            else
                ShowDetail(plate);

            rebuildPlateViews();
            refreshActionBar();
        }

        // 布到手上了只重染格子：整盘重铺会把正在拖的那块视图掀回原位，拖拽当场就废了
        public void BeginPlateDrag(BackpackItemInstance plate)
        {
            if (plate == null || m_backpack == null)
                return;

            m_plateInHand = plate;
            // 布已经离地，压在它下面的操作条跟着收起来，免得拖的时候满屏都是控件
            m_plateBar.gameObject.SetActive(false);
            fillSlotColors();
        }

        // 松手之前先解除手持，格子才有机会按布落下之后的样子重染
        public void EndPlateDrag(BackpackItemInstance plate)
        {
            if (m_plateInHand == null)
                return;

            if (plate != null && m_plateInHand != plate)
                return;

            m_plateInHand = null;
            // 操作条不用在这里补回来：落下之后一定走 TryMovePlate 的重铺，那时会自己浮回来
            fillSlotColors();
        }

        // 点中的是哪件东西，两行写在详情区：第一行名字，第二行它身上的能力线
        public void ShowDetail(BackpackItemInstance item)
        {
            if (item?.Definition == null)
            {
                clearDetail();
                return;
            }

            m_detailTarget = item;
            m_detailBuffs.Clear();
            item.Definition.FillBuffs(item.Tier, m_detailBuffs);

            m_detailRect.gameObject.SetActive(true);
            m_detailNameText.text = UIShopGoodsName.Of(item.Definition, item.Tier);
            m_detailDescText.text = UIShopBuffText.OfMany(m_detailBuffs);
        }

        // ==================== Private Function ====================
        // 一次全量重铺，买卖与刷新这种动了库存又动了背包的操作才用
        private void refreshAll()
        {
            refreshCounters();
            rebuildGoods();
            refreshBackpack();
        }

        // 背包与暂存架是一对：上格放不下会掉进暂存架，取回又会占掉网格
        private void refreshBackpack()
        {
            if (m_backpack == null)
                return;

            fillBackpack();
            fillStorage();
            refreshActionBar();
            validateDetail();
        }

        private void refreshStorage()
        {
            if (m_backpack != null)
                fillStorage();
        }

        private void refreshCounters()
        {
            m_coinsText.text = $"金币 {m_run.Coins}";
            m_targetText.text = $"目标 {m_run.RoundValue} / {m_run.RoundTarget}";
            m_divesText.text = $"下潜 {m_run.DivesRemaining} 次";
        }

        private void fillBackpack()
        {
            fillSlotColors();
            rebuildPlateViews();
            rebuildItemViews();
        }

        // 每一格是布还是锁格，问模型这块格归哪块布，布的颜色就是格子的底色
        private void fillSlotColors()
        {
            BackpackModel model = m_backpack.Model;
            int columns = m_grid.constraintCount;

#if UNITY_EDITOR
            if (m_slots.Length != columns * model.Rows)
                QLog.Error($"格子数与配置不符：预制体 {m_slots.Length} 格，配置 {columns}×{model.Rows}");
#endif

            for (int i = 0; i < m_slots.Length; i++)
            {
                int row = i / columns;
                int column = i % columns;

                if (!model.IsCellUnlocked(column, row))
                {
                    m_slots[i].SetLocked();
                    continue;
                }

                BackpackItemInstance plate = model.FindPlateAt(column, row);

                // 布被抓到手上，它铺出来的那几格连底板都没了，跟没铺过一样显示成锁格
                if (plate == m_plateInHand)
                {
                    m_slots[i].SetLocked();
                    continue;
                }

                if (plate != null)
                    m_slots[i].SetCloth(plate.Definition.TintColor);
                else
                    m_slots[i].SetEmpty();
            }
        }

        // 网格上的物品全部来自模型，一件一个视图，形状格与颜色由视图自己问实例
        private void rebuildItemViews()
        {
            if (m_backpack == null)
                return;

            IReadOnlyList<BackpackItemInstance> items = m_backpack.Model.Items;
            m_itemsShown = 0;

            for (int i = 0; i < items.Count; i++)
                showItem(items[i]);

            for (int i = m_itemsShown; i < m_itemViews.Count; i++)
                m_itemViews[i].gameObject.SetActive(false);
        }

        // 暂存架按件显示，件数之外的格子留空，多出来的件数说明配置和预制体没对齐
        private void fillStorage()
        {
            IReadOnlyList<BackpackItemInstance> stash = m_backpack.Stash;

#if UNITY_EDITOR
            if (stash.Count > m_storageSlots.Length)
                QLog.Error($"暂存 {stash.Count} 件超过仓库格数 {m_storageSlots.Length}，多出来的没有格子显示");
#endif

            for (int i = 0; i < m_storageSlots.Length; i++)
            {
                if (i < stash.Count)
                    m_storageSlots[i].SetStored(UIShopGoodsName.Of(stash[i].Definition, stash[i].Tier),
                                                UIShopQuality.ColorFor(stash[i].Definition, stash[i].Tier), i, this);
                else
                    m_storageSlots[i].SetEmpty();
            }
        }

        private void showItem(BackpackItemInstance item)
        {
            while (m_itemViews.Count <= m_itemsShown)
                m_itemViews.Add(Instantiate(m_itemTemplate, m_itemsRect));

            UIShopItemView view = m_itemViews[m_itemsShown];
            view.gameObject.SetActive(true);
            view.Setup(item, m_cellSize, m_cellSpacing, this);
            m_itemsShown++;
        }

        // 布层是运行时建的一层底板，位置尺寸抄自 Layer_Items，两层共用同一套格子坐标
        private void createClothLayer()
        {
#if UNITY_EDITOR
            if (m_itemsRect.parent == null)
                QLog.Error($"物品层 {ITEMS_PATH} 没有父节点，布层无处可挂");
#endif

            RectTransform layer = new GameObject(CLOTH_LAYER_NAME, typeof(RectTransform)).GetComponent<RectTransform>();
            layer.SetParent(m_itemsRect.parent, false);
            layer.anchorMin = new Vector2(0.5f, 1f);
            layer.anchorMax = new Vector2(0.5f, 1f);
            layer.pivot = new Vector2(0.5f, 1f);
            layer.sizeDelta = m_itemsRect.sizeDelta;
            layer.anchoredPosition = m_itemsRect.anchoredPosition;

            // 排到物品层前面，UI 按兄弟序号后画的压在上面，布自然垫在装备底下
            layer.SetSiblingIndex(m_itemsRect.GetSiblingIndex());
            m_clothsRect = layer;
        }

        // 选中框层抄布层与物品层同一套 rect，排在物品层之后，框才不会被装备压住
        private void createSelectLayer()
        {
            RectTransform layer = new GameObject(SELECT_LAYER_NAME, typeof(RectTransform)).GetComponent<RectTransform>();
            layer.SetParent(m_itemsRect.parent, false);
            layer.anchorMin = new Vector2(0.5f, 1f);
            layer.anchorMax = new Vector2(0.5f, 1f);
            layer.pivot = new Vector2(0.5f, 1f);
            layer.sizeDelta = m_itemsRect.sizeDelta;
            layer.anchoredPosition = m_itemsRect.anchoredPosition;

            // 排到物品层后面，UI 按兄弟序号后画的压在上面，框才浮在布与装备之上
            layer.SetSiblingIndex(m_itemsRect.GetSiblingIndex() + 1);
            m_selectRect = layer;
        }

        // 选中框只跟着当前选中的布走，一格一圈角标，没选中就把整层收干净
        private void refreshSelectionFrames()
        {
            float pitch = m_cellSize + m_cellSpacing;
            m_selectFramesUsed = 0;

            if (m_selectedPlate != null && m_backpack != null)
            {
                for (int i = 0; i < m_plateCells.Count; i++)
                {
                    Vector2Int cell = m_plateCells[i];
                    showSelectionFrame(cell.x, cell.y, pitch);
                }
            }

            for (int i = m_selectFramesUsed; i < m_selectFrames.Count; i++)
                m_selectFrames[i].gameObject.SetActive(false);
        }

        // 框的位置与布层那一格完全对齐，尺寸贴着格子本身，不越界到相邻格
        private void showSelectionFrame(int column, int row, float pitch)
        {
            while (m_selectFrames.Count <= m_selectFramesUsed)
                createSelectFrame();

            Image frame = m_selectFrames[m_selectFramesUsed];
            frame.gameObject.SetActive(true);
            frame.rectTransform.anchoredPosition = new Vector2(column * pitch, -row * pitch);
            frame.rectTransform.sizeDelta = new Vector2(m_cellSize, m_cellSize);
            m_selectFramesUsed++;
        }

        private void createSelectFrame()
        {
            GameObject frameObject = new GameObject(SELECT_FRAME_NAME, typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)frameObject.transform;
            rect.SetParent(m_selectRect, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);

            Image frame = rect.GetComponent<Image>();
            frame.sprite = PlateSelectSprite;
            frame.type = Image.Type.Simple;
            frame.raycastTarget = false;
            m_selectFrames.Add(frame);
        }

        // 操作条挂在物品层里，平时收着，只有选中一块布才浮出来
        private void createPlateBar()
        {
            float barHeight = m_cellSize * BAR_HEIGHT_RATIO;
            float barWidth = BAR_PADDING * 2f + BAR_BUTTON_WIDTH * 3f + BAR_BUTTON_SPACING * 2f;

            GameObject barObject = new GameObject(PLATE_BAR_NAME, typeof(RectTransform), typeof(Image));
            Image backdrop = barObject.GetComponent<Image>();
            backdrop.color = new Color32(0x2A, 0x1F, 0x14, 0xF0);

            m_plateBar = barObject.GetComponent<RectTransform>();
            m_plateBar.SetParent(m_itemsRect, false);
            m_plateBar.anchorMin = new Vector2(0f, 1f);
            m_plateBar.anchorMax = new Vector2(0f, 1f);
            m_plateBar.pivot = new Vector2(0f, 1f);
            m_plateBar.sizeDelta = new Vector2(barWidth, barHeight);

            // 条上的空白也接点击，点一下就取消选中，不然玩家得去点别的布才能取消
            Button clearButton = barObject.AddComponent<Button>();
            clearButton.targetGraphic = backdrop;
            clearButton.onClick.AddListener(handleClearPlateSelection);

            m_plateRotateButton = createPlateBarButton(PLATE_ROTATE_NAME, "旋转", 0, out _);
            m_plateStashButton = createPlateBarButton(PLATE_STASH_NAME, "拆下", 1, out _);
            m_plateSellButton = createPlateBarButton(PLATE_SELL_NAME, "出售", 2, out m_plateSellText);
            m_plateBar.gameObject.SetActive(false);
        }

        // 条上的按钮也由代码生成：底图加一个居中文字，位置按序号推，字体抄自金币文本，免得中文成豆腐块
        private Button createPlateBarButton(string name, string label, int index, out TMP_Text buttonText)
        {
            float barHeight = m_plateBar.sizeDelta.y;
            float buttonHeight = barHeight * BAR_BUTTON_HEIGHT_RATIO;

            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(m_plateBar, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(BAR_BUTTON_WIDTH, buttonHeight);
            rect.anchoredPosition = new Vector2(BAR_PADDING + index * (BAR_BUTTON_WIDTH + BAR_BUTTON_SPACING),
                -(barHeight - buttonHeight) * 0.5f);

            Image background = buttonObject.GetComponent<Image>();
            background.color = new Color32(0x7A, 0x5F, 0x3A, 0xFF);

            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = m_coinsText.font;
            text.fontSize = BAR_FONT_SIZE;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            text.text = label;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;
            buttonText = text;
            return button;
        }

        // 详情区抄货架面板那张底图与金币文本的字体字色，版式再改也不用回来动这里
        // 位置卡在货架面板底边与底部按钮顶边中间那条空隙，整块背景下没有第二处空位
        private void createDetailPanel()
        {
            Image shopBackdrop = FindComponent<Image>(SHOP_PANEL_PATH);

            GameObject panelObject = new GameObject(DETAIL_PANEL_NAME, typeof(RectTransform), typeof(Image));
            Image backdrop = panelObject.GetComponent<Image>();
            backdrop.sprite = shopBackdrop.sprite;
            backdrop.type = shopBackdrop.type;
            backdrop.color = shopBackdrop.color;
            // 详情区只是给人看的，别把点击挡回去，格子该拖还得能拖
            backdrop.raycastTarget = false;

            m_detailRect = (RectTransform)panelObject.transform;
            m_detailRect.SetParent(m_backgroundRect, false);
            m_detailRect.anchorMin = new Vector2(0.5f, 1f);
            m_detailRect.anchorMax = new Vector2(0.5f, 1f);
            m_detailRect.pivot = new Vector2(0.5f, 1f);
            m_detailRect.anchoredPosition = new Vector2(0f, DETAIL_TOP);
            m_detailRect.sizeDelta = new Vector2(DETAIL_WIDTH, DETAIL_HEIGHT);

            m_detailNameText = createDetailText(DETAIL_NAME_TEXT_NAME, DETAIL_NAME_FONT_SIZE,
                DETAIL_NAME_TOP, DETAIL_NAME_HEIGHT);
            m_detailDescText = createDetailText(DETAIL_DESC_TEXT_NAME, DETAIL_DESC_FONT_SIZE,
                DETAIL_DESC_TOP, DETAIL_DESC_HEIGHT);

            m_detailRect.gameObject.SetActive(false);
        }

        // 详情两行都是横向拉满、左边留内边距，字宽随面板一起走，长文案省略号收尾
        private TMP_Text createDetailText(string name, float fontSize, float top, float height)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = (RectTransform)textObject.transform;
            rect.SetParent(m_detailRect, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(DETAIL_SIDE_PADDING, top);
            // 横向贴着面板拉满，左右各留一份内边距
            rect.sizeDelta = new Vector2(-DETAIL_SIDE_PADDING * 2f, height);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = m_coinsText.font;
            text.color = m_coinsText.color;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private void clearDetail()
        {
            m_detailTarget = null;
            m_detailRect.gameObject.SetActive(false);
        }

        // 卖掉、拆走、被挤进暂存架之后，详情里那件东西已经不在网格上了，不能留一张空壳文案
        private void validateDetail()
        {
            if (m_detailTarget == null || m_backpack == null)
                return;

            if (m_backpack.Model.Items.Contains(m_detailTarget) ||
                m_backpack.Model.Plates.Contains(m_detailTarget))
                return;

            clearDetail();
        }

        // 布层重铺，模型里有几块布就铺几个视图，多出来的藏起来
        private void rebuildPlateViews()
        {
            if (m_backpack == null)
                return;

            IReadOnlyList<BackpackItemInstance> plates = m_backpack.Model.Plates;
            m_platesShown = 0;

            for (int i = 0; i < plates.Count; i++)
                showPlate(plates[i]);

            for (int i = m_platesShown; i < m_plateViews.Count; i++)
                m_plateViews[i].gameObject.SetActive(false);
        }

        private void showPlate(BackpackItemInstance plate)
        {
            while (m_plateViews.Count <= m_platesShown)
                createPlateView();

            UIShopPlateView view = m_plateViews[m_platesShown];
            view.gameObject.SetActive(true);
            view.Setup(plate, m_cellSize, m_cellSpacing, this, m_cellSprite);
            m_platesShown++;
        }

        private void createPlateView()
        {
            GameObject plateObject = new GameObject(PLATE_VIEW_NAME, typeof(RectTransform));
            plateObject.transform.SetParent(m_clothsRect, false);
            m_plateViews.Add(plateObject.AddComponent<UIShopPlateView>());
        }

        // 操作条跟着选中的布走：放得下就压在布上边，贴到顶了就挪到布下边
        private void refreshActionBar()
        {
            if (m_selectedPlate == null || m_backpack == null || !m_backpack.Model.Plates.Contains(m_selectedPlate))
            {
                m_selectedPlate = null;
                m_plateBar.gameObject.SetActive(false);
                refreshSelectionFrames();
                return;
            }

            BackpackItemInstance plate = m_selectedPlate;
            m_plateCells.Clear();
            plate.FillWorldCells(m_plateCells);

            int minColumn = int.MaxValue;
            int minRow = int.MaxValue;
            int maxRow = int.MinValue;

            for (int i = 0; i < m_plateCells.Count; i++)
            {
                Vector2Int cell = m_plateCells[i];
                minColumn = Mathf.Min(minColumn, cell.x);
                minRow = Mathf.Min(minRow, cell.y);
                maxRow = Mathf.Max(maxRow, cell.y);
            }

            float pitch = m_cellSize + m_cellSpacing;
            float barWidth = m_plateBar.sizeDelta.x;
            float barHeight = m_plateBar.sizeDelta.y;
            float gridWidth = Columns * pitch - m_cellSpacing;
            float gridHeight = Rows * pitch - m_cellSpacing;

            // 两层都以网格左上角为原点，往下的距离写成负的 anchoredPosition
            float topEdge = -minRow * pitch;
            float bottomEdge = -((maxRow + 1) * pitch - m_cellSpacing);
            // 条的 pivot 在左上角，落在布上方要把整条高度加回来
            float above = topEdge + BAR_GAP + barHeight;
            float below = bottomEdge - BAR_GAP;

            m_plateBar.anchoredPosition = new Vector2(
                Mathf.Clamp(minColumn * pitch, 0f, Mathf.Max(0f, gridWidth - barWidth)),
                Mathf.Clamp(minRow > 0 ? above : below, barHeight - gridHeight, 0f));

            m_plateRotateButton.interactable = plate.CanRotate;

            int sellPrice = m_shop != null ? m_shop.SellPriceOf(plate.Definition) : 0;
            m_plateSellText.text = sellPrice > 0 ? $"卖 {sellPrice}" : "不回收";
            m_plateSellButton.interactable = sellPrice > 0;

            m_plateBar.gameObject.SetActive(true);
            // 重铺会新插物品视图，条子每次都要重新压到最上面才不会被盖住
            m_plateBar.SetAsLastSibling();

            refreshSelectionFrames();
        }

        // 三个按钮都只做一件事：把当前选中的布交给宿主的方法，成不成都由它重铺
        private void handleRotatePlateClicked()
        {
            TryRotatePlate(m_selectedPlate);
        }

        private void handleStashPlateClicked()
        {
            TryStashPlate(m_selectedPlate);
        }

        private void handleSellPlateClicked()
        {
            TrySellPlate(m_selectedPlate);
        }

        private void handleClearPlateSelection()
        {
            SelectPlate(null);
        }

        // 货架来自服务的库存，卡片顺序与库存顺序一致
        private void rebuildGoods()
        {
            m_goodsShown = 0;

            if (m_shop != null)
            {
                IReadOnlyList<ShopStockEntry> stock = m_shop.Stock;

                for (int i = 0; i < stock.Count; i++)
                    showGoods(stock[i], m_run.Coins >= stock[i].Price);
            }

            for (int i = m_goodsShown; i < m_goodsItems.Count; i++)
                m_goodsItems[i].gameObject.SetActive(false);
        }

        private void showGoods(ShopStockEntry entry, bool affordable)
        {
            while (m_goodsItems.Count <= m_goodsShown)
                m_goodsItems.Add(Instantiate(m_goodsTemplate, m_goodsRect));

            UIShopGoodsItem goods = m_goodsItems[m_goodsShown];
            goods.gameObject.SetActive(true);
            goods.Setup(entry, UIShopQuality.ColorFor(entry.Definition, entry.Tier), affordable, handleBuyClicked);
            m_goodsShown++;
        }

        // 买成与否则全由服务决定，面板只管结果对应的界面反应
        private void handleBuyClicked(ShopStockEntry entry)
        {
            if (m_shop == null)
                return;

            int stashBefore = m_backpack != null ? m_backpack.Stash.Count : 0;
            ShopBuyResult result = m_shop.TryBuy(entry, m_run, m_backpack);

            switch (result)
            {
                case ShopBuyResult.Success:
                    // 网格放不下时东西会落进暂存架，而暂存中的板块不产生 BUFF，得让玩家看见
                    if (m_backpack.Stash.Count > stashBefore)
                        QLog.Info($"{UIShopGoodsName.Of(entry.Definition, entry.Tier)} 网格放不下，已进暂存架，暂存中的板块不生效");
                    refreshAll();
                    break;
                case ShopBuyResult.NotEnoughCoins:
                    QLog.Warning($"金币不够，还差 {entry.Price - m_run.Coins}");
                    break;
                // 装不下时钱已原额退回，金币得跟着刷回去
                case ShopBuyResult.BackpackFull:
                    QLog.Info("网格与暂存架都满了，买不进");
                    refreshCounters();
                    break;
                default:
                    break;
            }
        }

        private void handleRefreshClicked()
        {
            if (m_shop == null)
                return;

            if (!m_shop.TryRefresh(m_run))
            {
                QLog.Info($"刷新要 {m_shop.RefreshCost} 金币，钱不够");
                return;
            }

            refreshAll();
        }

        // 商店阶段唯一的去处就是下潜，两个按钮同义
        // 面板不自己决定下一阶段是谁，只喊一声「离开商店」，切阶段交给控制器
        private void handleLeaveShopClicked()
        {
            EventManager.PublishEvent(EventDefines.ShopExitRequested);
        }
    }
}
