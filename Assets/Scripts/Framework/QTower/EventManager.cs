/*
 * ┌──────────────────────────────────┐
 * │  描    述: QTower 全局事件管理器
 * │  类    名: EventManager.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 */

using System;
using System.Collections.Generic;

namespace Framework.QTower.Event
{
    public static class EventManager
    {
        private static readonly Dictionary<string, Delegate> m_events = new();

        // 注册无参数全局事件
        public static void RegisterEvent(string eventName, Action callback)
        {
            if (m_events.TryGetValue(eventName, out Delegate registered))
            {
                m_events[eventName] = Delegate.Combine(registered, callback);
                return;
            }

            m_events.Add(eventName, callback);
        }

        public static void RegisterEvent<TEvent>(string eventName, Action<TEvent> callback)
        {
            if (m_events.TryGetValue(eventName, out Delegate registered))
            {
                m_events[eventName] = Delegate.Combine(registered, callback);
                return;
            }

            m_events.Add(eventName, callback);
        }

        public static void UnregisterEvent<TEvent>(string eventName, Action<TEvent> callback)
        {
            if (!m_events.TryGetValue(eventName, out Delegate registered))
                return;

            Delegate remaining = Delegate.Remove(registered, callback);
            if (remaining == null)
            {
                m_events.Remove(eventName);
                return;
            }

            m_events[eventName] = remaining;
        }

        // 取消注册无参数全局事件
        public static void UnregisterEvent(string eventName, Action callback)
        {
            if (!m_events.TryGetValue(eventName, out Delegate registered))
                return;

            Delegate remaining = Delegate.Remove(registered, callback);
            if (remaining == null)
            {
                m_events.Remove(eventName);
                return;
            }

            m_events[eventName] = remaining;
        }

        // 发布无参数全局事件
        public static void PublishEvent(string eventName)
        {
            if (m_events.TryGetValue(eventName, out Delegate registered))
                ((Action)registered).Invoke();
        }

        public static void PublishEvent<TEvent>(string eventName, TEvent eventData)
        {
            if (m_events.TryGetValue(eventName, out Delegate registered))
                ((Action<TEvent>)registered).Invoke(eventData);
        }
    }
}
