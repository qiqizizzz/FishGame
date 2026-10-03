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

        private Slider _mainVolumeSlider;
        private Slider _effectVolumeSlider;
        private Button _muteButton;
        private Button _closeButton;
        private Image _muteImage;

        protected override void OnInit()
        {
            _mainVolumeSlider = FindComponent<Slider>(MAIN_VOLUME_SLIDER_PATH);
            _effectVolumeSlider = FindComponent<Slider>(EFFECT_VOLUME_SLIDER_PATH);
            _muteButton = FindComponent<Button>(MUTE_BUTTON_PATH);
            _muteImage = FindComponent<Image>(MUTE_BUTTON_PATH);
            _closeButton = FindComponent<Button>(CLOSE_BUTTON_PATH);

            _mainVolumeSlider.onValueChanged.AddListener(handleMainVolumeChanged);
            _effectVolumeSlider.onValueChanged.AddListener(handleEffectVolumeChanged);
            _muteButton.onClick.AddListener(handleMuteClicked);
            _closeButton.onClick.AddListener(handleCloseClicked);
        }

        protected override void OnOpen(params object[] args)
        {
            refreshFromSoundManager();
        }

        private void handleMainVolumeChanged(float value)
        {
            SoundManager.TotalVolume = _mainVolumeSlider.normalizedValue;
        }

        private void handleEffectVolumeChanged(float value)
        {
            SoundManager.EffectVolume = _effectVolumeSlider.normalizedValue;
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
            _mainVolumeSlider.SetValueWithoutNotify(SoundManager.TotalVolume * _mainVolumeSlider.maxValue);
            _effectVolumeSlider.SetValueWithoutNotify(SoundManager.EffectVolume * _effectVolumeSlider.maxValue);
            refreshMuteVisual();
        }

        private void refreshMuteVisual()
        {
            _muteImage.sprite = SoundManager.IsStop ? MuteOnSprite : MuteOffSprite;
        }
    }
}
