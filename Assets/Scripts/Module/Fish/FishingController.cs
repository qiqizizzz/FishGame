/*
 * ┌──────────────────────────────────┐
 * │  描    述: 钓鱼场景控制器，驱动单局流程与场景退出
 * │  类    名: FishingController.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 * 玩法钩子状态机、鱼群与结算在 M1-M3 接入，本文件先立住玩法场景的控制层入口
 */

using Framework.QTower.Common.Defines;
using Framework.QTower.Controller;
using Framework.QTower.Event;
using UnityEngine;
using Utils.log;

namespace Module.Fish
{
    public sealed class FishingController : BaseController
    {
        private const KeyCode EXIT_TO_MENU_KEY = KeyCode.Escape;

        protected override void OnInit()
        {
            QLog.Info("钓鱼场景控制器已注册");
        }

        public override void Tick(float deltaTime)
        {
            // 临时退出入口，M3 结算面板的「退出」按钮接上后改由按钮事件驱动
            if (Input.GetKeyDown(EXIT_TO_MENU_KEY))
                EventManager.PublishEvent(EventDefines.ReturnToMainMenuRequested);
        }
    }
}
