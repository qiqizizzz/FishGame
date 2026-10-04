/*
 * ┌──────────────────────────────────┐
 * │  描    述: 钓鱼场景入口，负责玩法控制器的注册与注销
 * │  类    名: FishingScene.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 * 场景销毁即代表离开玩法，控制器随场景生命周期注销，界面实例一并释放
 */

using Framework.QTower;
using Game;
using UnityEngine;

namespace Module.Fish
{
    public sealed class FishingScene : MonoBehaviour
    {
        private void Start()
        {
            if (GameApp.ControllerManager == null)
                return;

            GameApp.ControllerManager.Register(ControllerType.Fishing, new FishingController());
        }

        private void OnDestroy()
        {
            if (GameApp.ControllerManager == null)
                return;

            GameApp.ControllerManager.Unregister(ControllerType.Fishing);
        }
    }
}
