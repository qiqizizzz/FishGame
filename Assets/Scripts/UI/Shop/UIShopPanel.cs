/*
* ┌──────────────────────────────────┐
* │  描    述: 商店与背包面板，回合内展示金币、背包网格、仓库暂存与可购商品
* │  类    名: UIShopPanel.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 数值只读不写：金币与剩余下潜次数取自传入的 RunState，面板不回查任何玩法对象
* 背包内容与商品定价目前都是界面占位，背包与商店模块接上后整段替换
* 单格边长与间距从网格组件读，改版式只动预制体不动脚本
* 物品视图与商品卡由面板自带的隐藏模板复制并留用，只开关不销毁
*/

using System.Collections.Generic;
using Framework.QTower.View;
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

        // 从网格读出来的单格尺寸，摆物品时算跨格用
        private float m_cellSize;
        private float m_cellSpacing;

        // 复制出来的物品视图与商品卡只开关不销毁，销毁会延到帧尾，同帧重建会被布局算进内容高度
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

            m_refreshButton.onClick.AddListener(handleRefreshClicked);
            m_diveButton.onClick.AddListener(handleDiveClicked);
            m_closeButton.onClick.AddListener(handleCloseClicked);
        }

        protected override void OnOpen(params object[] args)
        {
            RunState run = args.Length > 0 ? args[0] as RunState : null;

#if UNITY_EDITOR
            if (run == null)
                QLog.Error("没有传入 RunState，面板留空");
#endif

            if (run == null)
                return;

            refreshCounters(run);
            fillBackpack();
            fillStorage();
            rebuildGoods();
        }

        // ==================== Private Function ====================
        private void refreshCounters(RunState run)
        {
            m_coinsText.text = $"金币 {run.Coins}";
            m_targetText.text = $"目标 {run.RoundValue} / {run.RoundTarget}";
            m_divesText.text = $"下潜 {run.DivesRemaining} 次";
        }

        // 占位：外圈一圈锁定，中心九格可用，背包模块接入后改由格子解锁数据驱动
        private void fillBackpack()
        {
            int columns = m_grid.constraintCount;
            int rows = m_slots.Length / columns;

            for (int i = 0; i < m_slots.Length; i++)
            {
                int row = i / columns;
                int column = i % columns;
                bool isEdge = row == 0 || column == 0 || row == rows - 1 || column == columns - 1;

                if (isEdge)
                    m_slots[i].SetLocked();
                else
                    m_slots[i].SetEmpty();
            }

            rebuildItemViews();
        }

        // 占位：三件形状不同的物品把跨格效果看出来，坐标是（列, 行）
        private void rebuildItemViews()
        {
            m_itemsShown = 0;

            showItem(new[] { new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(1, 3) }, "教学鱼竿", UIShopQuality.White);
            showItem(new[] { new Vector2Int(2, 2), new Vector2Int(2, 3), new Vector2Int(3, 3) }, "幸运罗盘", UIShopQuality.Purple);
            showItem(new[] { new Vector2Int(3, 1) }, "发光鱼饵", UIShopQuality.Blue);

            for (int i = m_itemsShown; i < m_itemViews.Count; i++)
                m_itemViews[i].gameObject.SetActive(false);
        }

        // 占位：仓库先摆两件暂存物品看效果，接入后由仓库数据驱动
        private void fillStorage()
        {
            for (int i = 0; i < m_storageSlots.Length; i++)
                m_storageSlots[i].SetEmpty();

            storeItem(0, "延长鱼线", UIShopQuality.Blue);
            storeItem(1, "鱼竿工具箱", UIShopQuality.Gold);
        }

        private void showItem(Vector2Int[] cells, string itemName, Color qualityColor)
        {
            while (m_itemViews.Count <= m_itemsShown)
                m_itemViews.Add(Instantiate(m_itemTemplate, m_itemsRect));

            UIShopItemView item = m_itemViews[m_itemsShown];
            item.gameObject.SetActive(true);
            item.Setup(cells, m_cellSize, m_cellSpacing, qualityColor, itemName);
            m_itemsShown++;
        }

        private void storeItem(int index, string itemName, Color qualityColor)
        {
            if (index >= m_storageSlots.Length)
            {
#if UNITY_EDITOR
                QLog.Error($"仓库格下标越界：{index}，当前共 {m_storageSlots.Length} 格");
#endif
                return;
            }

            m_storageSlots[index].SetStored(itemName, qualityColor);
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

        private void handleDiveClicked()
        {
            // 回合流程接上后这里改成进入钓鱼阶段，目前先把面板收起
            Hide();
        }

        private void handleCloseClicked()
        {
            Hide();
        }
    }
}
