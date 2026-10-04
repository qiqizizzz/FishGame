/*
 * ┌──────────────────────────────────┐
 * │  描    述: 游戏场景控制器，负责启动游戏场景默认模块与界面
 * │  类    名: GameController.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 */

using System;
using Cysharp.Threading.Tasks;
using Framework.QTower;
using Framework.QTower.Common.Defines;
using Framework.QTower.Common.Scene;
using Framework.QTower.Controller;
using Framework.QTower.Event;
using UI.Common;
using UI.MainMenu;
using UI.Setting;
using UnityEngine;
using Utils.log;

namespace Game
{
    public sealed class GameController : BaseController
    {
        private const int MAIN_MENU_SORTING_ORDER = 0;
        private const int SETTINGS_SORTING_ORDER = 100;
        private const int LOADING_SORTING_ORDER = 200;

        // 遮罩要在场景切换完成后多等两帧再收，让新场景相机先出画
        private const int LOADING_SETTLE_FRAMES = 2;

        // 遮罩最短显示时间：加载太快收掉，转圈根本来不及被看见
        private const float LOADING_MIN_VISIBLE_SECONDS = 0.5f;

        // 等场景就绪信号的兜底超时，初始化炸了也不该把玩家永久关在黑屏里
        private const float LOADING_READY_TIMEOUT = 8f;

        // 场景侧发来的就绪信号，切场景前复位
        private bool m_sceneReady;

        // 注册游戏场景默认 HUD
        public GameController()
        {
            GameApp.UIManager.Register<UIMainMenu>(new UIData
            {
                Address = UIDefines.UIMainMenu,
                Parent = GameApp.UIManager.UIRoot,
                Controller = this,
                SortingOrder = MAIN_MENU_SORTING_ORDER
            });

            // 注册功能面板：设置界面
            GameApp.UIManager.Register<UISetting>(new UIData
            {
                Address = UIDefines.UISetting,
                Parent = GameApp.UIManager.UIRoot,
                Controller = this,
                SortingOrder = SETTINGS_SORTING_ORDER
            });

            // 注册切场景遮罩：必须压在所有界面之上
            GameApp.UIManager.Register<UILoadingPanel>(new UIData
            {
                Address = UIDefines.UILoadingPanel,
                Parent = GameApp.UIManager.UIRoot,
                Controller = this,
                SortingOrder = LOADING_SORTING_ORDER
            });
        }

        protected override void OnInit()
        {
            ShowMainMenu().Forget();
        }

        protected override void RegisterModuleEvent()
        {
            RegisterEvent(EventDefines.EnterFishingRequested, enterFishingScene);
            RegisterEvent(EventDefines.ReturnToMainMenuRequested, returnToMainMenu);
        }

        protected override void OnDestroy()
        {
            GameApp.UIManager.UnregisterByController(this);
        }

        #region 事件
        private async UniTaskVoid ShowMainMenu()
        {
            await GameApp.UIManager.Open<UIMainMenu>();
        }

        private void enterFishingScene() => switchToFishingScene().Forget();

        private void returnToMainMenu() => switchToMainMenuScene().Forget();

        private async UniTask switchToFishingScene()
        {
            GameApp.UIManager.Close<UIMainMenu>();

            if (await loadSceneWithLoading(SceneDefines.Fishing, true))
                return;

            await GameApp.UIManager.Open<UIMainMenu>();
        }

        private async UniTask switchToMainMenuScene()
        {
            if (!await loadSceneWithLoading(SceneDefines.Game))
                return;

            await GameApp.UIManager.Open<UIMainMenu>();
        }

        // 黑屏遮罩盖住整段空窗：开遮罩 → 加载 → 等场景就绪 → 补足最短显示 → 关遮罩
        // 鱼 prefab 预加载发生在场景加载完成之后，不等就绪信号遮罩就会提前收，HUD 慢好几拍
        private async UniTask<bool> loadSceneWithLoading(string sceneName, bool waitForSceneReady = false)
        {
            m_sceneReady = false;

            if (waitForSceneReady)
                EventManager.RegisterEvent(EventDefines.FishingSceneReady, new Action(onSceneReady));

            await GameApp.UIManager.Open<UILoadingPanel>();
            float visibleUntil = Time.unscaledTime + LOADING_MIN_VISIBLE_SECONDS;

            bool loaded = await SceneLoader.LoadAsync(sceneName);

            if (waitForSceneReady)
                await waitSceneReady();

            await holdLoadingUntil(visibleUntil);

            for (int frame = 0; frame < LOADING_SETTLE_FRAMES; frame++)
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);

            GameApp.UIManager.Close<UILoadingPanel>();

            if (waitForSceneReady)
                EventManager.UnregisterEvent(EventDefines.FishingSceneReady, new Action(onSceneReady));

            return loaded;
        }

        private void onSceneReady() => m_sceneReady = true;

        // 等场景侧初始化信号，超时兜底，别让遮罩无限期挂着
        private async UniTask waitSceneReady()
        {
            float deadline = Time.unscaledTime + LOADING_READY_TIMEOUT;

            while (!m_sceneReady && Time.unscaledTime < deadline)
                await UniTask.Yield(PlayerLoopTiming.Update);

#if UNITY_EDITOR
            if (!m_sceneReady)
                QLog.Warning("等待场景就绪超时，直接关闭加载遮罩");
#endif
        }

        // 补足最短显示时间，遮罩至少完整转一圈再收
        private async UniTask holdLoadingUntil(float untilUnscaledTime)
        {
            while (Time.unscaledTime < untilUnscaledTime)
                await UniTask.Yield(PlayerLoopTiming.Update);
        }

        #endregion
    }
}
