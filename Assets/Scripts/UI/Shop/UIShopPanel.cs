/*
* ┌──────────────────────────────────┐
* │  描    述: 商店与背包面板，回合内展示金币、背包网格、仓库暂存与可购商品
* │  类    名: UIShopPanel.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 数值只读不写：金币与剩余下潜次数取自传入的 RunState，面板不回查任何玩法对象
* 背包与仓库读 RunBackpack 的真实数据，商品仍是界面占位，等商店库存模块接上再换
* 格子解锁直接问模型的解锁掩码，物品坐标直接问实例的世界格，界面不自己算位图
* 单格边长与间距从网格组件读，改版式只动预制体不动脚本
* 物品视图与商品卡由面板自带的隐藏模板复制并留用，只开关不销毁
* 面板不决定流程走向，按钮只广播「离开商店」，切阶段由控制器负责
*/

using System.Collections.Generic;
using Framework.QTower.Common.Defines;
using Framework.QTower.Event;
using Framework.QTower.View;
using Module.Backpack.Model;
using Module.Fish.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utils.log;

namespace UI.Shop
{
    public sealed class UIShopPanel : UIBase
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

        // 本局背包，面板只读它，不替它保管状态
        private RunBackpack m_backpack;

        // 从网格读出来的单格尺寸，摆物品时算跨格用
        private float m_cellSize;
        private float m_cellSpacing;

        // 复制出来的物品视图与商品卡只开关不销毁，销毁会延到帧尾，同帧重建会被布局算进内容高度
        private readonly List<UIShopItemView> m_itemViews = new();
        private readonly List<UIShopGoodsItem> m_goodsItems = new();

        // 装世界格坐标的缓冲，物品视图摆完就把值写进自己的方块，不存引用
        private readonly List<Vector2Int> m_cellBuffer = new();
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

            m_refreshButton.onClick.AddListener(handleRefreshClicked);
            m_diveButton.onClick.AddListener(handleLeaveShopClicked);
            m_closeButton.onClick.AddListener(handleLeaveShopClicked);
        }

        protected override void OnOpen(params object[] args)
        {
            RunState run = args.Length > 0 ? args[0] as RunState : null;
            RunBackpack backpack = args.Length > 1 ? args[1] as RunBackpack : null;

#if UNITY_EDITOR
            if (run == null)
                QLog.Error("没有传入 RunState，面板留空");
            if (backpack == null)
                QLog.Error("没有传入 RunBackpack，背包与仓库不会填充");
#endif

            if (run == null)
                return;

            refreshCounters(run);
            rebuildGoods();

            if (backpack == null)
                return;

            m_backpack = backpack;
            fillBackpack();
            fillStorage();
        }

        // ==================== Private Function ====================
        private void refreshCounters(RunState run)
        {
            m_coinsText.text = $"金币 {run.Coins}";
            m_targetText.text = $"目标 {run.RoundValue} / {run.RoundTarget}";
            m_divesText.text = $"下潜 {run.DivesRemaining} 次";
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

        // 网格上的物品全部来自模型，一件一个视图，形状格问实例要世界坐标
        private void rebuildItemViews()
        {
            IReadOnlyList<BackpackItemInstance> items = m_backpack.Model.Items;
            m_itemsShown = 0;

            for (int i = 0; i < items.Count; i++)
            {
                items[i].FillWorldCells(m_cellBuffer);
                showItem(m_cellBuffer, items[i].Definition.Label, UIShopQuality.TierColor(items[i].Tier));
            }

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
                    m_storageSlots[i].SetStored(stash[i].Definition.Label, UIShopQuality.TierColor(stash[i].Tier));
                else
                    m_storageSlots[i].SetEmpty();
            }
        }

        private void showItem(IReadOnlyList<Vector2Int> cells, string itemName, Color qualityColor)
        {
            while (m_itemViews.Count <= m_itemsShown)
                m_itemViews.Add(Instantiate(m_itemTemplate, m_itemsRect));

            UIShopItemView item = m_itemViews[m_itemsShown];
            item.gameObject.SetActive(true);
            item.Setup(cells, m_cellSize, m_cellSpacing, qualityColor, itemName);
            m_itemsShown++;
        }

        // 占位：商品池与售价都还没定，这里只是把一排卡片铺出来看版式
        private void rebuildGoods()
        {
            m_goodsShown = 0;

            showGoods("钓竿升阶", 120, UIShopQuality.Gold);
            showGoods("延长鱼线 +8 米", 30, UIShopQuality.Blue);
            showGoods("背包扩容 +1 格", 60, UIShopQuality.Purple);
            showGoods("金币加成 +10%", 90, UIShopQuality.White);

            for (int i = m_goodsShown; i < m_goodsItems.Count; i++)
                m_goodsItems[i].gameObject.SetActive(false);
        }

        private void showGoods(string goodsName, int price, Color qualityColor)
        {
            while (m_goodsItems.Count <= m_goodsShown)
                m_goodsItems.Add(Instantiate(m_goodsTemplate, m_goodsRect));

            UIShopGoodsItem goods = m_goodsItems[m_goodsShown];
            goods.gameObject.SetActive(true);
            goods.Setup(goodsName, price, qualityColor);
            m_goodsShown++;
        }

        private void handleRefreshClicked()
        {
            // 商品池接上之前重刷不出新内容，先走一次重铺把交互留在原位
            rebuildGoods();
        }

        // 商店阶段唯一的去处就是下潜，两个按钮暂时同义，等商店能买东西再分职责
        // 面板不自己决定下一阶段是谁，只喊一声「离开商店」，切阶段交给控制器
        private void handleLeaveShopClicked()
        {
            EventManager.PublishEvent(EventDefines.ShopExitRequested);
        }
    }
}
