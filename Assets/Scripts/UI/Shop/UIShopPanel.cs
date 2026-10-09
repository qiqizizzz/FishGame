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
* 单格边长与间距从网格组件读，改版式只动预制体不动脚本
* 物品视图与商品卡由模板复制并留用，只开关不销毁，销毁会延到帧尾被布局算进内容高度
* 面板不决定流程走向，按钮只广播「离开商店」，切阶段由控制器负责
*/

using System.Collections.Generic;
using Framework.QTower.Common.Defines;
using Framework.QTower.Event;
using Framework.QTower.View;
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
        private const string GOODS_PATH = "Bg/Panel_Shop/Row_Goods";
        private const string GOODS_TEMPLATE_PATH = GOODS_PATH + "/Goods_Template";
        private const string REFRESH_PATH = "Bg/Panel_Shop/Btn_Refresh";
        private const string DIVE_PATH = "Bg/Btns/Btn_Dive";
        private const string CLOSE_PATH = "Bg/Btns/Btn_Close";

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

#if UNITY_EDITOR
            if (m_grid == null)
                QLog.Error($"网格上没有 GridLayoutGroup，读不到单格尺寸：{GRID_PATH}");
            if (m_slots.Length == 0)
                QLog.Error($"背包网格里没有格子，请检查 {GRID_PATH}");
            if (m_storageSlots.Length == 0)
                QLog.Error($"仓库网格里没有格子，请检查 {STORAGE_PATH}");
#endif

            m_cellSize = m_grid.cellSize.x;
            m_cellSpacing = m_grid.spacing.x;

            // 模板本身不上架，留着当复制源，否则布局里会多出一块空位
            m_itemTemplate.gameObject.SetActive(false);
            m_goodsTemplate.gameObject.SetActive(false);

            m_refreshButton.onClick.AddListener(handleRefreshClicked);
            m_diveButton.onClick.AddListener(handleLeaveShopClicked);
            m_closeButton.onClick.AddListener(handleLeaveShopClicked);
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

        // 每一格是空格还是锁格，全看模型的解锁掩码，异形开局也由配置说话
        private void fillBackpack()
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

                if (model.IsCellUnlocked(column, row))
                    m_slots[i].SetEmpty();
                else
                    m_slots[i].SetLocked();
            }

            rebuildItemViews();
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
