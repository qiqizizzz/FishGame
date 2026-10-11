/*
 * ┌──────────────────────────────────┐
 * │  描    述: 全局音效播放器，负责 BGM、一次性音效与循环音效播放
 * │  类    名: SoundManager.cs       
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 */

using System.Collections.Generic;
using Common.Res;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Utils.log;

namespace Framework.QTower.Common.Sound
{
    public static class SoundManager
    {
        private const string SOUND_ROOT_NAME = "SoundManager";
        private const string BGM_SOURCE_NAME = "BgmSource";
        private const string EFFECT_SOURCE_NAME = "EffectSource";

        private static readonly Dictionary<string, AudioClip> m_clipDic = new Dictionary<string, AudioClip>();

        private static GameObject m_soundRoot;
        private static Transform m_effectRoot;
        private static AudioSource m_bgmSource;
        private static AudioSource m_effectSource;
        private static bool m_isStop;
        private static float m_totalVolume = 1f;
        private static float m_bgmVolume = 1f;
        private static float m_voiceVolume = 1f;
        private static float m_effectVolume = 1f;
        private static string m_bgmRes;
        private static float m_bgmScale = 1f;
        private static int m_bgmSeq;

        public static bool IsStop
        {
            get => m_isStop;
            set
            {
                EnsureAudioSources();
                m_isStop = value;
                if (m_isStop)
                    m_bgmSource.Pause();
                else if (!m_bgmSource.isPlaying && m_bgmSource.clip != null)
                    m_bgmSource.Play();
            }
        }

        public static float TotalVolume
        {
            get => m_totalVolume;
            set
            {
                m_totalVolume = Mathf.Clamp01(value);
                RefreshBgmVolume();
                RefreshEffectVolume();
            }
        }

        public static float BgmVolume
        {
            get => m_bgmVolume;
            set
            {
                m_bgmVolume = Mathf.Clamp01(value);
                RefreshBgmVolume();
            }
        }

        public static float VoiceVolume
        {
            get => m_voiceVolume;
            set => m_voiceVolume = Mathf.Clamp01(value);
        }

        public static float EffectVolume
        {
            get => m_effectVolume;
            set
            {
                m_effectVolume = Mathf.Clamp01(value);
                RefreshEffectVolume();
            }
        }

        /// <summary>
        /// 播放 Addressables 中配置的背景音乐
        /// </summary>
        /// <param name="res">Addressables 地址</param>
        /// <param name="volumeScale">相对全局 BGM 音量的倍率，用于单独压低某一首</param>
        public static void PlayBGM(string res, float volumeScale = 1f)
        {
            if (m_isStop)
                return;

            EnsureAudioSources();

            bool sameTrack = m_bgmRes == res && m_bgmSource.isPlaying;

            // 起停都算一代，排队中的旧淡出与旧加载从此作废
            m_bgmSeq++;
            m_bgmRes = res;
            m_bgmScale = Mathf.Clamp01(volumeScale);

            // 同一首还在响时不重开，只把音量拉回目标值，免得面板反复开关把 BGM 顶回开头
            if (sameTrack)
            {
                RefreshBgmVolume();
                return;
            }

            PlayBgmAsync(res).Forget();
        }

        /// <summary>
        /// 停止背景音乐，默认带一小段淡出，淡出期间若被新的 PlayBGM 接管则本次停止作废
        /// </summary>
        /// <param name="fadeSeconds">淡出时长，传 0 立即硬停</param>
        public static void StopBGM(float fadeSeconds = 0.25f)
        {
            if (m_isStop)
                return;

            EnsureAudioSources();
            m_bgmSeq++;
            m_bgmRes = null;

            if (fadeSeconds <= 0f || !m_bgmSource.isPlaying)
            {
                m_bgmSource.Stop();
                return;
            }

            FadeOutBgmAsync(m_bgmSeq, fadeSeconds).Forget();
        }

        // 在指定世界坐标播放 Addressables 中配置的一次性音效
        public static void PlayEffect(string res, Vector3 pos)
        {
            if (m_isStop)
                return;

            PlayEffectAsync(res, pos).Forget();
        }

        // 播放非空间化的一次性音效，UI 与经济类反馈走这条
        public static void PlayEffect(string res)
        {
            PlaySFX(res);
        }

        /// <summary>
        /// 播放非空间化的一次性音效，不受距离衰减影响
        /// </summary>
        /// <param name="res">Addressables 地址</param>
        /// <param name="volumeScale">相对全局音效音量的倍率</param>
        /// <param name="pitch">变调倍率，小于 1 更闷更沉</param>
        public static void PlaySFX(string res, float volumeScale = 1f, float pitch = 1f)
        {
            if (m_isStop)
                return;

            PlaySFXAsync(res, volumeScale, pitch).Forget();
        }

        // 在常驻音效源上叠加播放 UI 音效
        public static void PlayUIEffect(AudioClip clip, float volumeScale = 1f)
        {
            if (m_isStop || clip == null)
                return;

            EnsureAudioSources();
            m_effectSource.PlayOneShot(clip, Mathf.Max(0f, volumeScale));
        }

        // 在指定世界坐标播放一次性音效资源
        public static AudioSource PlayEffect(AudioClip clip, Vector3 position, float volumeScale = 1f,
            float pitch = 1f, bool spatial = true)
        {
            if (m_isStop || clip == null)
                return null;

            AudioSource audioSource = CreateRuntimeAudioSource(EFFECT_SOURCE_NAME, position, spatial);
            audioSource.clip = clip;
            audioSource.loop = false;
            audioSource.volume = Mathf.Clamp01(volumeScale) * m_effectVolume * m_totalVolume;
            audioSource.pitch = Mathf.Max(0.01f, pitch);
            audioSource.Play();
            Object.Destroy(audioSource.gameObject, clip.length / audioSource.pitch);
            return audioSource;
        }

