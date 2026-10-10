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
* 选中一块布或一件装备才浮出操作条，旋转、拆下架、卖成金币都在条上按，单击不直接改东西
* 背包与暂存架双向拖：拖到暂存架面板上就下架，从暂存格拖进网格就按幽灵吸附的落点上架
* 格子上不再常驻名称，点中布或装备都在它旁边浮出详情小窗：第一行名字，第二行能力线
* 详情小窗是独立预制体 UIShopDetailWindow，脚本只实例化一次并按选中物染色，版式全在预制体里改
* 商品卡走同一套幽灵与同一扇小窗：点开只看详情，拖到网格的空位或拖进暂存架那一刻才扣钱
* 商品的详情比已入手的多一个右上角价钱，是布还要在窗里画出它真实的异形，小窗跟着长高
* 拖一块布起来时它铺过的格子先空成锁格，手里那块布由视图的幽灵格跟着手指画
* 布与装备一出网格就压在仓库与商店底下，拖起来期间要把整块背包面板提到最后，松手放回原位
* 单格边长与间距从网格组件读，改版式只动预制体不动脚本
* 物品视图与商品卡由模板复制并留用，只开关不销毁，销毁会延到帧尾被布局算进内容高度
* 装备被布挤下网格就自己飞进暂存架那一格，飞完格子才显形，玩家看得见它去了哪
* 面板不决定流程走向，按钮只广播「离开商店」，切阶段由控制器负责
*/

using System.Collections;
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
        private const string STORAGE_PANEL_PATH = "Bg/Panel_Storage";
        private const string STORAGE_PATH = STORAGE_PANEL_PATH + "/Grid_Storage";
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

        // 详情小窗是独立预制体，运行时只实例化一次，这里只认它内部的三个子节点
        private const string DETAIL_WINDOW_NAME = "Panel_Detail";
        private const string DETAIL_NAME_TEXT_PATH = "Txt_DetailName";
        private const string DETAIL_DESC_TEXT_PATH = "Txt_DetailDesc";
        private const string DETAIL_ACCENT_PATH = "Img_Accent";
        // 商品详情比已入手的多一个右上角价钱与一条布形预览，这两个节点只在小窗预制体里排
        private const string DETAIL_PRICE_PATH = "Txt_DetailPrice";
        private const string DETAIL_CLOTH_SHAPE_PATH = "Node_ClothShape";
        // 宽跟着两行文案走，两头各封一个界，省得长文案把小窗拉成一条横幅
        private const float DETAIL_MIN_WIDTH = 340f;
        private const float DETAIL_MAX_WIDTH = 520f;
        private const float DETAIL_GAP = 14f;
        private const float DETAIL_SAFE_MARGIN = 20f;
        // 两行文本离左右缘的留白，抄自预制体里的文本边距，改版式要两边一起动
        private const float DETAIL_TEXT_INSET_LEFT = 38f;
        private const float DETAIL_TEXT_INSET_RIGHT = 16f;
        // 候选摆位一共有六个，试完都不合适才退回收夹
        private const int DETAIL_CANDIDATE_COUNT = 6;
        // 价钱压在右上角，名字行要给它让出这几像素，两行才不叠字
        private const float DETAIL_PRICE_INSET_RIGHT = 16f;
        private const float DETAIL_PRICE_GAP = 12f;
        // 窗里多一条布形预览就得多长出这么多，长出来的量跟着预览带的高走
        private const float DETAIL_PREVIEW_EXTRA = 38f;

        // 操作条的高取单格的不到一半，宽由三个按钮加内边距凑出来，横排自己排
        private const float BAR_HEIGHT_RATIO = 0.48f;
        private const float BAR_BUTTON_HEIGHT_RATIO = 0.74f;
        private const float BAR_BUTTON_WIDTH = 150f;
        private const float BAR_BUTTON_SPACING = 8f;
        private const float BAR_PADDING = 10f;
        private const float BAR_GAP = 10f;
        private const float BAR_FONT_SIZE = 24f;

        // 装备飞进暂存架的行程时长，纯表现，落点判定早在模型里定死了，这里只补一段看得见的过程
        private const float FLY_DURATION = 0.28f;

        // 飞行物借用的临时层，面板里没有这个节点
        private const string FLY_LAYER_NAME = "Layer_FlyStash";

        // ==================== 字段[外部设置] ====================
        [Tooltip("选中一块布时套在每一格上的外框图，取 Button_select_brown 这种四角标，中心要留空")]
        [SerializeField] private Sprite PlateSelectSprite;

        [Tooltip("点中布或装备时浮在旁边的详情小窗预制体，两行文本与左侧色条都在预制体里排")]
        [SerializeField] private GameObject DetailWindowPrefab;

        // ==================== 属性 ====================
        public int Columns => m_backpack != null ? m_backpack.Model.Columns : 0;

        public int Rows => m_backpack != null ? m_backpack.Model.Rows : 0;

        // 幽灵格与落点换算都以物品层为准，它的左上角正是网格第 0 格的左上角
        public RectTransform GhostLayer => m_itemsRect;

        public float CellPitch => m_cellSize + m_cellSpacing;

        public Sprite GhostCellSprite => m_cellSprite;

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
        private TMP_Text m_plateStashText;
        private TMP_Text m_plateSellText;

        // 选中态只有面板这一份，布与装备共用同一套外框与操作条，拆走卖掉之后由面板清空
        private BackpackItemInstance m_selectedPlate;
        private BackpackItemInstance m_selectedItem;

        // 量当前选中物的外框用，操作条定位与逐格选中外框都靠它
        private readonly List<Vector2Int> m_selectedCells = new();

        // 暂存架那块面板是拖拽下架的落点，只判指针在不在它的矩形里
        private RectTransform m_storagePanelRect;

        // 装备被挤下网格要飞进架子，起飞那一刻的中心、尺寸与转角得在模型动手之前抄下来，事后视图就被重铺了
        private readonly Dictionary<BackpackItemInstance, Vector3> m_flyCenters = new();
        private readonly Dictionary<BackpackItemInstance, Vector2> m_flySizes = new();
        private readonly Dictionary<BackpackItemInstance, float> m_flyAngles = new();

        // 还在飞的这几件，架子上的那一格先留空，落地才显形，免得东西没到架子就先亮
        private readonly HashSet<BackpackItemInstance> m_flyingIn = new();
        private int m_flyingCount;

        // 飞行物挂在这一层里，它盖在背包与仓库之上才看得见在飞
        private RectTransform m_flyRect;

        // 模型动手之前架子上的件数，动手之后多出来的这几件就是刚落架的
        private int m_stashCountBefore;

        // 量矩形中心用的四个角，全程复用同一个数组，免得每件都分配一次
        private readonly Vector3[] m_flyCorners = new Vector3[4];

        // 被抓到手上的那块布，它铺过的格子在这期间显示成锁格，因为底板确实跟着手指走了
        private BackpackItemInstance m_plateInHand;

        // 背包面板整块，拖起来的东西要压过仓库与商店，只能连它一起往上提
        private RectTransform m_backpackPanelRect;

        // 提层之前的兄弟序号，-1 表示当前没提着，松手按它放回原位
        private int m_backpackPanelIndex = -1;

        // 幽灵格要抄底层格子的底图，整张网格同源，读第一格就够
        private Sprite m_cellSprite;

        // 详情小窗挂在整块背景上，两行文本、左侧色条与它报的是哪一件
        private RectTransform m_backgroundRect;
        private RectTransform m_detailRect;
        private TMP_Text m_detailNameText;
        private TMP_Text m_detailDescText;
        private Image m_detailAccent;
        private BackpackItemInstance m_detailTarget;
        private readonly List<BackpackBuff> m_detailBuffs = new();
        // 小窗的高读自预制体，算落点时用它，脚本里不再写死
        private float m_detailHeight;

        // 商品的详情多一个右上角价钱与一条布形预览，窗子按有没有这两样来长
        private TMP_Text m_detailPriceText;
        private Color m_detailPriceDefaultColor;
        private UIShopClothShape m_detailClothShape;
        private ShopStockEntry m_detailGoods;
        private RectTransform m_detailCardRect;
        // 预制体不带预览带时的高，收起预览要还原到它
        private float m_detailBaseHeight;

        // 量商品卡四角用的数组，逐帧重摆也不反复分配
        private readonly Vector3[] m_detailCorners = new Vector3[4];

        // 正被抓在手上的那件商品，预览按「买不买得起它」整块染色，松手没落到位置就什么也不扣
        private ShopStockEntry m_pendingPurchase;

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
            m_storagePanelRect = FindComponent<RectTransform>(STORAGE_PANEL_PATH);
            m_itemsRect = FindComponent<RectTransform>(ITEMS_PATH);
            m_itemTemplate = FindComponent<UIShopItemView>(ITEM_TEMPLATE_PATH);
            m_goodsRect = FindComponent<RectTransform>(GOODS_PATH);
            m_goodsTemplate = FindComponent<UIShopGoodsItem>(GOODS_TEMPLATE_PATH);
            m_refreshButton = FindComponent<Button>(REFRESH_PATH);
            m_diveButton = FindComponent<Button>(DIVE_PATH);
            m_closeButton = FindComponent<Button>(CLOSE_PATH);

            // 布层与物品层都挂在背包面板下，再往上一层就是整块背景，小窗挂它才能越过网格的范围摆
            m_backgroundRect = m_itemsRect.transform.parent.parent as RectTransform;
            m_backpackPanelRect = m_itemsRect.transform.parent as RectTransform;

