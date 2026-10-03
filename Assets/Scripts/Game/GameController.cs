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
using Framework.QTower.Controller;
using UI.MainMenu;

namespace Game
{
    public sealed class GameController : BaseController
    {

        // 注册游戏场景默认 HUD
        public GameController()
        {
            GameApp.UIManager.Register<UIMainMenu>(new UIData
            {
                Address = UIDefines.UIMainMenu,
                Parent = GameApp.UIManager.UIRoot,
                Controller = this,
                SortingOrder = 0
            });
        }

        protected override void OnInit()
        {
            ShowMainMenu().Forget();
        }
        
        protected override void RegisterModuleEvent()
        {
            
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
        #endregion
    }
}
