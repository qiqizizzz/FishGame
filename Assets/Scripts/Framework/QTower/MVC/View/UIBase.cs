/*
 * ┌──────────────────────────────────┐
 * │  描    述: 运行时 UI 视图基类，统一 UI 生命周期与事件订阅
 * │  类    名: UIBase.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 */

using System;
using System.Collections.Generic;
using Framework.QTower.Controller;
using Framework.QTower.Event;
using UnityEngine;

namespace Framework.QTower.View
{
    public abstract class UIBase : MonoBehaviour
    {
        public BaseController Controller { get; internal set; }

        private readonly Dictionary<string, Action> m_events = new();
        private bool m_isInitialized;

        #region 生命周期
        public void Init()
        {
            if (m_isInitialized)
                return;

            OnInit();
            SubscribeViewEvents();
            m_isInitialized = true;
        }

        public void Open(params object[] args)
        {
            SetVisible(true);
            OnOpen(args);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        public void Close(params object[] args)
        {
            OnClose(args);
            Hide();
        }

        private void OnDestroy()
        {
            if (!m_isInitialized)
                return;

            RemoveViewEvent();
            OnDispose();
            m_isInitialized = false;
        }
        #endregion

        private void SetVisible(bool isVisible)
        {
            if (gameObject.activeSelf == isVisible)
                return;

            gameObject.SetActive(isVisible);
        }

        protected void RegisterEvent<TEvent>(string eventName, Action<TEvent> callback)
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

        protected void UnregisterEvent(string eventName)
        {
            if (!m_events.TryGetValue(eventName, out Action removeEvent))
                return;

            removeEvent.Invoke();
            m_events.Remove(eventName);
        }

        protected virtual void OnInit()
        {
        }

        protected virtual void OnOpen(params object[] args)
        {
        }

        protected virtual void OnClose(params object[] args)
        {
        }

        protected virtual void SubscribeViewEvents()
        {
        }

        protected virtual void OnDispose()
        {
        }

        // 移除全部托管视图事件
        private void RemoveViewEvent()
        {
            foreach (Action removeEvent in m_events.Values)
                removeEvent.Invoke();

            m_events.Clear();
        }
    }
}
