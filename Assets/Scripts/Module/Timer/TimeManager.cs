/*
 * ┌──────────────────────────────────┐
 * │  描    述: 时间管理器，负责对外提供全局计时任务调度
 * │  类    名: TimeManager.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 */

using System;

namespace Module.Timer
{
    public sealed class TimeManager
    {
        private readonly GameTimer m_gameTimer = new();

        public int Count => m_gameTimer.Count;

        // 注册计时任务
        public int Register(float duration, Action callback, bool isLooping = false)
        {
            return m_gameTimer.Register(duration, callback, isLooping);
        }

        // 取消指定计时任务
        public void Cancel(int timerId)
        {
            m_gameTimer.Cancel(timerId);
        }

        // 清空全部计时任务
        public void ClearAll()
        {
            m_gameTimer.ClearAll();
        }

        // 推进计时器
        public void Tick(float deltaTime)
        {
            m_gameTimer.Tick(deltaTime);
        }

        // 销毁前清理全部计时任务
        public void Destroy()
        {
            ClearAll();
        }
    }
}
