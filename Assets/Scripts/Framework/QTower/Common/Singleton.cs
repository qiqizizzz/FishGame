/*
 * ┌──────────────────────────────────┐
 * │  描    述: 纯 C# 单例基类，统一应用级对象生命周期
 * │  类    名: Singleton.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 */

using System;

namespace Framework.QTower.Common
{
    public abstract class Singleton<T> where T : Singleton<T>, new()
    {
        private static readonly Lazy<T> S_instance = new(() => new T());

        private bool m_isInitialized;

        public static T Instance => S_instance.Value;
        public bool IsInitialized => m_isInitialized;

        public void Init()
        {
            if (m_isInitialized)
                return;

            OnInit();
            m_isInitialized = true;
        }

        public void Tick(float deltaTime)
        {
            if (!m_isInitialized)
                return;

            OnTick(deltaTime);
        }

        public void Destroy()
        {
            if (!m_isInitialized)
                return;

            m_isInitialized = false;
            OnDestroy();
        }

        protected virtual void OnInit()
        {
        }

        protected virtual void OnTick(float deltaTime)
        {
        }

        protected virtual void OnDestroy()
        {
        }
    }
}
