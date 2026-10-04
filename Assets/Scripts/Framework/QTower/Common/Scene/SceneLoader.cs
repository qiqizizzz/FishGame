/*
 * ┌──────────────────────────────────┐
 * │  描    述: 场景异步加载器，统一切场景流程与加载状态守卫
 * │  类    名: SceneLoader.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 * 外观模式：屏蔽 UnityEngine.SceneManagement 的 AsyncOperation 轮询细节，
 * 对外只暴露 UniTask 接口，与 ResManager 的异步资源加载保持同一套等待方式
 */

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utils.log;

namespace Framework.QTower.Common.Scene
{
    public static class SceneLoader
    {
        public static bool IsLoading { get; private set; }

        public static string ActiveSceneName => SceneManager.GetActiveScene().name;

        /// <summary>
        /// 异步加载场景，加载期间重复调用会被拒绝
        /// </summary>
        /// <param name="sceneName">场景名，取自 SceneDefines</param>
        /// <param name="mode">Single 替换当前场景，Additive 叠加保留当前场景</param>
        /// <param name="onProgress">加载进度回调，取值 0-1</param>
        /// <returns>加载成功返回 true</returns>
        public static async UniTask<bool> LoadAsync(
            string sceneName,
            LoadSceneMode mode = LoadSceneMode.Single,
            Action<float> onProgress = null)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                QLog.Error("场景加载失败：场景名为空");
                return false;
            }

            if (IsLoading)
            {
                QLog.Warning($"场景加载被拒绝：{sceneName}，当前已有场景正在加载");
                return false;
            }

#if UNITY_EDITOR
            if (getBuildScenePath(sceneName) == null)
            {
                QLog.Error($"场景加载失败：{sceneName} 未以启用状态加入 Build Settings");
                return false;
            }
#endif

            IsLoading = true;
            try
            {
                AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, mode);
                if (operation == null)
                {
                    QLog.Error($"场景加载失败：{sceneName} 加载句柄为空，请检查场景资源是否损坏");
                    return false;
                }

                while (!operation.isDone)
                {
                    onProgress?.Invoke(operation.progress);
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }

                onProgress?.Invoke(1f);
                return true;
            }
            finally
            {
                IsLoading = false;
            }
        }

#if UNITY_EDITOR
        // 查询场景是否已启用并加入 Build Settings，命中返回资源路径，否则返回 null
        private static string getBuildScenePath(string sceneName)
        {
            foreach (UnityEditor.EditorBuildSettingsScene scene in UnityEditor.EditorBuildSettings.scenes)
            {
                if (!scene.enabled)
                    continue;

                if (System.IO.Path.GetFileNameWithoutExtension(scene.path) == sceneName)
                    return scene.path;
            }

            return null;
        }
#endif
    }
}
