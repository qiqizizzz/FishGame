/*
* ┌──────────────────────────────────┐
* │  描    述: 本局结算面板，汇总捕获明细、收益与最大深度并提供再来一局
* │  类    名: UIResultPanel.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 数据由 FishingController 在出水封盘后直接传进来，面板不回查任何玩法对象
* 行用面板自带的隐藏模板复制，最多十来条，不值得为它单独做池与地址注册
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
        private const string CONTENT_PATH = "Bg/Content_Rows";
        private const string TEMPLATE_PATH = "Bg/Content_Rows/Row_Template";
        private const string AGAIN_PATH = "Bg/Btns/Btn_Again";
        private const string MENU_PATH = "Bg/Btns/Btn_Menu";

        private TMP_Text m_titleText;
        private TMP_Text m_summaryText;
        private TMP_Text m_coinsText;
        private Transform m_content;
        private UIResultFishItem m_template;
        private Button m_againButton;
        private Button m_menuButton;

        private readonly List<UIResultFishItem> m_rows = new();

        protected override void OnInit()
        {
            m_titleText = FindComponent<TMP_Text>(TITLE_PATH);
            m_summaryText = FindComponent<TMP_Text>(SUMMARY_PATH);
            m_coinsText = FindComponent<TMP_Text>(COINS_PATH);
            m_content = FindNode(CONTENT_PATH);
            m_template = FindComponent<UIResultFishItem>(TEMPLATE_PATH);
            m_againButton = FindComponent<Button>(AGAIN_PATH);
            m_menuButton = FindComponent<Button>(MENU_PATH);

            m_againButton.onClick.AddListener(handleAgainClicked);
            m_menuButton.onClick.AddListener(handleMenuClicked);
        }

        protected override void OnOpen(params object[] args)
        {
            SessionResult result = args.Length > 0 ? args[0] as SessionResult : null;

#if UNITY_EDITOR
            if (result == null)
                QLog.Error($"[{nameof(UIResultPanel)}] 没有传入 SessionResult，面板留空");
#endif

            if (result == null)
                return;

            m_titleText.text = "本次收获";
            m_summaryText.text = $"下潜深度 {result.MaxDepth:0} 米　·　捕获 {result.TotalCatch} 条"
                + $"　·　渔获 {result.TotalCoins} / {result.TargetCoins}"
                + (result.IsTargetReached ? "（已达标）" : string.Empty);
            m_coinsText.text = $"金币 +{result.TotalCoins}";

            rebuildRows(result);
        }

        // ==================== Private Function ====================
        private void rebuildRows(SessionResult result)
        {
            for (int i = m_rows.Count - 1; i >= 0; i--)
                Destroy(m_rows[i].gameObject);

            m_rows.Clear();

            for (int i = 0; i < result.Items.Count; i++)
            {
                UIResultFishItem row = Instantiate(m_template, m_content);

                // 模板是隐藏的，先激活让 Awake 绑好引用，再填数据
                row.gameObject.SetActive(true);
                row.Setup(result.Items[i]);
                m_rows.Add(row);
            }
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