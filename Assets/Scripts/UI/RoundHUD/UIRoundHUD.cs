/*
* ┌──────────────────────────────────┐
* │  描    述: 回合 HUD，常驻回合数、阶段切换横幅与右上角设置教程入口
* │  类    名: UIRoundHUD.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 回合数钉在屏幕顶部中间，整个回合一直挂着
* 阶段横幅只在切换那一瞬扫过一次：屏外右侧滑入居中、停一下、再向左划出屏外
* 出屏距离按面板根节点宽度实时换算，不在 prefab 里写死分辨率
*/

using Cysharp.Threading.Tasks;
using DG.Tweening;
using Framework.QTower.View;
using Game;
using TMPro;
using UI.Setting;
using UI.Tutorial;
using UnityEngine;
using UnityEngine.UI;
using Utils.log;

namespace UI.RoundHUD
{
    public sealed class UIRoundHUD : UIBase
    {
        private const string ROUND_TEXT_PATH = "Panel_Round/Txt_Round";
        private const string BANNER_PATH = "Banner";
        private const string BANNER_TEXT_PATH = "Banner/Txt_Phase";
        private const string SETTING_BUTTON_PATH = "Panel_SideButtons/Btn_Setting";
        private const string TUTORIAL_BUTTON_PATH = "Panel_SideButtons/Btn_Tutorial";

        private const int MIN_ROUND = 1;

        // ==================== 字段[外部设置] ====================
        [Header("横幅动画")]
        [Tooltip("横幅从屏外右侧滑到屏幕正中的时长（秒）")]
        [SerializeField] private float SlideInSeconds = 0.45f;

        [Tooltip("横幅停在屏幕正中的时长（秒）")]
        [SerializeField] private float HoldSeconds = 1.2f;

        [Tooltip("横幅从屏幕正中划出到屏外左侧的时长（秒）")]
        [SerializeField] private float SlideOutSeconds = 0.4f;

        [Tooltip("开场横幅的起飞等待（秒），留给切场景遮罩收场，否则整段动画会在黑屏里放完")]
        [SerializeField] private float RevealSeconds = 0.6f;

        [Tooltip("横幅停留时的纵向位置，0 为屏幕正中")]
        [SerializeField] private float BannerCenterY;

        // ==================== 属性 ====================
        /// <summary>当前展示的回合数，尚未开局时为 0</summary>
        public int Round => m_round;

        // ==================== 字段[私有] ====================
        private RectTransform m_rootRect;
        private RectTransform m_bannerRect;
        private TMP_Text m_roundText;
        private TMP_Text m_bannerText;

        private Sequence m_bannerSequence;
        private Tween m_revealTween;
        private int m_round;

        // ==================== 生命周期 ====================
        protected override void OnInit()
        {
            m_rootRect = (RectTransform)transform;
            m_roundText = FindComponent<TMP_Text>(ROUND_TEXT_PATH);
            m_bannerRect = FindComponent<RectTransform>(BANNER_PATH);
            m_bannerText = FindComponent<TMP_Text>(BANNER_TEXT_PATH);
            bindSideButtons();
        }

        protected override void OnOpen(params object[] args)
        {
            m_round = 0;
            m_roundText.text = string.Empty;
            killTweens();
            hideBanner();
        }

        protected override void OnClose(params object[] args)
        {
            killTweens();
            hideBanner();
        }

        // ==================== Public Function ====================
        /// <summary>
        /// 开一回合：立刻刷新顶部回合数，横幅等遮罩收场后再放
        /// </summary>
        /// <param name="round">回合数，从 1 开始</param>
        /// <param name="phase">本回合进入的阶段</param>
        public void StartRound(int round, GamePhase phase)
        {
#if UNITY_EDITOR
            if (round < MIN_ROUND)
                QLog.Error($"[{nameof(UIRoundHUD)}] 回合数非法：{round}，应为 {MIN_ROUND} 起的正整数");
#endif
            m_round = round;
            m_roundText.text = $"第 {round} 回合";

            killTweens();
            m_revealTween = DOVirtual.DelayedCall(RevealSeconds, () => playBanner(phase), true);
        }

        /// <summary>
        /// 立刻放一次阶段横幅，用于回合内商店与钓鱼之间的切换
        /// </summary>
        public void PlayPhaseBanner(GamePhase phase)
        {
            killTweens();
            playBanner(phase);
        }

        // ==================== Private Function ====================
        // 右上角常驻入口：设置与教程都注册在 UIManager 上，这里只做转发
        private void bindSideButtons()
        {
            Button settingButton = FindComponent<Button>(SETTING_BUTTON_PATH);
            Button tutorialButton = FindComponent<Button>(TUTORIAL_BUTTON_PATH);

            settingButton.onClick.AddListener(openSettingPanel);
            tutorialButton.onClick.AddListener(openTutorialPanel);
        }

        private void openSettingPanel()
        {
            GameApp.UIManager.Open<UISetting>().Forget();
        }

        private void openTutorialPanel()
        {
            GameApp.UIManager.Open<UITutorial>().Forget();
        }

        private void playBanner(GamePhase phase)
        {
            m_bannerText.text = phaseTitle(phase);

            // 面板根铺满屏幕，用它的宽度算出横幅完全出屏需要滑多远
            float travel = m_rootRect.rect.width * 0.5f + m_bannerRect.rect.width * 0.5f;
            Vector2 center = new Vector2(0f, BannerCenterY);

            m_bannerRect.anchoredPosition = center + Vector2.right * travel;
            m_bannerRect.gameObject.SetActive(true);

            m_bannerSequence = DOTween.Sequence();
            m_bannerSequence.SetUpdate(true);
            m_bannerSequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            m_bannerSequence.OnKill(handleBannerEnded);
            m_bannerSequence.Append(m_bannerRect.DOAnchorPos(center, SlideInSeconds).SetEase(Ease.OutQuint));
            m_bannerSequence.AppendInterval(HoldSeconds);
            m_bannerSequence.Append(m_bannerRect.DOAnchorPos(center - Vector2.right * travel, SlideOutSeconds)
                .SetEase(Ease.InQuint));
        }

        // Sequence 播完与被 Kill 都会走到这里，正好把横幅收掉避免留在屏上
        private void handleBannerEnded()
        {
            m_bannerSequence = null;
            hideBanner();
        }

        private void hideBanner()
        {
            m_bannerRect.gameObject.SetActive(false);
        }

        private void killTweens()
        {
            if (m_revealTween != null && m_revealTween.IsActive())
                m_revealTween.Kill();

            m_revealTween = null;

            if (m_bannerSequence != null && m_bannerSequence.IsActive())
                m_bannerSequence.Kill();
        }

        private static string phaseTitle(GamePhase phase)
        {
            return phase switch
            {
                GamePhase.Shop => "商店阶段",
                GamePhase.Fishing => "钓鱼阶段",
                _ => string.Empty
            };
        }
    }
}
