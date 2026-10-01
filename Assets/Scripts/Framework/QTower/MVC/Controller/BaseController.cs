/*
 * ┌──────────────────────────────────┐
 * │  描    述: 控制器基类，统一控制器生命周期
 * │  类    名: BaseController.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 */

using System;
using System.Collections.Generic;
using Framework.QTower.Event;
using Framework.QTower.View;

namespace Framework.QTower.Controller
{
    public abstract class BaseController
    {
        private readonly Dictionary<string, Action> m_events = new();

        public void Init()
        {
            OnInit();
            RegisterModuleEvent();
        }

        public void Destroy()
        {
            RemoveModuleEvent();
            OnDestroy();
        }

        #region 生命周期
        protected virtual void OnInit()
        {
        }

        public virtual void Tick(float deltaTime)
        {
            
        }

        protected virtual void OnDestroy()
        {
        }
        #endregion

        public void RegisterEvent<TEvent>(string eventName, Action<TEvent> callback)
        {
            EventManager.RegisterEvent(eventName, callback);
            Action removeEvent = () => EventManager.UnregisterEvent(eventName, callback);

            if (m_events.TryGetValue(eventName, out Action registered))
            {
                m_events[eventName] = registered + removeEvent;
                return;
            }

            m_events.Add(eventName, removeEvent);
        }

        // 注册由控制器托管的无参数事件
        public void RegisterEvent(string eventName, Action callback)
        {
            EventManager.RegisterEvent(eventName, callback);
            Action removeEvent = () => EventManager.UnregisterEvent(eventName, callback);

            if (m_events.TryGetValue(eventName, out Action registered))
            {
                m_events[eventName] = registered + removeEvent;
                return;
            }

            m_events.Add(eventName, removeEvent);
        }

        public void UnregisterEvent(string eventName)
        {
            if (!m_events.TryGetValue(eventName, out Action removeEvent))
                return;

            removeEvent.Invoke();
            m_events.Remove(eventName);
        }

        protected virtual void RegisterModuleEvent()
        {
        }

        public virtual void OnViewLoaded(UIBase view)
        {
        }

        public virtual void OnViewOpened(UIBase view)
        {
        }

        public virtual void OnViewClosed(UIBase view)
        {
        }

        private void RemoveModuleEvent()
        {
            foreach (Action removeEvent in m_events.Values)
                removeEvent.Invoke();

            m_events.Clear();
        }
    }
}
