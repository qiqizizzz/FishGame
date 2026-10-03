/*
 * ┌──────────────────────────────────┐
 * │  描    述: UI 音效组件，点击与悬停自动播放，共用 SoundManager 常驻音效源
 * │  类    名: UIAudio.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 */

using Framework.QTower.Common.Sound;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Framework.QTower.View
{
    [DisallowMultipleComponent]
    public sealed class UIAudio : MonoBehaviour, IPointerEnterHandler
    {
        [Header("音效")]
        [Tooltip("点击时播放的音效，留空则不播")]
        [SerializeField] private AudioClip ClickClip;

        [Tooltip("指针悬停时播放的音效，留空则不播")]
        [SerializeField] private AudioClip HoverClip;

        [Tooltip("相对全局音效音量的倍率")]
        [SerializeField, Range(0f, 2f)] private float VolumeScale = 1f;

        private Button m_button;

        private void Awake()
        {
            // Button 属可选依赖，允许挂在非按钮物体上只播悬停音
            TryGetComponent(out m_button);
            if (m_button != null)
                m_button.onClick.AddListener(handleClick);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            play(HoverClip);
        }

        private void handleClick()
        {
            play(ClickClip);
        }

        // 交给 SoundManager 的常驻音效源播放，本组件不持有 AudioSource
        private void play(AudioClip clip)
        {
            SoundManager.PlayUIEffect(clip, VolumeScale);
        }
    }
}
