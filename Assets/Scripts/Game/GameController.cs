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

namespace Game
{
    public sealed class GameController : BaseController
    {

        // 注册游戏场景默认 HUD
        public GameController()
        {
            /*GameApp.UIManager.Register<UICombatHUD>(new UIData
            {
                Address = UIDefines.UICombatHUD,
                Parent = GameApp.UIManager.UIRoot,
                Controller = this,
                SortingOrder = 0
            });*/
        }

        protected override void OnInit()
        {
            
        }

        protected override void RegisterModuleEvent()
        {
            
        }

        // 玩家死亡后打开结算界面
        private void HandlePlayerDied()
        {
            
        }

        // 玩家重新挑战后关闭结算界面
        private void HandlePlayerRetryRequested()
        {
            
        }

        protected override void OnDestroy()
        {
            GameApp.UIManager.UnregisterByController(this);
        }
    }
}