        // 在指定父节点下播放循环音效资源
        public static AudioSource PlayLoop(AudioClip clip, Transform parent, Vector3 localPosition, float volumeScale = 1f,
            float pitch = 1f, bool spatial = true)
        {
            if (m_isStop || clip == null)
                return null;

            EnsureAudioSources();
            GameObject sourceObject = new GameObject(EFFECT_SOURCE_NAME);
            Transform sourceTransform = sourceObject.transform;
            if (parent == null)
            {
                sourceTransform.SetParent(m_effectRoot, false);
                sourceTransform.position = localPosition;
            }
            else
            {
                sourceTransform.SetParent(parent, false);
                sourceTransform.localPosition = localPosition;
            }

            sourceTransform.localRotation = Quaternion.identity;

            AudioSource audioSource = ConfigureAudioSource(sourceObject.AddComponent<AudioSource>(), spatial);
            audioSource.clip = clip;
            audioSource.loop = true;
            audioSource.volume = Mathf.Clamp01(volumeScale) * m_effectVolume * m_totalVolume;
            audioSource.pitch = Mathf.Max(0.01f, pitch);
            audioSource.Play();
            return audioSource;
        }

        // 停止并销毁指定音效播放源
        public static void Stop(AudioSource audioSource)
        {
            if (audioSource == null)
                return;

            audioSource.Stop();
            Object.Destroy(audioSource.gameObject);
        }

        // 异步加载并播放背景音乐
        private static async UniTaskVoid PlayBgmAsync(string res)
        {
            AudioClip clip = await LoadClipAsync(res);
            if (clip == null || m_isStop)
                return;

            // 排队期间又被切走的那首就作废，免得旧请求把新 BGM 盖掉
            if (m_bgmRes != res)
                return;

            EnsureAudioSources();
            m_bgmSource.clip = clip;
            RefreshBgmVolume();
            m_bgmSource.Play();
        }

        // 异步加载并播放一次性音效
        private static async UniTaskVoid PlayEffectAsync(string res, Vector3 pos)
        {
            AudioClip clip = await LoadClipAsync(res);
            if (clip == null || m_isStop)
                return;

            PlayEffect(clip, pos);
        }

        // 异步加载后在常驻音效源上非空间化播放
        private static async UniTaskVoid PlaySFXAsync(string res, float volumeScale, float pitch)
        {
            AudioClip clip = await LoadClipAsync(res);
            if (clip == null || m_isStop)
                return;

            PlayEffect(clip, Vector3.zero, volumeScale, pitch, false);
        }

        // 通过 Addressables 地址加载并缓存音频资源
        private static async UniTask<AudioClip> LoadClipAsync(string res)
        {
            if (m_clipDic.TryGetValue(res, out AudioClip cachedClip))
                return cachedClip;

            AudioClip clip = await ResManager.LoadAssetAsync<AudioClip>(res);
            if (clip == null)
            {
                QLog.Error($"音频资源不存在：{res}");
                return null;
            }

            if (!m_clipDic.ContainsKey(res))
                m_clipDic.Add(res, clip);

            return clip;
        }

        // 确保全局播放节点已创建
        private static void EnsureAudioSources()
        {
            if (m_soundRoot != null)
                return;

            m_soundRoot = new GameObject(SOUND_ROOT_NAME);
            Object.DontDestroyOnLoad(m_soundRoot);
            m_bgmSource = ConfigureAudioSource(m_soundRoot.AddComponent<AudioSource>(), false);
            m_bgmSource.loop = true;

            GameObject effectRootObject = new GameObject("Effects");
            effectRootObject.transform.SetParent(m_soundRoot.transform, false);
            m_effectRoot = effectRootObject.transform;
            m_effectSource = ConfigureAudioSource(effectRootObject.AddComponent<AudioSource>(), false);
        }

        // 刷新背景音乐最终音量
        private static void RefreshBgmVolume()
        {
            EnsureAudioSources();
            m_bgmSource.volume = m_bgmVolume * m_totalVolume * m_bgmScale;
        }

        // 刷新音效最终音量
        private static void RefreshEffectVolume()
        {
            EnsureAudioSources();
            m_effectSource.volume = m_effectVolume * m_totalVolume;
        }

        // 淡出到零再真停，中途被新的 PlayBGM 接管就静默退出，音量由对方决定
        private static async UniTaskVoid FadeOutBgmAsync(int seq, float fadeSeconds)
        {
            AudioSource source = m_bgmSource;
            float startVolume = source.volume;
            float elapsed = 0f;

            while (elapsed < fadeSeconds)
            {
                if (seq != m_bgmSeq || source == null)
                    return;

                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / fadeSeconds));
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (seq != m_bgmSeq || source == null)
                return;

            source.Stop();
            source.volume = startVolume;
        }

        // 创建一次性音效播放节点
        private static AudioSource CreateRuntimeAudioSource(string sourceName, Vector3 position, bool spatial)
        {
            EnsureAudioSources();
            GameObject sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(m_effectRoot, false);
            sourceObject.transform.position = position;
            return ConfigureAudioSource(sourceObject.AddComponent<AudioSource>(), spatial);
        }

        // 设置 AudioSource 的通用播放参数
        private static AudioSource ConfigureAudioSource(AudioSource audioSource, bool spatial)
        {
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = spatial ? 1f : 0f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 1f;
            audioSource.maxDistance = 18f;
            return audioSource;
        }
    }
}
