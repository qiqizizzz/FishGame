/*
* ┌──────────────────────────────────┐
* │  描    述: 钓鱼 HUD，显示渔获价值/放线深度/钩上负载与当前阶段
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

namespace UI.Fishing
{
    public sealed class UIFishingHUD : UIBase
    {
        private const string PANEL_PATH = "Panel_Counters";
        private const string ROW_HOOK_PATH = "Panel_Counters/Row_Hook";
        private const string ROW_LINE_PATH = "Panel_Counters/Row_Line";
        private const string ROW_LOAD_PATH = "Panel_Counters/Row_Load";
        private const string PHASE_TEXT_PATH = "Txt_Phase";

        private const float PENALTY_FLASH_SECONDS = 0.45f;

        private static readonly Color PANEL_BASE_COLOR = new Color(0.043f, 0.094f, 0.157f, 0.62f);
        private static readonly Color PANEL_WARN_COLOR = new Color(0.47f, 0.12f, 0.14f, 0.74f);

        private FishingController m_controller;
        private Image m_panelImage;
        private TMP_Text m_phaseText;
        private UIHudCounterItem m_valueCounter;
        private UIHudCounterItem m_lineCounter;
        private UIHudCounterItem m_loadCounter;

        private int m_lastCoins = -1;
        private int m_lastDepth = -1;
        private int m_lastLoad = -1;
        private int m_lastLoadCeiling = -1;
        private float m_flashEndTime = -1f;

        protected override void OnInit()
        {
            m_controller = Controller as FishingController;
            m_panelImage = FindComponent<Image>(PANEL_PATH);
            m_phaseText = FindComponent<TMP_Text>(PHASE_TEXT_PATH);
            m_valueCounter = bindCounter(ROW_HOOK_PATH, "渔获");
            m_lineCounter = bindCounter(ROW_LINE_PATH, "鱼线");
            m_loadCounter = bindCounter(ROW_LOAD_PATH, "负载");

#if UNITY_EDITOR
            if (m_controller == null)
                QLog.Error($"[{nameof(UIFishingHUD)}] Controller 不是 FishingController，HUD 取不到玩法数据");
#endif
        }

        protected override void OnOpen(params object[] args)
        {
            m_panelImage.color = PANEL_BASE_COLOR;
            m_flashEndTime = -1f;
            m_lastCoins = m_lastDepth = m_lastLoad = m_lastLoadCeiling = -1;
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
            int coins = m_controller.TotalCoins;

            if (coins != m_lastCoins)
            {
                m_lastCoins = coins;
                m_valueCounter.SetValue($"{coins} / {m_controller.TargetCoins}");
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
                m_panelImage.color = PANEL_BASE_COLOR;
                return;
            }

            m_panelImage.color = PANEL_WARN_COLOR;
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