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

        private static readonly Dictionary<string, AudioClip> S_ClipDic = new Dictionary<string, AudioClip>();

        private static GameObject s_soundRoot;
        private static Transform s_effectRoot;
        private static AudioSource s_bgmSource;
        private static AudioSource s_effectSource;
        private static bool s_isStop;
        private static float s_totalVolume = 1f;
        private static float s_bgmVolume = 1f;
        private static float s_voiceVolume = 1f;
        private static float s_effectVolume = 1f;

        public static bool IsStop
        {
            get => s_isStop;
            set
            {
                EnsureAudioSources();
                s_isStop = value;
                if (s_isStop)
                    s_bgmSource.Pause();
                else if (!s_bgmSource.isPlaying && s_bgmSource.clip != null)
                    s_bgmSource.Play();
            }
        }

        public static float TotalVolume
        {
            get => s_totalVolume;
            set
            {
                s_totalVolume = Mathf.Clamp01(value);
                RefreshBgmVolume();
            }
        }

        public static float BgmVolume
        {
            get => s_bgmVolume;
            set
            {
                s_bgmVolume = Mathf.Clamp01(value);
                RefreshBgmVolume();
            }
        }

        public static float VoiceVolume
        {
            get => s_voiceVolume;
            set => s_voiceVolume = Mathf.Clamp01(value);
        }

        public static float EffectVolume
        {
            get => s_effectVolume;
            set => s_effectVolume = Mathf.Clamp01(value);
        }

        // 播放 Addressables 中配置的背景音乐
        public static void PlayBGM(string res)
        {
            if (s_isStop)
                return;

            PlayBgmAsync(res).Forget();
        }

        // 在指定世界坐标播放 Addressables 中配置的一次性音效
        public static void PlayEffect(string res, Vector3 pos)
        {
            if (s_isStop)
                return;

            PlayEffectAsync(res, pos).Forget();
        }

        // 播放非空间化的一次性音效
        public static void PlayEffect(string res)
        {
            PlayEffect(res, Vector3.zero);
        }

        // 在指定世界坐标播放一次性音效资源
        public static AudioSource PlayEffect(AudioClip clip, Vector3 position, float volumeScale = 1f,
            float pitch = 1f, bool spatial = true)
        {
            if (s_isStop || clip == null)
                return null;

            AudioSource audioSource = CreateRuntimeAudioSource(EFFECT_SOURCE_NAME, position, spatial);
            audioSource.clip = clip;
            audioSource.loop = false;
            audioSource.volume = Mathf.Clamp01(volumeScale) * s_effectVolume * s_totalVolume;
            audioSource.pitch = Mathf.Max(0.01f, pitch);
            audioSource.Play();
            Object.Destroy(audioSource.gameObject, clip.length / audioSource.pitch);
            return audioSource;
        }

        // 在指定父节点下播放循环音效资源
        public static AudioSource PlayLoop(AudioClip clip, Transform parent, Vector3 localPosition, float volumeScale = 1f,
            float pitch = 1f, bool spatial = true)
        {
            if (s_isStop || clip == null)
                return null;

            EnsureAudioSources();
            GameObject sourceObject = new GameObject(EFFECT_SOURCE_NAME);
            Transform sourceTransform = sourceObject.transform;
            if (parent == null)
            {
                sourceTransform.SetParent(s_effectRoot, false);
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
            audioSource.volume = Mathf.Clamp01(volumeScale) * s_effectVolume * s_totalVolume;
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
            if (clip == null || s_isStop)
                return;

            EnsureAudioSources();
            s_bgmSource.clip = clip;
            s_bgmSource.Play();
        }

        // 异步加载并播放一次性音效
        private static async UniTaskVoid PlayEffectAsync(string res, Vector3 pos)
        {
            AudioClip clip = await LoadClipAsync(res);
            if (clip == null || s_isStop)
                return;

            PlayEffect(clip, pos);
        }

        // 通过 Addressables 地址加载并缓存音频资源
        private static async UniTask<AudioClip> LoadClipAsync(string res)
        {
            if (S_ClipDic.TryGetValue(res, out AudioClip cachedClip))
                return cachedClip;

            AudioClip clip = await ResManager.LoadAssetAsync<AudioClip>(res);
            if (clip == null)
            {
                QLog.Error($"音频资源不存在：{res}");
                return null;
            }

            if (!S_ClipDic.ContainsKey(res))
                S_ClipDic.Add(res, clip);

            return clip;
        }

        // 确保全局播放节点已创建
        private static void EnsureAudioSources()
        {
            if (s_soundRoot != null)
                return;

            s_soundRoot = new GameObject(SOUND_ROOT_NAME);
            Object.DontDestroyOnLoad(s_soundRoot);
            s_bgmSource = ConfigureAudioSource(s_soundRoot.AddComponent<AudioSource>(), false);
            s_bgmSource.loop = true;

            GameObject effectRootObject = new GameObject("Effects");
            effectRootObject.transform.SetParent(s_soundRoot.transform, false);
            s_effectRoot = effectRootObject.transform;
            s_effectSource = ConfigureAudioSource(effectRootObject.AddComponent<AudioSource>(), false);
        }

        // 刷新背景音乐最终音量
        private static void RefreshBgmVolume()
        {
            EnsureAudioSources();
            s_bgmSource.volume = s_bgmVolume * s_totalVolume;
        }

        // 创建一次性音效播放节点
        private static AudioSource CreateRuntimeAudioSource(string sourceName, Vector3 position, bool spatial)
        {
            EnsureAudioSources();
            GameObject sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(s_effectRoot, false);
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