#if UNITY_EDITOR
            if (m_grid == null)
                QLog.Error($"网格上没有 GridLayoutGroup，读不到单格尺寸：{GRID_PATH}");
            if (m_slots.Length == 0)
                QLog.Error($"背包网格里没有格子，请检查 {GRID_PATH}");
            if (m_storageSlots.Length == 0)
                QLog.Error($"仓库网格里没有格子，请检查 {STORAGE_PATH}");
            if (m_storagePanelRect == null)
                QLog.Error($"暂存架面板没找到，拖不进架子：{STORAGE_PANEL_PATH}");
            if (PlateSelectSprite == null)
                QLog.Error($"没有指定 {nameof(PlateSelectSprite)}，选中一块布不会有外框");
            if (DetailWindowPrefab == null)
                QLog.Error($"没有指定 {nameof(DetailWindowPrefab)}，点中布或装备不会有详情小窗");
            if (m_backgroundRect == null)
                QLog.Error("背景节点没找到，详情小窗无处可挂");
            if (m_backpackPanelRect == null)
                QLog.Error("背包面板没找到，拖起来的布与装备压不过仓库与商店");
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
            bindDetailWindow();
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

            // 重开面板不该带着上一眼的选中态走，条子、外框与详情一起清干净
            m_selectedPlate = null;
            m_selectedItem = null;
            m_plateInHand = null;
            // 上一眼可能正拖着商品就关了面板，那笔没成交的买卖要跟着作废
            m_pendingPurchase = null;
            // 上一眼可能正拖着东西就关了面板，提层状态在这里兜底清干净
            restoreBackpackPanel();
            refreshActionBar();

            refreshAll();
        }

        // 面板中途被关掉或整块禁用时 End 系列来不及跑，提层状态在这里自己收干净，不留残留
        private void OnDisable()
        {
            restoreBackpackPanel();
            stopFlights();
        }

        // ==================== Public Function ====================
        // 宿主手势入口，转给背包模型校验，成不成都要重铺，让落点歪掉的物品弹回原位
        public bool TryRotate(BackpackItemInstance item)
        {
            if (m_backpack == null || item == null)
                return false;

            bool moved = m_backpack.Model.TryRotate(item);
            refreshBackpack();
            return moved;
        }

        public bool TryMove(BackpackItemInstance item, int anchorColumn, int anchorRow)
        {
            if (m_backpack == null || item == null)
                return false;

            bool moved = m_backpack.Model.TryMove(item, anchorColumn, anchorRow);
            refreshBackpack();
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

            captureStashLanding();
            bool moved = m_backpack.TryMovePlate(plate, anchorColumn, anchorRow);
            if (moved)
                flyNewStashEntries();

            refreshBackpack();
            return moved;
        }

        public bool TryRotatePlate(BackpackItemInstance plate)
        {
            if (m_backpack == null || plate == null)
                return false;

            captureStashLanding();
            bool rotated = m_backpack.TryRotatePlate(plate);
            if (rotated)
                flyNewStashEntries();

            refreshBackpack();
            return rotated;
        }

        // 拆进暂存架的布既不铺格也不给 BUFF，取回来靠暂存架那套入口
        public bool TryStashPlate(BackpackItemInstance plate)
        {
            if (m_backpack == null || plate == null)
                return false;

            captureStashLanding();
            bool stashed = m_backpack.TryRemovePlateToStash(plate);
            if (stashed)
            {
                m_selectedPlate = null;
                // 布自己也要进架，可它原先不在装备之列，没有出发点，飞的只有被它挤下来的那几件
                flyNewStashEntries();
            }

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
            captureStashLanding();

            if (!m_backpack.TryRemovePlate(plate))
            {
                QLog.Info("这块布上压着装备，暂存架又接不住，先把装备挪开再卖");
                return false;
            }

            flyNewStashEntries();

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
            m_selectedItem = null;

            if (cancel)
                clearDetail();
            else
                ShowDetail(plate);

            refreshActionBar();
        }

        // 装备同样只有一份选中态，点同一件取消，点另一件换人，布跟着让位
        public void SelectItem(BackpackItemInstance item)
        {
            bool cancel = item == null || m_selectedItem == item;
            m_selectedItem = cancel ? null : item;
            m_selectedPlate = null;

            if (cancel)
                clearDetail();
            else
                ShowDetail(item);

            refreshActionBar();
        }

        // 装备到手上了，操作条与详情小窗先收起来，免得拖的时候满屏控件
        public void BeginItemDrag(BackpackItemInstance item)
        {
            raiseBackpackPanel();
            m_plateBar.gameObject.SetActive(false);
            m_detailRect.gameObject.SetActive(false);
        }

        // 手上的装备落下或作废，条子按落点之后的选中态复原
        public void EndItemDrag(BackpackItemInstance item)
        {
            restoreBackpackPanel();
            refreshActionBar();
        }

        // 装备摘进暂存架：架子满了收不动，收不成也要重铺一次把视图弹回原格
        public bool TryStashItem(BackpackItemInstance item)
        {
            if (m_backpack == null || item == null)
                return false;

            captureStashLanding();
            bool stashed = m_backpack.TryRemoveItemToStash(item);
            if (stashed)
            {
                m_selectedItem = null;
                // 从手上摘进架子也补一段飞行，出发点就按松手时它停着的地方算
                flyNewStashEntries();
            }

            refreshBackpack();
            return stashed;
        }

        // 从暂存架按落点摆上网格，摆不成原样留在架上，幽灵随视图重铺一起收掉
        public bool TryPlaceFromStashAt(int stashIndex, int anchorColumn, int anchorRow)
        {
            if (m_backpack == null)
                return false;

            bool placed = m_backpack.TryRecallFromStashAt(stashIndex, anchorColumn, anchorRow);
            refreshBackpack();
            return placed;
        }

        // 暂存架那块面板就是下架落点，指针落在面板矩形里才算
        public bool IsPointerInStash(Vector2 screenPoint)
        {
            if (m_storagePanelRect == null)
                return false;

            return RectTransformUtility.RectangleContainsScreenPoint(m_storagePanelRect, screenPoint, null);
        }

        // 把屏幕坐标换算成网格落点格，与视图铺块用的是同一套坐标，指到网格外返回 false
        public bool TryPointerCell(Vector2 screenPoint, out int anchorColumn, out int anchorRow)
        {
            anchorColumn = 0;
            anchorRow = 0;

            if (m_itemsRect == null || m_backpack == null)
                return false;

            int columns = Columns;
            int rows = Rows;
            if (columns < 1 || rows < 1)
                return false;

            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_itemsRect, screenPoint, null, out local);

            Vector2 grid = local - new Vector2(m_itemsRect.rect.xMin, m_itemsRect.rect.yMax);
            float pitch = m_cellSize + m_cellSpacing;
            int column = Mathf.RoundToInt(grid.x / pitch);
            int row = Mathf.RoundToInt(-grid.y / pitch);

            if (column < 0 || column >= columns || row < 0 || row >= rows)
                return false;

            anchorColumn = column;
            anchorRow = row;
            return true;
        }

        // 拖拽预览逐格问这里：越界、脚下没布、布正被抓在手上、被别的装备压住，四种都算缺格
        // 手上这件自己占的那几格要豁免，否则原地挪一格的瞬间就会把自己染成一片红
        // 布要铺的格恰恰是没布的格，所以布只看「这格有没有别的布压着」，装备在下头不算冲突
        public bool IsPreviewCellValid(BackpackItemInstance dragging, int column, int row)
        {
            BackpackModel model = m_backpack != null ? m_backpack.Model : null;
            if (model == null || dragging?.Definition == null)
                return false;

            if (column < 0 || column >= model.Columns || row < 0 || row >= model.Rows)
                return false;

            // 正拖着商品又买不起：整块判红，反正松手也成交不了，别让红绿看着像白花钱
            if (m_pendingPurchase != null && m_run != null && m_pendingPurchase.Price > m_run.Coins)
                return false;

            BackpackItemInstance plate = model.FindPlateAt(column, row);

            if (dragging.Definition.Kind == BackpackDefKind.Cloth)
                return plate == null || plate == m_plateInHand;

            if (plate == null || plate == m_plateInHand)
                return false;

            // 只有真在网格上的那件才谈得上「自己占的格」，还没落位的临时实例不能拿 ItemCovers 自我豁免
            return model.IsCellFree(column, row)
                   || (model.Items.Contains(dragging) && model.ItemCovers(dragging, column, row));
        }

        // 布到手上了只重染格子：整盘重铺会把正在拖的那块视图掀回原位，拖拽当场就废了
        public void BeginPlateDrag(BackpackItemInstance plate)
        {
            if (plate == null || m_backpack == null)
                return;

            m_plateInHand = plate;
            raiseBackpackPanel();
            // 布已经离地，压在它下面的操作条与详情小窗跟着收起来，免得拖的时候满屏都是控件
            m_plateBar.gameObject.SetActive(false);
            m_detailRect.gameObject.SetActive(false);
            fillSlotColors();
        }

        // 松手之前先解除手持，格子才有机会按布落下之后的样子重染
        public void EndPlateDrag(BackpackItemInstance plate)
        {
            // 这块布还认不认得都要先把层还回去，漏一次背包面板就一直压在商店上面
            restoreBackpackPanel();

            if (m_plateInHand == null)
                return;

            if (plate != null && m_plateInHand != plate)
                return;

            m_plateInHand = null;
            // 操作条不用在这里补回来：落下之后一定走 TryMovePlate 的重铺，那时会自己浮回来
            fillSlotColors();
        }

        // 点中的是哪件东西，两行写在详情小窗：第一行名字，第二行它身上的能力线
        public void ShowDetail(BackpackItemInstance item)
        {
            if (item?.Definition == null)
            {
                clearDetail();
                return;
            }

            // 换成已入手的东西，商品那份价钱与预览一起收掉，两条链路各写各的
            m_detailGoods = null;
            m_detailCardRect = null;
            m_detailPriceText.gameObject.SetActive(false);
            setDetailNameReserve(0f);

            m_detailTarget = item;
            m_detailBuffs.Clear();
            item.Definition.FillBuffs(item.Tier, m_detailBuffs);

            m_detailRect.gameObject.SetActive(true);
            m_detailNameText.text = UIShopGoodsName.Of(item.Definition, item.Tier);
            m_detailDescText.text = UIShopBuffText.OfMany(m_detailBuffs);
            // 左侧色条染成这件东西的对应色，布吃布自己的底色，装备吃档位品质色
            Color qualityColor = UIShopQuality.ColorFor(item.Definition, item.Tier);
            m_detailAccent.color = qualityColor;

            // 布在背包里可能已经被转过向，预览要照它此刻的朝向画，不能永远画基准那一面
            if (item.Definition.Kind == BackpackDefKind.Cloth)
                setDetailClothPreview(item.ShapeDef, qualityColor, item.OrientationIndex);
            else
                clearDetailClothPreview();
        }

        // 点开商品卡看的是「还没到手的这一件」，与已入手的物件共用同一扇小窗，只是多一个价钱
        public void ShowGoodsDetail(ShopStockEntry entry, RectTransform cardRect)
        {
            if (entry == null)
            {
                clearDetail();
                return;
            }

            // 看商品的时候不占用选中态：东西还躺在货架上，网格里的选中物不该被它顶掉
            m_selectedPlate = null;
            m_selectedItem = null;
            m_detailTarget = null;
            m_detailGoods = entry;
            m_detailCardRect = cardRect;

            Color goodsColor = UIShopQuality.ColorFor(entry.Definition, entry.Tier);

            m_detailNameText.text = UIShopGoodsName.Of(entry.Definition, entry.Tier);
            m_detailBuffs.Clear();
            entry.Definition.FillBuffs(entry.Tier, m_detailBuffs);
            m_detailDescText.text = UIShopBuffText.OfMany(m_detailBuffs);
            m_detailAccent.color = goodsColor;

            // 能点开的一定还在卖，价钱照直写，买不起才染红
            m_detailPriceText.text = $"{entry.Price} 金币";
            m_detailPriceText.color = m_run != null && entry.Price > m_run.Coins
                ? UIShopQuality.PriceUnaffordable
                : m_detailPriceDefaultColor;
            m_detailPriceText.gameObject.SetActive(true);

            // 名字行要给右上角的价钱让位，让出的宽度按价钱自己量出来
            setDetailNameReserve(m_detailPriceText.GetPreferredValues(m_detailPriceText.text).x
                + DETAIL_PRICE_INSET_RIGHT + DETAIL_PRICE_GAP);

            if (entry.Definition.Kind == BackpackDefKind.Cloth)
                setDetailClothPreview(entry.Definition.ShapeAt(entry.Tier), goodsColor, 0);
            else
                clearDetailClothPreview();

            refreshActionBar();
        }

        // 商品抓到手上：钱还没扣、东西也还不在网格上，只把背包面板提起来，收起操作条与小窗，留幽灵跟着手
        public void BeginGoodsDrag(ShopStockEntry entry)
        {
            m_pendingPurchase = entry;
            raiseBackpackPanel();
            m_plateBar.gameObject.SetActive(false);
            m_detailRect.gameObject.SetActive(false);
        }

        // 松手先还层再谈成交，成没成都要把提上去的面板放回原位
        public void EndGoodsDrag(ShopStockEntry entry)
        {
            m_pendingPurchase = null;
            restoreBackpackPanel();
            refreshActionBar();
        }

        // 按玩家拖出来的落点买成这一件，铺不开就整笔作废，面板不替它另找位置
        public bool TryBuyGoodsAt(ShopStockEntry entry, int anchorColumn, int anchorRow)
        {
            if (m_shop == null)
                return false;

            ShopBuyResult result = m_shop.TryBuyAt(entry, m_run, m_backpack, anchorColumn, anchorRow);
            handleBuyResult(entry, result, false);
            return result == ShopBuyResult.Success;
        }

        // 直接把这一件买进暂存架，架子没空位同样整笔作废
        public bool TryBuyGoodsToStash(ShopStockEntry entry)
        {
            if (m_shop == null)
                return false;

            ShopBuyResult result = m_shop.TryBuyToStash(entry, m_run, m_backpack);
            handleBuyResult(entry, result, true);
            return result == ShopBuyResult.Success;
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

        // 拖起来的东西出了网格就压在仓库与商店底下，把整块背包面板提到最后才压得住
        // 提层只认第一次的序号，重复调用不会把提层后的序号当成原序号记下来
        private void raiseBackpackPanel()
        {
            if (m_backpackPanelRect == null || m_backpackPanelIndex >= 0)
                return;

            m_backpackPanelIndex = m_backpackPanelRect.GetSiblingIndex();
            m_backpackPanelRect.SetAsLastSibling();
        }

        // 松手放回原位，没提过就是空转，中途关面板也靠 OnOpen 再兜一次
        private void restoreBackpackPanel()
        {
            if (m_backpackPanelRect == null || m_backpackPanelIndex < 0)
                return;

            m_backpackPanelRect.SetSiblingIndex(m_backpackPanelIndex);
            m_backpackPanelIndex = -1;
        }

        private void refreshCounters()
        {
            m_coinsText.text = $"金币 {m_run.Coins}";
            m_targetText.text = $"渔获目标 {m_run.RoundValue} / {m_run.RoundTarget}";
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
                // 还在飞的这几件先让格子空着，落地才显形
                if (i < stash.Count && !m_flyingIn.Contains(stash[i]))
                    m_storageSlots[i].SetStored(stash[i], i, this);
                else
                    m_storageSlots[i].SetEmpty();
            }
        }

        // ==================== 飞进暂存架 ====================
        // 模型动手之前把每件装备此刻的中心与尺寸抄下来，事后视图会被重铺，那时候再问就晚了
        private void captureStashLanding()
        {
            m_flyCenters.Clear();
            m_flySizes.Clear();
            m_flyAngles.Clear();
            m_stashCountBefore = m_backpack.Stash.Count;

            IReadOnlyList<BackpackItemInstance> items = m_backpack.Model.Items;

            for (int i = 0; i < items.Count && i < m_itemsShown; i++)
            {
                RectTransform rect = (RectTransform)m_itemViews[i].transform;
                m_flyCenters[items[i]] = rectCenter(rect);
                // 起飞姿态照抄图标自己那一份，拧着摆的那件飞起来也拧着，不会看着换了个东西
                m_flySizes[items[i]] = m_itemViews[i].IconSize;
                m_flyAngles[items[i]] = m_itemViews[i].IconAngle;
            }
        }

        // 多进架的这几件各起一段飞行，先登记后重铺，架子那一格就靠这份登记决定显不显形
        private void flyNewStashEntries()
        {
            IReadOnlyList<BackpackItemInstance> stash = m_backpack.Stash;

            for (int i = m_stashCountBefore; i < stash.Count; i++)
            {
                BackpackItemInstance item = stash[i];

                // 本来就不在网格上的东西没有出发点，例如拆下来直接进架的那块布
                if (!m_flyCenters.TryGetValue(item, out Vector3 center))
                    continue;

                if (!m_flyingIn.Add(item))
                    continue;

                m_flyAngles.TryGetValue(item, out float angle);
                m_flyingCount++;
                StartCoroutine(flyToStashCoroutine(item, center, m_flySizes[item], angle));
            }

            m_flyCenters.Clear();
            m_flySizes.Clear();
            m_flyAngles.Clear();
        }

        // 架子那份只读列表没有 IndexOf，一共就五件，线性找一遍就够
        private int stashIndexOf(BackpackItemInstance item)
        {
            IReadOnlyList<BackpackItemInstance> stash = m_backpack.Stash;

            for (int i = 0; i < stash.Count; i++)
            {
                if (ReferenceEquals(stash[i], item))
                    return i;
            }

            return -1;
        }

        // 从原来那格飞到架子那一格，尺寸一路缩到格子大小，看着就是被收进了架子
        private IEnumerator flyToStashCoroutine(BackpackItemInstance item, Vector3 startCenter, Vector2 startSize, float startAngle)
        {
            GameObject flyer = createFlyer(item, startCenter, startSize, startAngle);
            RectTransform flyRect = (RectTransform)flyer.transform;
            Vector2 start = toFlyLocal(startCenter);
            float flown = 0f;

            while (flown < FLY_DURATION)
            {
                int stashIndex = stashIndexOf(item);

                // 中途这件被取回、架子重排、或者面板关掉把图收掉了，这段飞行就没必要再演下去
                if (flyer == null || stashIndex < 0 || stashIndex >= m_storageSlots.Length)
                    break;

                RectTransform slot = (RectTransform)m_storageSlots[stashIndex].transform;
                float t = Mathf.SmoothStep(0f, 1f, flown / FLY_DURATION);

                flyRect.anchoredPosition = Vector2.Lerp(start, toFlyLocal(rectCenter(slot)), t);
                flyRect.sizeDelta = Vector2.Lerp(startSize, slot.rect.size, t);
                // 图标可以拧着摆，架子那一格却是正的，转角一路归零才对得上落地的样子
                flyRect.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(startAngle, 0f, t));

                flown += Time.deltaTime;
                yield return null;
            }

            if (flyer != null)
                Destroy(flyer);

            finishFlight(item);
        }

        // 借一张图当飞行物：有图标就贴图，没图标染品质色，尺寸先按它原来占的那块包围盒
        private GameObject createFlyer(BackpackItemInstance item, Vector3 startCenter, Vector2 startSize, float startAngle)
        {
            ensureFlyLayer();

            GameObject flyObject = new GameObject(FLY_LAYER_NAME + "_Item", typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)flyObject.transform;
            rect.SetParent(m_flyRect, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = startSize;
            rect.anchoredPosition = toFlyLocal(startCenter);
            // 架子那一格是方的，转角一路归零，落进去正好和暂存格的摆法对上
            rect.localEulerAngles = new Vector3(0f, 0f, startAngle);

            Sprite icon = item.Definition.Icon;
            Image image = flyObject.GetComponent<Image>();
            image.sprite = icon;
            image.raycastTarget = false;
            image.color = icon != null ? Color.white : UIShopQuality.ColorFor(item.Definition, item.Tier);
            return flyObject;
        }

        // 飞行层是运行时建的一层，挂在面板最上面才压得过背包与仓库，用完留着下次再飞
        private void ensureFlyLayer()
        {
            if (m_flyRect != null)
            {
                m_flyRect.SetAsLastSibling();
                return;
            }

            GameObject layerObject = new GameObject(FLY_LAYER_NAME, typeof(RectTransform));
            m_flyRect = (RectTransform)layerObject.transform;
            m_flyRect.SetParent(transform, false);
            m_flyRect.anchorMin = Vector2.zero;
            m_flyRect.anchorMax = Vector2.one;
            m_flyRect.offsetMin = Vector2.zero;
            m_flyRect.offsetMax = Vector2.zero;
        }

        // 屏幕上的一个点换算到飞行层的局部坐标，与这个文件其余的换算一样按覆盖式画布算
        private Vector2 toFlyLocal(Vector3 worldPoint)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, worldPoint);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_flyRect, screen, null, out Vector2 local);
            return local;
        }

        // 取一个矩形在屏幕上的中心，起点与落点都按中心算，不受各层枢轴摆法的影响
        private Vector3 rectCenter(RectTransform rect)
        {
            rect.GetWorldCorners(m_flyCorners);
            return (m_flyCorners[0] + m_flyCorners[2]) * 0.5f;
        }

        // 一件落地，架子那一格这才显形
        private void finishFlight(BackpackItemInstance item)
        {
            if (!m_flyingIn.Remove(item))
                return;

            m_flyingCount--;
            fillStorage();
        }

        // 面板中途关掉，飞着的东西一律作废，重开时直接按模型的最新状态铺
        private void stopFlights()
        {
            m_flyCenters.Clear();
            m_flySizes.Clear();
            m_flyAngles.Clear();

            if (m_flyingCount < 1)
                return;

            m_flyingIn.Clear();
            m_flyingCount = 0;

            if (m_flyRect != null)
            {
                Destroy(m_flyRect.gameObject);
                m_flyRect = null;
            }
        }

        private void showItem(BackpackItemInstance item)
        {
            while (m_itemViews.Count <= m_itemsShown)
                m_itemViews.Add(Instantiate(m_itemTemplate, m_itemsRect));

            UIShopItemView view = m_itemViews[m_itemsShown];
            view.gameObject.SetActive(true);
            view.Setup(item, m_cellSize, m_cellSpacing, this, m_cellSprite);
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

        // 选中框跟着当前选中物占的那几格走，布与装备共用，没选中就把整层收干净
        private void refreshSelectionFrames()
        {
            float pitch = m_cellSize + m_cellSpacing;
            m_selectFramesUsed = 0;

            for (int i = 0; i < m_selectedCells.Count; i++)
            {
                Vector2Int cell = m_selectedCells[i];
                showSelectionFrame(cell.x, cell.y, pitch);
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
            m_plateStashButton = createPlateBarButton(PLATE_STASH_NAME, "拆下", 1, out m_plateStashText);
            m_plateSellButton = createPlateBarButton(PLATE_SELL_NAME, "出售", 2, out m_plateSellText);

            // 三个按钮各管一件事，建完当场就绑上，少绑一个条上就有一个键按了没反应
            m_plateRotateButton.onClick.AddListener(handleRotateClicked);
            m_plateStashButton.onClick.AddListener(handleStashClicked);
            m_plateSellButton.onClick.AddListener(handleSellClicked);
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

        // 详情小窗是独立预制体，这里只实例化一次并认好两行文本与色条，版式与配色全在预制体里改
        // 它挂在整块背景上而不是物品层，摆到选中物左右时才不会被 800 宽的网格卡住
        private void bindDetailWindow()
        {
            // 预制体或挂载点缺失都在 OnInit 里报过错了，这里只跳过绑定，别让整层面板起不来
            if (DetailWindowPrefab == null || m_backgroundRect == null)
                return;

            GameObject windowObject = Instantiate(DetailWindowPrefab, m_backgroundRect);
            windowObject.name = DETAIL_WINDOW_NAME;
            m_detailRect = windowObject.GetComponent<RectTransform>();

            m_detailNameText = findDetailChild<TMP_Text>(windowObject.transform, DETAIL_NAME_TEXT_PATH);
            m_detailDescText = findDetailChild<TMP_Text>(windowObject.transform, DETAIL_DESC_TEXT_PATH);
            m_detailAccent = findDetailChild<Image>(windowObject.transform, DETAIL_ACCENT_PATH);
            m_detailPriceText = findDetailChild<TMP_Text>(windowObject.transform, DETAIL_PRICE_PATH);
            m_detailClothShape = findDetailChild<UIShopClothShape>(windowObject.transform, DETAIL_CLOTH_SHAPE_PATH);
            // 价钱本来的颜色读自预制体，买得起就染回它，调色板改了不用回来动代码
            m_detailPriceDefaultColor = m_detailPriceText.color;
            // 高跟着预制体走，脚本只留一个数用来算落点，改小窗高度不用回来动这里
            m_detailBaseHeight = m_detailHeight = m_detailRect.sizeDelta.y;

            // 价钱与布形预览只在商品那一份详情里露脸，绑完先各自收好
            m_detailPriceText.gameObject.SetActive(false);
            m_detailClothShape.SetVisible(false);
            m_detailRect.gameObject.SetActive(false);
        }

        // 预制体里的子节点少了就是版式被人动过，编辑器下先报明是哪个节点，再让它抛出去
        private T findDetailChild<T>(Transform windowRoot, string path) where T : Component
        {
            Transform child = windowRoot.Find(path);
#if UNITY_EDITOR
            if (child == null)
                QLog.Error($"详情小窗预制体缺少节点 {path}，请检查 {DetailWindowPrefab.name}");
#endif
            return child.GetComponent<T>();
        }

        // 详情小窗跟着选中物浮出来，摆哪一侧从六个候选里挑：右侧贴顶、右侧贴底、左侧贴顶、左侧贴底、正上、正下
        private void refreshDetailWindow(int minColumn, int maxColumn, int minRow, int maxRow)
        {
            if (m_detailRect == null || m_detailTarget == null)
                return;

            m_detailRect.gameObject.SetActive(true);

            float width = detailWidth();
            m_detailRect.sizeDelta = new Vector2(width, m_detailHeight);

            // 选中物占的那几格合成包围盒，再整体搬到背景坐标系，小窗才允许越过物品层的边界
            Vector2 gridCorner = toBackground(new Vector2(m_itemsRect.rect.xMin, m_itemsRect.rect.yMax));
            float pitch = m_cellSize + m_cellSpacing;
            float left = gridCorner.x + minColumn * pitch;
            float right = gridCorner.x + maxColumn * pitch + m_cellSize;
            float top = gridCorner.y - minRow * pitch;
            float bottom = gridCorner.y - (maxRow * pitch + m_cellSize);

            m_detailRect.anchoredPosition = detailAnchor(left, right, top, bottom, width);
            // 小窗要压过货架与暂存架才看得清，它本身不吃点击，压着也不挡住拖拽
            m_detailRect.SetAsLastSibling();
        }

        // 窗里长出一条布形预览，小窗跟着加高，颜色与朝向都由调用方按货架上的或手里的这块布给定
        private void setDetailClothPreview(GridShapeSO shape, Color cellColor, int orientationIndex)
        {
            m_detailClothShape.SetVisible(true);
            m_detailClothShape.Setup(shape, cellColor, m_cellSprite, orientationIndex);
            m_detailHeight = m_detailBaseHeight + DETAIL_PREVIEW_EXTRA;
        }

        private void clearDetailClothPreview()
        {
            m_detailClothShape.SetVisible(false);
            m_detailHeight = m_detailBaseHeight;
        }

        // 价钱压在名字行右上方，名字的可写宽度要扣掉它让出来的那段，两行才不叠字
        private void setDetailNameReserve(float reserveWidth)
        {
            Vector2 size = m_detailNameText.rectTransform.sizeDelta;
            size.x = -(DETAIL_TEXT_INSET_LEFT + DETAIL_TEXT_INSET_RIGHT + reserveWidth);
            m_detailNameText.rectTransform.sizeDelta = size;
        }

        // 名字与文案谁更长就照谁定宽，价钱占掉的那一角也要算进宽里，超出上下限的部分交给省略号
        private float detailWidth()
        {
            float nameWidth = m_detailNameText.GetPreferredValues(m_detailNameText.text).x;
            float descWidth = m_detailDescText.GetPreferredValues(m_detailDescText.text).x;
            float priceReserve = m_detailPriceText.gameObject.activeSelf
                ? m_detailPriceText.GetPreferredValues(m_detailPriceText.text).x
                  + DETAIL_PRICE_INSET_RIGHT + DETAIL_PRICE_GAP
                : 0f;
            float widest = Mathf.Max(nameWidth, descWidth) + DETAIL_TEXT_INSET_LEFT
                           + DETAIL_TEXT_INSET_RIGHT + priceReserve;
            return Mathf.Clamp(widest, DETAIL_MIN_WIDTH, DETAIL_MAX_WIDTH);
        }

        // 商品卡在自己的父级里，量它的角只要绕一圈世界坐标搬到背景层
        private Vector2 cornerToBackground(Vector3 worldPoint)
        {
            Vector3 local = m_backgroundRect.InverseTransformPoint(worldPoint);
            return new Vector2(local.x, local.y);
        }

        // 商品的详情浮在被点那张卡的上方：卡下面贴着面板底缘与按钮条，那点位置塞不进一扇窗
        private void showGoodsDetailWindow()
        {
            if (m_detailRect == null || m_detailCardRect == null)
                return;

            m_detailRect.gameObject.SetActive(true);

            float width = detailWidth();
            m_detailRect.sizeDelta = new Vector2(width, m_detailHeight);

            m_detailCardRect.GetWorldCorners(m_detailCorners);
            Vector2 topLeft = cornerToBackground(m_detailCorners[1]);
            Vector2 bottomRight = cornerToBackground(m_detailCorners[3]);

            Rect safe = m_backgroundRect.rect;
            float halfWidth = width * 0.5f;
            float halfHeight = m_detailHeight * 0.5f;
            float leftLimit = safe.xMin + DETAIL_SAFE_MARGIN + halfWidth;
            float rightLimit = safe.xMax - DETAIL_SAFE_MARGIN - halfWidth;
            float bottomLimit = safe.yMin + DETAIL_SAFE_MARGIN + halfHeight;
            float topLimit = safe.yMax - DETAIL_SAFE_MARGIN - halfHeight;

            float centerX = Mathf.Clamp((topLeft.x + bottomRight.x) * 0.5f, leftLimit, rightLimit);
            float centerY = topLeft.y + DETAIL_GAP + halfHeight;

            // 顶上顶不下了就翻到卡下方，总之先保证整扇窗还在背景里
            if (centerY > topLimit)
                centerY = bottomRight.y - DETAIL_GAP - halfHeight;

            m_detailRect.anchoredPosition = new Vector2(centerX, Mathf.Clamp(centerY, bottomLimit, topLimit));
            // 小窗要压过货架与暂存架才看得清，它本身不吃点击，压着也不挡住拖拽
            m_detailRect.SetAsLastSibling();
        }

        // 物品层里的一个点搬到背景坐标系：两层没有旋转也没有缩放，绕世界坐标走一遍就够
        private Vector2 toBackground(Vector2 itemsLocal)
        {
            Vector3 world = m_itemsRect.TransformPoint(new Vector3(itemsLocal.x, itemsLocal.y, 0f));
            Vector3 backgroundLocal = m_backgroundRect.InverseTransformPoint(world);
            return new Vector2(backgroundLocal.x, backgroundLocal.y);
        }

        // 六个候选位挨个试，越出安全区或压着操作条的都跳过，第一个干净的就当落点
        private Vector2 detailAnchor(float left, float right, float top, float bottom, float width)
        {
            // 只读 RectTransform.rect 的四条边，不新建 Rect，免得踩轴心歧义的坑
            Rect safe = m_backgroundRect.rect;
            float safeLeft = safe.xMin + DETAIL_SAFE_MARGIN;
            float safeRight = safe.xMax - DETAIL_SAFE_MARGIN;
            float safeTop = safe.yMax - DETAIL_SAFE_MARGIN;
            float safeBottom = safe.yMin + DETAIL_SAFE_MARGIN;

            float barLeft = 0f, barRight = 0f, barTop = 0f, barBottom = 0f;
            bool barVisible = m_plateBar != null && m_plateBar.gameObject.activeSelf;
            if (barVisible)
            {
                Vector2 corner = toBackground(new Vector2(m_itemsRect.rect.xMin + m_plateBar.anchoredPosition.x,
                    m_itemsRect.rect.yMax + m_plateBar.anchoredPosition.y));
                barLeft = corner.x;
                barRight = corner.x + m_plateBar.sizeDelta.x;
                barTop = corner.y;
                barBottom = corner.y - m_plateBar.sizeDelta.y;
            }

            for (int index = 0; index < DETAIL_CANDIDATE_COUNT; index++)
            {
                Vector2 corner = detailCandidate(index, left, right, top, bottom, width);
                float windowBottom = corner.y - m_detailHeight;

                if (corner.x < safeLeft || corner.x + width > safeRight)
                    continue;
                if (corner.y > safeTop || windowBottom < safeBottom)
                    continue;
                if (barVisible && corner.x < barRight && barLeft < corner.x + width &&
                    windowBottom < barTop && barBottom < corner.y)
                    continue;

                return new Vector2(corner.x + width * 0.5f, corner.y - m_detailHeight * 0.5f);
            }

            // 六个位置全被占，就退回右侧贴顶那一个再夹回安全区，总得让玩家看得见名字
            Vector2 fallback = detailCandidate(0, left, right, top, bottom, width);
            float centerX = Mathf.Clamp(fallback.x + width * 0.5f, safeLeft + width * 0.5f, safeRight - width * 0.5f);
            float centerY = Mathf.Clamp(fallback.y - m_detailHeight * 0.5f,
                safeBottom + m_detailHeight * 0.5f, safeTop - m_detailHeight * 0.5f);
            return new Vector2(centerX, centerY);
        }

        // 候选位的排法：0/1 在右侧贴顶与贴底，2/3 在左侧贴顶与贴底，4 正上方，5 正下方，都留一份 DETAIL_GAP；返回小窗左上角
        private Vector2 detailCandidate(int index, float left, float right, float top, float bottom, float width)
        {
            switch (index)
            {
                case 0: return new Vector2(right + DETAIL_GAP, top);
                case 1: return new Vector2(right + DETAIL_GAP, bottom);
                case 2: return new Vector2(left - DETAIL_GAP - width, top);
                case 3: return new Vector2(left - DETAIL_GAP - width, bottom);
                case 4: return new Vector2((left + right - width) * 0.5f, top + DETAIL_GAP + m_detailHeight);
                default: return new Vector2((left + right - width) * 0.5f, bottom - DETAIL_GAP);
            }
        }

        // 收起小窗：价钱与布形预览都是商品那一份的东西，一并还原成预制体那个样子
        // 手上正拖着的商品不在这里管，那是 EndGoodsDrag 收的尾
        private void clearDetail()
        {
            m_detailTarget = null;
            m_detailGoods = null;
            m_detailCardRect = null;
            m_detailPriceText.gameObject.SetActive(false);
            setDetailNameReserve(0f);
            clearDetailClothPreview();
            m_detailRect.gameObject.SetActive(false);
        }

        // 卖掉、拆走、被挤进暂存架之后，详情里那件东西已经不在网格上了，不能留一张空壳文案
        private void validateDetail()
        {
            // 商品那一份只认货架：买走了或这面货架重抽了，小窗就不该再挂着它
            if (m_detailGoods != null)
            {
                if (m_detailGoods.IsSold || m_shop == null || !m_shop.Stock.Contains(m_detailGoods))
                    clearDetail();
                return;
            }

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

        // 操作条跟着当前选中物走：放得下就压在它上边，贴到顶了就挪到下边
        private void refreshActionBar()
        {
            m_selectedCells.Clear();

            BackpackItemInstance target = m_selectedPlate != null ? m_selectedPlate : m_selectedItem;
            bool onGrid = target != null && m_backpack != null &&
                          (m_backpack.Model.Plates.Contains(target) || m_backpack.Model.Items.Contains(target));

            // 选中的东西被卖掉、拆走或挤进暂存架，条子、外框与详情一起收掉
            if (!onGrid)
            {
                m_selectedPlate = null;
                m_selectedItem = null;
                m_plateBar.gameObject.SetActive(false);
                // 商品那一份详情不靠选中态活着，条子收起时它要跟着重摆，不能一并清掉
                if (m_detailGoods != null)
                    showGoodsDetailWindow();
                else
                    clearDetail();
                refreshSelectionFrames();
                return;
            }

            target.FillWorldCells(m_selectedCells);
            bool plateMode = m_selectedPlate != null;

            int minColumn = int.MaxValue;
            int maxColumn = int.MinValue;
            int minRow = int.MaxValue;
            int maxRow = int.MinValue;

            for (int i = 0; i < m_selectedCells.Count; i++)
            {
                Vector2Int cell = m_selectedCells[i];
                minColumn = Mathf.Min(minColumn, cell.x);
                maxColumn = Mathf.Max(maxColumn, cell.x);
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
            // 条的 pivot 在左上角，落在选中物上方要把整条高度加回来
            float above = topEdge + BAR_GAP + barHeight;
            float below = bottomEdge - BAR_GAP;

            m_plateBar.anchoredPosition = new Vector2(
                Mathf.Clamp(minColumn * pitch, 0f, Mathf.Max(0f, gridWidth - barWidth)),
                Mathf.Clamp(minRow > 0 ? above : below, barHeight - gridHeight, 0f));

            m_plateRotateButton.interactable = target.CanRotate;

            // 布拆下来叫拆下，装备摘下来叫取下，两件事本质一样：都进暂存架
            m_plateStashText.text = plateMode ? "拆下" : "取下";

            // 回收价只有布料总表算得出来，装备在这条上一直显示不回收
            int sellPrice = m_shop != null ? m_shop.SellPriceOf(target.Definition) : 0;
            m_plateSellText.text = sellPrice > 0 ? $"卖 {sellPrice}" : "不回收";
            m_plateSellButton.interactable = sellPrice > 0;

            m_plateBar.gameObject.SetActive(true);
            // 重铺会新插物品视图，条子每次都要重新压到最上面才不会被盖住
            m_plateBar.SetAsLastSibling();

            // 小窗排在条子之后，它要拿条子已经落好的位置做避让；商品那份跟着卡走，不跟着网格走
            if (m_detailGoods != null)
                showGoodsDetailWindow();
            else
                refreshDetailWindow(minColumn, maxColumn, minRow, maxRow);

            refreshSelectionFrames();
        }

        // 条上三个按钮都只看选中者是布还是装备，成不成都由各自那条链路重铺
        private void handleRotateClicked()
        {
            if (m_selectedPlate != null)
                TryRotatePlate(m_selectedPlate);
            else
                TryRotate(m_selectedItem);
        }

        private void handleStashClicked()
        {
            if (m_selectedPlate != null)
                TryStashPlate(m_selectedPlate);
            else
                TryStashItem(m_selectedItem);
        }

        private void handleSellClicked()
        {
            if (m_selectedPlate != null)
                TrySellPlate(m_selectedPlate);
            else
                TrySellItem(m_selectedItem);
        }

        private void handleClearPlateSelection()
        {
            m_selectedItem = null;
            SelectPlate(null);
        }

        // 卖装备先摘再收钱：算不出回收价就一分不收，条上的键这时本来就是灰的
        private bool TrySellItem(BackpackItemInstance item)
        {
            if (m_backpack == null || item == null)
                return false;

            if (m_shop == null)
            {
                QLog.Error("没有传入 ShopService，装备算不出回收价");
                return false;
            }

            int price = m_shop.SellPriceOf(item.Definition);
            if (price <= 0)
                return false;

            if (!m_backpack.TryRemoveItem(item))
                return false;

            m_run.AddCoins(price);
            m_selectedItem = null;
            QLog.Info($"{UIShopGoodsName.Of(item.Definition, item.Tier)} 已卖出，回收 {price} 金币");
            refreshBackpack();
            refreshCounters();
            return true;
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
            goods.Setup(entry, UIShopQuality.ColorFor(entry.Definition, entry.Tier), affordable, this, m_cellSprite);
            m_goodsShown++;
        }

        // 买成与否则全由服务决定，面板只管结果对应的界面反应
        // toStash 只影响文案：买进架子的那件既不铺格也不给 BUFF，得让玩家看见
        private void handleBuyResult(ShopStockEntry entry, ShopBuyResult result, bool toStash)
        {
            string label = UIShopGoodsName.Of(entry.Definition, entry.Tier);

            switch (result)
            {
                case ShopBuyResult.Success:
                    QLog.Info(toStash ? $"{label} 已放进暂存架，暂存中的板块不生效" : $"{label} 已购入");
                    clearDetail();
                    refreshAll();
                    break;
                case ShopBuyResult.NotEnoughCoins:
                    QLog.Warning($"金币不够，还差 {entry.Price - m_run.Coins}");
                    break;
                // 落点铺不开与架子满都是整笔作废，钱已原额退回，金币得跟着刷回去
                case ShopBuyResult.NoRoom:
                    QLog.Info(toStash ? "暂存架没有空位，钱已退回" : "这个位置铺不开，钱已退回");
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
