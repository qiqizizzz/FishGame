/*
* ┌──────────────────────────────────┐
* │  描    述: 全局玩家设置，音量/静音/震动的持久化与生效
* │  类    名: GameSettings.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 设置界面只负责改这里的值，真正落地交给 SoundManager 与 Handheld
* 音量改动即时生效，但延迟到面板关闭才写盘，免得拖动滑条时每帧落磁盘
*/

using Framework.QTower.Common.Sound;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 全局玩家设置：从 PlayerPrefs 读出音量与开关，改动时同步给音频系统
    /// </summary>
    public static class GameSettings
    {
        // ==================== 常量与静态字段 ====================
        private const string MASTER_VOLUME_KEY = "Settings_MasterVolume";
        private const string BGM_VOLUME_KEY = "Settings_BgmVolume";
        private const string EFFECT_VOLUME_KEY = "Settings_EffectVolume";
        private const string MUTE_KEY = "Settings_Mute";
        private const string VIBRATE_KEY = "Settings_Vibrate";

        private const float DEFAULT_VOLUME = 1f;
        private const int DEFAULT_VIBRATE = 1;

        // ==================== 字段[私有] ====================
        private static float m_masterVolume = DEFAULT_VOLUME;
        private static float m_bgmVolume = DEFAULT_VOLUME;
        private static float m_effectVolume = DEFAULT_VOLUME;
        private static bool m_muted;
        private static bool m_vibrate = DEFAULT_VIBRATE == 1;
        private static bool m_loaded;

        // ==================== 属性 ====================
        /// <summary>主音量（0~1），同时作用于 BGM 与音效</summary>
        public static float MasterVolume
        {
            get
            {
                ensureLoaded();
                return m_masterVolume;
            }
            set
            {
                ensureLoaded();
                m_masterVolume = Mathf.Clamp01(value);
                SoundManager.TotalVolume = m_masterVolume;
            }
        }

        /// <summary>音乐音量（0~1），只作用于 BGM 通道</summary>
        public static float BgmVolume
        {
            get
            {
                ensureLoaded();
                return m_bgmVolume;
            }
            set
            {
                ensureLoaded();
                m_bgmVolume = Mathf.Clamp01(value);
                SoundManager.BgmVolume = m_bgmVolume;
            }
        }

        /// <summary>音效音量（0~1），只作用于一次性音效与 UI 音</summary>
        public static float EffectVolume
        {
            get
            {
                ensureLoaded();
                return m_effectVolume;
            }
            set
            {
                ensureLoaded();
                m_effectVolume = Mathf.Clamp01(value);
                SoundManager.EffectVolume = m_effectVolume;
            }
        }

        /// <summary>全局静音，开启时 SoundManager 会一并暂停 BGM</summary>
        public static bool Muted
        {
            get
            {
                ensureLoaded();
                return m_muted;
            }
            set
            {
                ensureLoaded();
                m_muted = value;
                SoundManager.IsStop = m_muted;
            }
        }

        /// <summary>震动开关，只有支持震动的平台才会真的震动</summary>
        public static bool Vibrate
        {
            get
            {
                ensureLoaded();
                return m_vibrate;
            }
            set
            {
                ensureLoaded();
                m_vibrate = value;
            }
        }

        /// <summary>当前运行平台是否支持震动反馈</summary>
        public static bool IsVibrateSupported => Application.platform == RuntimePlatform.Android
            || Application.platform == RuntimePlatform.IPhonePlayer;

        // ==================== Public Function ====================
        /// <summary>
        /// 把内存中的设置重新灌给音频系统，启动时与设置面板打开时各用一次
        /// </summary>
        public static void ApplyToAudio()
        {
            SoundManager.TotalVolume = MasterVolume;
            SoundManager.BgmVolume = BgmVolume;
            SoundManager.EffectVolume = EffectVolume;
            SoundManager.IsStop = Muted;
        }

        /// <summary>
        /// 写盘保存当前设置，面板关闭时调用一次即可
        /// </summary>
        public static void Save()
        {
            ensureLoaded();

            PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, m_masterVolume);
            PlayerPrefs.SetFloat(BGM_VOLUME_KEY, m_bgmVolume);
            PlayerPrefs.SetFloat(EFFECT_VOLUME_KEY, m_effectVolume);
            PlayerPrefs.SetInt(MUTE_KEY, m_muted ? 1 : 0);
            PlayerPrefs.SetInt(VIBRATE_KEY, m_vibrate ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 按设置决定是否震动，上钩与受罚这类强反馈处调用
        /// </summary>
        public static void VibrateIfEnabled()
        {
            if (!Vibrate || !IsVibrateSupported)
                return;

#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        // ==================== Private Function ====================
        // 首次访问才从 PlayerPrefs 读，之后一律走内存值
        private static void ensureLoaded()
        {
            if (m_loaded)
                return;

            m_masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, DEFAULT_VOLUME));
            m_bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BGM_VOLUME_KEY, DEFAULT_VOLUME));
            m_effectVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(EFFECT_VOLUME_KEY, DEFAULT_VOLUME));
            m_muted = PlayerPrefs.GetInt(MUTE_KEY, 0) != 0;
            m_vibrate = PlayerPrefs.GetInt(VIBRATE_KEY, DEFAULT_VIBRATE) != 0;
            m_loaded = true;
        }

        // 首个场景加载完就把存档里的音量接上，早于任何界面与音频播放
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void applyOnStartup()
        {
            ApplyToAudio();
        }
    }
}
