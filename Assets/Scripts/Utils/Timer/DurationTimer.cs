/*
 * ┌──────────────────────────────────────────────────┐
 * │  描    述: 轻量无事件时间计时器，负责局部时间进度计算
 * │  类    名: DurationTimer.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────────────────────┘
 */

using UnityEngine;

namespace Utils.Timer
{
    public sealed class DurationTimer
    {
        private float m_duration;//总时长
        private float m_elapsedTime;//已运行时间
        private bool m_isRunning;//是否在运行
        private bool m_hasStarted;//是否已经开始了

        public bool IsRunning => m_isRunning;
        public bool HasStarted => m_hasStarted;
        public bool IsFinished => m_hasStarted && !m_isRunning && m_elapsedTime >= m_duration;
        public float Duration => m_duration;
        public float ElapsedTime => m_elapsedTime;
        public float RemainingTime => m_duration > m_elapsedTime ? m_duration - m_elapsedTime : 0f;
        
        //归一化
        public float NormalizedTime
        {
            get
            {
                if (!m_hasStarted)
                    return 0f;

                if (m_duration <= 0f)
                    return 1f;

                return Mathf.Clamp01(m_elapsedTime / m_duration);
            }
        }

        //启动计时器
        public void Start(float duration)
        {
            m_duration = duration;
            m_elapsedTime = 0f;
            m_hasStarted = true;
            m_isRunning = duration > 0f;
        }

        //更新计时器
        public void Tick(float deltaTime)
        {
            if(!m_isRunning || deltaTime < 0f) return;
            
            m_elapsedTime += deltaTime;

            if (m_elapsedTime < m_duration) return;
            
            m_elapsedTime = m_duration;
            m_isRunning = false;
        }
        
        //暂停计时器
        public void Pause()
        {
            if (!m_isRunning) return;

            m_isRunning = false;
        }
        
        //恢复计时器
        public void Resume()
        {
            if (!m_hasStarted || IsFinished) return;

            m_isRunning = true;
        }
        
        // 停止计时器并保留当前进度
        public void Stop()
        {
            if (!m_hasStarted)
                return;

            m_isRunning = false;
        }

        // 直接完成计时器
        public void Complete()
        {
            if (!m_hasStarted)
                return;

            m_elapsedTime = m_duration;
            m_isRunning = false;
        }

        // 重置计时器
        public void Reset()
        {
            m_duration = 0f;
            m_elapsedTime = 0f;
            m_isRunning = false;
            m_hasStarted = false;
        }
    }
}