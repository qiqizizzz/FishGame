/*
 * ┌──────────────────────────────────┐
 * │  描    述: 设置界面，控制主音量、音效音量与全局静音
 * │  类    名: UISetting.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 */

using Framework.QTower.Common.Sound;
using Framework.QTower.View;
using Game;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Setting
{
    public sealed class UISetting : UIBase
    {
        private const string MAIN_VOLUME_SLIDER_PATH = "Bg_Rows/Row_MainVolume/Sld_MainVolume";
        private const string EFFECT_VOLUME_SLIDER_PATH = "Bg_Rows/Row_EffectVolume/Sld_EffectVolume";
        private const string MUTE_BUTTON_PATH = "Bg_Rows/Row_Mute/Btn_Mute";
        private const string CLOSE_BUTTON_PATH = "Btn_Close";

        [Header("静音按钮精灵")]
        [Tooltip("未静音时显示的开关精灵")]
        [SerializeField] private Sprite MuteOffSprite;

        [Tooltip("已静音时显示的开关精灵")]
        [SerializeField] private Sprite MuteOnSprite;

        private Slider m_mainVolumeSlider;
        private Slider m_effectVolumeSlider;
        private Button m_muteButton;
        private Button m_closeButton;
        private Image m_muteImage;

        protected override void OnInit()
        {
            m_mainVolumeSlider = FindComponent<Slider>(MAIN_VOLUME_SLIDER_PATH);
            m_effectVolumeSlider = FindComponent<Slider>(EFFECT_VOLUME_SLIDER_PATH);
            m_muteButton = FindComponent<Button>(MUTE_BUTTON_PATH);
            m_muteImage = FindComponent<Image>(MUTE_BUTTON_PATH);
            m_closeButton = FindComponent<Button>(CLOSE_BUTTON_PATH);

            m_mainVolumeSlider.onValueChanged.AddListener(handleMainVolumeChanged);
            m_effectVolumeSlider.onValueChanged.AddListener(handleEffectVolumeChanged);
            m_muteButton.onClick.AddListener(handleMuteClicked);
            m_closeButton.onClick.AddListener(handleCloseClicked);
        }

        protected override void OnOpen(params object[] args)
        {
            refreshFromSoundManager();
        }

        private void handleMainVolumeChanged(float value)
        {
            SoundManager.TotalVolume = m_mainVolumeSlider.normalizedValue;
        }

        private void handleEffectVolumeChanged(float value)
        {
            SoundManager.EffectVolume = m_effectVolumeSlider.normalizedValue;
        }

        // 切换全局静音，SoundManager 同时暂停 BGM
        private void handleMuteClicked()
        {
            SoundManager.IsStop = !SoundManager.IsStop;
            refreshMuteVisual();
        }

        private void handleCloseClicked()
        {
            GameApp.UIManager.Close<UISetting>();
        }

        // 从 SoundManager 读回当前音量与静音状态，回填到控件
        private void refreshFromSoundManager()
        {
            m_mainVolumeSlider.SetValueWithoutNotify(SoundManager.TotalVolume * m_mainVolumeSlider.maxValue);
            m_effectVolumeSlider.SetValueWithoutNotify(SoundManager.EffectVolume * m_effectVolumeSlider.maxValue);
            refreshMuteVisual();
        }

        private void refreshMuteVisual()
        {
            m_muteImage.sprite = SoundManager.IsStop ? MuteOnSprite : MuteOffSprite;
        }
    }
}
