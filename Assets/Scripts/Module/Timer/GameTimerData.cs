/*
 * ┌──────────────────────────────────────────────┐
 * │  描    述: 游戏计时器数据，负责单个延迟或循环回调任务
 * │  类    名: GameTimerData.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────────────────┘
 */

using System;
using Utils.Timer;

namespace Module.Timer
{
    public class GameTimerData
    {
        private readonly DurationTimer m_timer;
        private readonly Action m_callback;//回调
        
        private readonly float m_duration;
        private readonly bool m_isLooping;

        private bool m_isCanceled;
        
        public int Id { get; }
        public bool IsCanceled => m_isCanceled;
        public bool IsFinished => m_isCanceled || (!m_isLooping && m_timer.IsFinished);
        
        public GameTimerData(int id, float duration, Action callback, bool isLooping)
        {
            Id = id;
            m_duration = duration;
            m_isLooping = isLooping;
            m_callback = callback;
            m_timer = new DurationTimer();
            m_timer.Start(duration);
        }

        public void Cancel() => m_isCanceled = true;
        
        public void Tick(float deltaTime)
        {
            if (IsFinished) return;
            
            m_timer.Tick(deltaTime);
            
            //若此时还没计时完, 则进入下一轮tick
            if(!m_timer.IsFinished) return;
            
            m_callback?.Invoke();

            //执行循环逻辑
            if (m_isLooping && !m_isCanceled)
                m_timer.Start(m_duration);
        }
    }
}
