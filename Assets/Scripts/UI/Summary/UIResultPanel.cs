/*
* ┌──────────────────────────────────┐
* │  描    述: 收竿结算面板，汇总本局收益与当前回合的捕获明细并提供再来一局
* │  类    名: UIResultPanel.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 数据由 FishingController 在出水封盘后直接传进来，面板不回查任何玩法对象
* 行由面板自带的隐藏模板复制并留用，条数不设上限，超出可视区交给 Scroll_List 滚动
*/

using System.Collections.Generic;
using Framework.QTower.Common.Defines;
using Framework.QTower.Event;
using Framework.QTower.View;
using Module.Fish.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utils.log;

namespace UI.Summary
{
    public sealed class UIResultPanel : UIBase
    {
        private const string TITLE_PATH = "Bg/Txt_Title";
        private const string SUMMARY_PATH = "Bg/Txt_Summary";
        private const string COINS_PATH = "Bg/Txt_Coins";
        private const string SCROLL_PATH = "Bg/Scroll_List";
        private const string CONTENT_PATH = "Bg/Scroll_List/Viewport/Content_Rows";
        private const string TEMPLATE_PATH = "Bg/Scroll_List/Viewport/Content_Rows/Row_Template";
        private const string AGAIN_PATH = "Bg/Btns/Btn_Again";
        private const string MENU_PATH = "Bg/Btns/Btn_Menu";

        private TMP_Text m_titleText;
        private TMP_Text m_summaryText;
        private TMP_Text m_coinsText;
        private ScrollRect m_scrollRect;
        private RectTransform m_content;
        private UIResultFishItem m_template;
        private Button m_againButton;
        private Button m_menuButton;

        // 复制出来的行只开关不销毁，销毁会延到帧尾，同帧重建会被布局算进内容高度
        private readonly List<UIResultFishItem> m_rows = new();

        protected override void OnInit()
        {
            m_titleText = FindComponent<TMP_Text>(TITLE_PATH);
            m_summaryText = FindComponent<TMP_Text>(SUMMARY_PATH);
            m_coinsText = FindComponent<TMP_Text>(COINS_PATH);
            m_scrollRect = FindComponent<ScrollRect>(SCROLL_PATH);
            m_content = FindComponent<RectTransform>(CONTENT_PATH);
            m_template = FindComponent<UIResultFishItem>(TEMPLATE_PATH);
            m_againButton = FindComponent<Button>(AGAIN_PATH);
            m_menuButton = FindComponent<Button>(MENU_PATH);

            m_againButton.onClick.AddListener(handleAgainClicked);
            m_menuButton.onClick.AddListener(handleMenuClicked);
        }

        protected override void OnOpen(params object[] args)
        {
            RunState run = args.Length > 0 ? args[0] as RunState : null;

#if UNITY_EDITOR
            if (run == null)
                QLog.Error($"[{nameof(UIResultPanel)}] 没有传入 RunState，面板留空");
#endif

            if (run == null)
                return;

            m_titleText.text = $"第 {run.Round} 回合收竿";
            m_summaryText.text = $"累计捕获 {run.TotalCatch} 条　·　最深 {run.MaxDepth:0} 米"
                + $"　·　渔获 {run.RoundValue} / {run.RoundTarget}　·　折算入账 +{run.RoundCoins} 金币";
            m_coinsText.text = $"金币 {run.Coins}";

            rebuildRows(run);
        }

        // ==================== Private Function ====================
        // 只铺当前回合的明细，整局鱼串起来能到上百条，回合之间不做累加展示
        private void rebuildRows(RunState run)
        {
            int count = run.RoundItems.Count;

            while (m_rows.Count < count)
            {
                // 模板本身是隐藏的，复制出来的行要先激活才会走 Awake 绑引用
                UIResultFishItem row = Instantiate(m_template, m_content);
                m_rows.Add(row);
            }

            for (int i = 0; i < m_rows.Count; i++)
            {
                bool visible = i < count;
                m_rows[i].gameObject.SetActive(visible);

                if (visible)
                    m_rows[i].Setup(run.RoundItems[i]);
            }

            scrollToTop();
        }

        // 重铺之后把列表拉回顶部，否则会停在上一回合的滚动位置
        // 高度先压回 0 逼 ContentSizeFitter 按当前可见行数重算，位置直接写 anchoredPosition：
        // verticalNormalizedPosition 要等 ScrollRect 的 LateUpdate 算出 bounds 才生效，同帧赋值会被吃掉
        private void scrollToTop()
        {
            m_content.sizeDelta = new Vector2(m_content.sizeDelta.x, 0f);
            LayoutRebuilder.MarkLayoutForRebuild(m_content);
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_content);

            m_content.anchoredPosition = new Vector2(m_content.anchoredPosition.x, 0f);
            m_scrollRect.velocity = Vector2.zero;
        }

        private void handleAgainClicked()
        {
            EventManager.PublishEvent(EventDefines.FishingSessionRestartRequested);
        }

        private void handleMenuClicked()
        {
            EventManager.PublishEvent(EventDefines.ReturnToMainMenuRequested);
        }
    }
}