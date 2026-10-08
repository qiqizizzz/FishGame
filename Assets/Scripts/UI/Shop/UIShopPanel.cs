/*
* ┌──────────────────────────────────┐
* │  描    述: 商店与背包面板，回合内展示金币、背包格子与可购商品
* │  类    名: UIShopPanel.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 数值只读不写：金币与剩余下潜次数取自传入的 RunState，面板不回查任何玩法对象
* 背包内容与商品定价目前都是界面占位，背包与商店模块接上后整段替换
* 商品卡由面板自带的隐藏模板复制并留用，只开关不销毁
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
        private const string GOODS_PATH = "Bg/Panel_Shop/Row_Goods";
        private const string GOODS_TEMPLATE_PATH = "Bg/Panel_Shop/Row_Goods/Goods_Template";
        private const string REFRESH_PATH = "Bg/Panel_Shop/Btn_Refresh";
        private const string DIVE_PATH = "Bg/Btns/Btn_Dive";
        private const string CLOSE_PATH = "Bg/Btns/Btn_Close";

        // 物品品质色，与背包层的四档链同源
        private static readonly Color m_qualityWhite = new Color32(0xC9, 0xCD, 0xD2, 0xFF);
        private static readonly Color m_qualityBlue = new Color32(0x3E, 0x7B, 0xFA, 0xFF);
        private static readonly Color m_qualityPurple = new Color32(0x8B, 0x5C, 0xF6, 0xFF);
        private static readonly Color m_qualityGold = new Color32(0xF5, 0xB9, 0x3B, 0xFF);

        // ==================== 字段[私有] ====================
        private TMP_Text m_coinsText;
        private TMP_Text m_targetText;
        private TMP_Text m_divesText;
        private UIShopSlotItem[] m_slots;
        private RectTransform m_goodsRect;
        private UIShopGoodsItem m_goodsTemplate;
        private Button m_refreshButton;
        private Button m_diveButton;
        private Button m_closeButton;

        // 复制出来的商品卡只开关不销毁，销毁会延到帧尾，同帧重建会被布局算进内容高度
        private readonly List<UIShopGoodsItem> m_goodsItems = new();
        private int m_goodsShown;

        // ==================== 生命周期 ====================
        protected override void OnInit()
        {
            m_coinsText = FindComponent<TMP_Text>(COINS_PATH);
            m_targetText = FindComponent<TMP_Text>(TARGET_PATH);
            m_divesText = FindComponent<TMP_Text>(DIVES_PATH);
            m_slots = FindComponent<RectTransform>(GRID_PATH).GetComponentsInChildren<UIShopSlotItem>();
            m_goodsRect = FindComponent<RectTransform>(GOODS_PATH);
            m_goodsTemplate = FindComponent<UIShopGoodsItem>(GOODS_TEMPLATE_PATH);
            m_refreshButton = FindComponent<Button>(REFRESH_PATH);
            m_diveButton = FindComponent<Button>(DIVE_PATH);
            m_closeButton = FindComponent<Button>(CLOSE_PATH);

#if UNITY_EDITOR
            if (m_slots.Length == 0)
                QLog.Error($"[{nameof(UIShopPanel)}] 背包网格里没有格子，请检查 {GRID_PATH}");
#endif

            m_refreshButton.onClick.AddListener(handleRefreshClicked);
            m_diveButton.onClick.AddListener(handleDiveClicked);
            m_closeButton.onClick.AddListener(handleCloseClicked);
        }

        protected override void OnOpen(params object[] args)
        {
            RunState run = args.Length > 0 ? args[0] as RunState : null;

#if UNITY_EDITOR
            if (run == null)
                QLog.Error($"[{nameof(UIShopPanel)}] 没有传入 RunState，面板留空");
#endif

            if (run == null)
                return;

            refreshCounters(run);
            fillBackpack();
            rebuildGoods();
        }

        // ==================== Private Function ====================
        private void refreshCounters(RunState run)
        {
            m_coinsText.text = $"金币 {run.Coins}";
            m_targetText.text = $"目标 {run.RoundValue} / {run.RoundTarget}";
            m_divesText.text = $"下潜 {run.DivesRemaining} 次";
        }

        // 占位：背包模块还没做，先摆三件物品把格子效果看出来，接入后由网格位图驱动
        private void fillBackpack()
        {
            for (int i = 0; i < m_slots.Length; i++)
                m_slots[i].SetEmpty();

            showSlot(0, "教学鱼竿", m_qualityWhite);
            showSlot(1, "延长鱼线", m_qualityBlue);
            showSlot(2, "幸运罗盘", m_qualityPurple);
        }

        private void showSlot(int index, string itemName, Color qualityColor)
        {
            if (index >= m_slots.Length)
            {
#if UNITY_EDITOR
                QLog.Error($"[{nameof(UIShopPanel)}] 格子下标越界：{index}，当前共 {m_slots.Length} 格");
#endif
                return;
            }

            m_slots[index].SetItem(itemName, qualityColor);
        }

        // 占位：商品池与售价都还没定，这里只是把一排卡片铺出来看版式
        private void rebuildGoods()
        {
            m_goodsShown = 0;

            showGoods("钓竿升阶", 120, m_qualityGold);
            showGoods("延长鱼线 +8 米", 30, m_qualityBlue);
            showGoods("背包扩容 +1 格", 60, m_qualityPurple);
            showGoods("金币加成 +10%", 90, m_qualityWhite);

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
