/*
* ┌──────────────────────────────────┐
* │  描    述: 钓鱼 HUD，显示回合渔获/剩余下潜/放线深度/钩上负载与当前阶段
* │  类    名: UIFishingHUD.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 数值全部从 FishingController 只读拉取，阶段与受罚走事件推送，HUD 不参与任何玩法判定
* 只有数字真的变了才拼字符串，稳态下每帧零分配
*/

using Framework.QTower.Common.Defines;
using Framework.QTower.View;
using Module.Fish;
using Module.Fish.Type;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utils.log;

namespace UI.RoundHUD
{
    public sealed class UIFishingHUD : UIBase
    {
        private const string PANEL_BG_PATH = "Panel_Counters/bg";
        private const string ROW_HOOK_PATH = "Panel_Counters/Row_Hook";
        private const string ROW_DIVE_PATH = "Panel_Counters/Row_Dive";
        private const string ROW_LINE_PATH = "Panel_Counters/Row_Line";
        private const string ROW_LOAD_PATH = "Panel_Counters/Row_Load";
        private const string PHASE_TEXT_PATH = "Txt_Phase";

        private const float PENALTY_FLASH_SECONDS = 0.45f;

        private static readonly Color PANEL_WARN_COLOR = new Color(0.47f, 0.12f, 0.14f, 0.74f);

        private FishingController m_controller;
        private Image m_panelImage;
        private Color m_panelBaseColor;
        private TMP_Text m_phaseText;
        private UIHudCounterItem m_valueCounter;
        private UIHudCounterItem m_lineCounter;
        private UIHudCounterItem m_diveCounter;
        private UIHudCounterItem m_loadCounter;

        private int m_lastCoins = -1;
        private int m_lastCoinsGoal = -1;
        private int m_lastDivesLeft = -1;
        private int m_lastDivesTotal = -1;
        private int m_lastDepth = -1;
        private int m_lastLoad = -1;
        private int m_lastLoadCeiling = -1;
        private float m_flashEndTime = -1f;

        protected override void OnInit()
        {
            m_controller = Controller as FishingController;
            // bg 是装饰件，取不到就跳过，不再因为它缺失而报错
            Transform panelBg = FindNode(PANEL_BG_PATH);
            m_panelImage = panelBg != null ? panelBg.GetComponent<Image>() : null;
            m_panelBaseColor = m_panelImage != null ? m_panelImage.color : Color.white;
            m_phaseText = FindComponent<TMP_Text>(PHASE_TEXT_PATH);
            m_valueCounter = bindCounter(ROW_HOOK_PATH, "渔获");
            m_diveCounter = bindCounter(ROW_DIVE_PATH, "下潜");
            m_lineCounter = bindCounter(ROW_LINE_PATH, "鱼线");
            m_loadCounter = bindCounter(ROW_LOAD_PATH, "负载");

#if UNITY_EDITOR
            if (m_controller == null)
                QLog.Error($"[{nameof(UIFishingHUD)}] Controller 不是 FishingController，HUD 取不到玩法数据");
#endif
        }

        protected override void OnOpen(params object[] args)
        {
            resetPanelColor();
            m_flashEndTime = -1f;
            m_lastCoins = m_lastCoinsGoal = m_lastDivesLeft = m_lastDivesTotal =
                m_lastDepth = m_lastLoad = m_lastLoadCeiling = -1;
        }

        protected override void SubscribeViewEvents()
        {
            RegisterEvent<HookPhase>(EventDefines.FishingPhaseChanged, handlePhaseChanged);
            RegisterEvent<PenaltyType>(EventDefines.FishingPenaltyApplied, handlePenaltyApplied);
        }

        private void Update()
        {
            // 回主菜单的瞬间控制器可能已销毁，这里属于运行时判空
            if (m_controller == null)
                return;

            refreshCounters();
            refreshFlash();
        }

        // ==================== Private Function ====================
        private void refreshCounters()
        {
            int coins = m_controller.RoundValue;
            int goal = m_controller.RoundTarget;

            if (coins != m_lastCoins || goal != m_lastCoinsGoal)
            {
                m_lastCoins = coins;
                m_lastCoinsGoal = goal;
                m_valueCounter.SetValue($"{coins} / {goal}");
            }

            int divesLeft = m_controller.DivesRemaining;
            int divesTotal = m_controller.DivesPerRound;

            if (divesLeft != m_lastDivesLeft || divesTotal != m_lastDivesTotal)
            {
                m_lastDivesLeft = divesLeft;
                m_lastDivesTotal = divesTotal;
                m_diveCounter.SetValue($"{divesLeft} / {divesTotal}");
            }

            int depth = (int)m_controller.Depth;

            if (depth != m_lastDepth)
            {
                m_lastDepth = depth;
                m_lineCounter.SetValue($"{depth} m");
            }

            int load = m_controller.CarriedCount;

            int ceiling = m_controller.MaxCarryCount;

            if (load != m_lastLoad || ceiling != m_lastLoadCeiling)
            {
                m_lastLoad = load;
                m_lastLoadCeiling = ceiling;
                m_loadCounter.SetValue($"{load} / {ceiling}");
            }
        }

        private void refreshFlash()
        {
            if (m_flashEndTime < 0f)
                return;

            if (Time.unscaledTime >= m_flashEndTime)
            {
                m_flashEndTime = -1f;
                resetPanelColor();
                return;
            }

            m_panelImage.color = PANEL_WARN_COLOR;
        }

        // 底色取预制体上的原始值，避免代码覆盖美术配置
        private void resetPanelColor()
        {
            if (m_panelImage == null)
                return;

            m_panelImage.color = m_panelBaseColor;
        }

        private UIHudCounterItem bindCounter(string path, string label)
        {
            UIHudCounterItem item = FindComponent<UIHudCounterItem>(path);
            item.Setup(label);
            return item;
        }

        private void handlePhaseChanged(HookPhase phase)
        {
            m_phaseText.text = phaseLabel(phase);
        }

        // 受罚就是"少赚了"，整块面板闪一下比盯着数字找变化更快
        private void handlePenaltyApplied(PenaltyType penalty)
        {
            if (m_panelImage == null)
                return;

            m_flashEndTime = Time.unscaledTime + PENALTY_FLASH_SECONDS;
        }

        private static string phaseLabel(HookPhase phase)
        {
            return phase switch
            {
                HookPhase.Descending => "下潜中",
                HookPhase.Reeling => "回收中",
                HookPhase.Tangled => "被缠住了",
                HookPhase.Landed => "出水",
                _ => "待命"
            };
        }
    }
}