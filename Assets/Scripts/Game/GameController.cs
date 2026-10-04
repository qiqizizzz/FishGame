/*
 * ┌──────────────────────────────────┐
 * │  描    述: 游戏场景控制器，负责启动游戏场景默认模块与界面
 * │  类    名: GameController.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 */

using Cysharp.Threading.Tasks;
using Framework.QTower;
using Framework.QTower.Common.Defines;
using Framework.QTower.Common.Scene;
using Framework.QTower.Controller;
using UI.Common;
using UI.MainMenu;
using UI.Setting;

namespace Game
{
    public sealed class GameController : BaseController
    {
        private const int MAIN_MENU_SORTING_ORDER = 0;
        private const int SETTINGS_SORTING_ORDER = 100;
        private const int LOADING_SORTING_ORDER = 200;

        // 遮罩要在场景切换完成后多等两帧再收，让新场景相机先出画
        private const int LOADING_SETTLE_FRAMES = 2;

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

            if (await loadSceneWithLoading(SceneDefines.Fishing))
                return;

            await GameApp.UIManager.Open<UIMainMenu>();
        }

        private async UniTask switchToMainMenuScene()
        {
            if (!await loadSceneWithLoading(SceneDefines.Game))
                return;

            await GameApp.UIManager.Open<UIMainMenu>();
        }

        // 黑屏遮罩盖住整段切场景空窗，避免新场景首帧露出默认天空盒
        private async UniTask<bool> loadSceneWithLoading(string sceneName)
        {
            await GameApp.UIManager.Open<UILoadingPanel>();

            bool loaded = await SceneLoader.LoadAsync(sceneName);

            for (int frame = 0; frame < LOADING_SETTLE_FRAMES; frame++)
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);

            GameApp.UIManager.Close<UILoadingPanel>();
            return loaded;
        }
        #endregion
    }
}
