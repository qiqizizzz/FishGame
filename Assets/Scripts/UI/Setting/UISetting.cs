/*
 * ┌──────────────────────────────────┐
 * │  描    述: 设置界面，控制音量、静音与震动，并提供返回主菜单入口
 * │  类    名: UISetting.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 * 所有数值读写都走 GameSettings，这里只负责把控件状态与设置值互相同步
 * 震动行在不支持震动的平台（PC）上整行隐藏，避免留下点了没反应的开关
 */

using Framework.QTower.Common.Defines;
using Framework.QTower.Event;
using Framework.QTower.View;
using Game;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Setting
{
    public sealed class UISetting : UIBase
    {
        private const string MAIN_VOLUME_SLIDER_PATH = "Bg_Rows/Row_MainVolume/Sld_MainVolume";
        private const string BGM_VOLUME_SLIDER_PATH = "Bg_Rows/Row_BgmVolume/Sld_BgmVolume";
        private const string EFFECT_VOLUME_SLIDER_PATH = "Bg_Rows/Row_EffectVolume/Sld_EffectVolume";
        private const string MUTE_BUTTON_PATH = "Bg_Rows/Row_Mute/Btn_Mute";
        private const string VIBRATE_BUTTON_PATH = "Bg_Rows/Row_Vibrate/Btn_Vibrate";
        private const string VIBRATE_ROW_PATH = "Bg_Rows/Row_Vibrate";
        private const string CROSS_BUTTON_PATH = "Btn_Cross";
        private const string MAIN_MENU_BUTTON_PATH = "Btns/Btn_MainMenu";
        private const string CLOSE_BUTTON_PATH = "Btns/Btn_Close";

        [Header("开关按钮精灵")]
        [Tooltip("开关关闭时显示的开关精灵，静音与震动共用")]
        [SerializeField] private Sprite MuteOffSprite;

        [Tooltip("开关打开时显示的开关精灵，静音与震动共用")]
        [SerializeField] private Sprite MuteOnSprite;

        private Slider m_mainVolumeSlider;
        private Slider m_bgmVolumeSlider;
        private Slider m_effectVolumeSlider;
        private Button m_muteButton;
        private Button m_vibrateButton;
        private Image m_muteImage;
        private Image m_vibrateImage;

        protected override void OnInit()
        {
            m_mainVolumeSlider = FindComponent<Slider>(MAIN_VOLUME_SLIDER_PATH);
            m_bgmVolumeSlider = FindComponent<Slider>(BGM_VOLUME_SLIDER_PATH);
            m_effectVolumeSlider = FindComponent<Slider>(EFFECT_VOLUME_SLIDER_PATH);
            m_muteButton = FindComponent<Button>(MUTE_BUTTON_PATH);
            m_muteImage = FindComponent<Image>(MUTE_BUTTON_PATH);
            m_vibrateButton = FindComponent<Button>(VIBRATE_BUTTON_PATH);
            m_vibrateImage = FindComponent<Image>(VIBRATE_BUTTON_PATH);

            Button crossButton = FindComponent<Button>(CROSS_BUTTON_PATH);
            Button mainMenuButton = FindComponent<Button>(MAIN_MENU_BUTTON_PATH);
            Button closeButton = FindComponent<Button>(CLOSE_BUTTON_PATH);

            m_mainVolumeSlider.onValueChanged.AddListener(handleVolumeChanged);
            m_bgmVolumeSlider.onValueChanged.AddListener(handleVolumeChanged);
            m_effectVolumeSlider.onValueChanged.AddListener(handleVolumeChanged);
            m_muteButton.onClick.AddListener(handleMuteClicked);
            m_vibrateButton.onClick.AddListener(handleVibrateClicked);
            crossButton.onClick.AddListener(handleCloseClicked);
            mainMenuButton.onClick.AddListener(handleMainMenuClicked);
            closeButton.onClick.AddListener(handleCloseClicked);
        }

        protected override void OnOpen(params object[] args)
        {
            // PC 上震动没有意义，整行收起来，VerticalLayoutGroup 会自动补位
            FindNode(VIBRATE_ROW_PATH).gameObject.SetActive(GameSettings.IsVibrateSupported);
            refreshControls();
        }

        // 关面板即落盘，滑条拖动过程中不写磁盘
        protected override void OnClose(params object[] args)
        {
            GameSettings.Save();
        }

        // ==================== Private Function ====================
        private void handleVolumeChanged(float value)
        {
            GameSettings.MasterVolume = m_mainVolumeSlider.normalizedValue;
            GameSettings.BgmVolume = m_bgmVolumeSlider.normalizedValue;
            GameSettings.EffectVolume = m_effectVolumeSlider.normalizedValue;
        }

        // 切换全局静音，SoundManager 会同时暂停 BGM
        private void handleMuteClicked()
        {
            GameSettings.Muted = !GameSettings.Muted;
            refreshMuteVisual();
        }

        private void handleVibrateClicked()
        {
            GameSettings.Vibrate = !GameSettings.Vibrate;
            refreshVibrateVisual();

            // 当场震一下当试听，玩家不用为了确认开关生效再去钓一条
            GameSettings.VibrateIfEnabled();
        }

        private void handleCloseClicked()
        {
            GameApp.UIManager.Close<UISetting>();
        }

        // 回主菜单要先收掉自己，否则遮罩切完场景后设置面板还盖在主菜单上
        private void handleMainMenuClicked()
        {
            GameApp.UIManager.Close<UISetting>();
            EventManager.PublishEvent(EventDefines.ReturnToMainMenuRequested);
        }

        // 从 GameSettings 读回当前值回填控件，SetValueWithoutNotify 防止回调自触发
        private void refreshControls()
        {
            m_mainVolumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume * m_mainVolumeSlider.maxValue);
            m_bgmVolumeSlider.SetValueWithoutNotify(GameSettings.BgmVolume * m_bgmVolumeSlider.maxValue);
            m_effectVolumeSlider.SetValueWithoutNotify(GameSettings.EffectVolume * m_effectVolumeSlider.maxValue);
            refreshMuteVisual();
            refreshVibrateVisual();
        }

        private void refreshMuteVisual()
        {
            m_muteImage.sprite = GameSettings.Muted ? MuteOnSprite : MuteOffSprite;
        }

        private void refreshVibrateVisual()
        {
            m_vibrateImage.sprite = GameSettings.Vibrate ? MuteOnSprite : MuteOffSprite;
        }
    }
}
