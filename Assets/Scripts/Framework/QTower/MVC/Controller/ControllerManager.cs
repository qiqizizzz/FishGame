/*
 * ┌──────────────────────────────────┐
 * │  描    述: 控制器管理器，统一驱动控制器生命周期
 * │  类    名: ControllerManager.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 */

using System.Collections.Generic;

namespace Framework.QTower.Controller
{
    public sealed class ControllerManager
    {
        private readonly List<BaseController> m_controllers = new();
        private readonly Dictionary<ControllerType, BaseController> m_controllerMap = new();

        public T Register<T>(T controller) where T : BaseController
        {
            m_controllers.Add(controller);
            controller.Init();
            return controller;
        }

        // 注册带全局类型标识的控制器
        public T Register<T>(ControllerType controllerType, T controller) where T : BaseController
        {
            if (m_controllerMap.TryGetValue(controllerType, out BaseController registeredController))
                return registeredController as T;

            m_controllerMap.Add(controllerType, controller);
            return Register(controller);
        }

        // 获取指定类型的全局控制器
        public T Get<T>(ControllerType controllerType) where T : BaseController
        {
            return Get(controllerType) as T;
        }

        // 获取指定类型的全局控制器
        public BaseController Get(ControllerType controllerType)
        {
            m_controllerMap.TryGetValue(controllerType, out BaseController controller);
            return controller;
        }

        // 注销指定类型的全局控制器
        public bool Unregister(ControllerType controllerType)
        {
            if (!m_controllerMap.TryGetValue(controllerType, out BaseController controller))
                return false;

            m_controllerMap.Remove(controllerType);
            m_controllers.Remove(controller);
            controller.Destroy();
            return true;
        }

        // 驱动全部已注册控制器
        public void Tick(float deltaTime)
        {
            foreach (BaseController controller in m_controllers)
                controller.Tick(deltaTime);
        }

        // 按注册逆序销毁控制器
        public void Destroy()
        {
            for (int i = m_controllers.Count - 1; i >= 0; i--)
                m_controllers[i].Destroy();

            m_controllerMap.Clear();
            m_controllers.Clear();
        }
    }
}
