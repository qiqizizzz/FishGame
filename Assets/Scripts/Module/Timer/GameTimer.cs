/*
 * ┌──────────────────────────────────┐
 * │  描    述: 游戏计时器，负责管理延迟与循环计时任务集合
 * │  类    名: GameTimer.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 */

using System;
using System.Collections.Generic;
using Utils.log;

namespace Module.Timer
{
    public sealed class GameTimer
    {
        private readonly List<GameTimerData> m_timers = new List<GameTimerData>();
        private int m_nextTimerId = 1;
        
        public int Count => m_timers.Count;
        
        //注册计时任务
        public int Register(float duration, Action callback, bool isLooping = false)
        {
            if (duration < 0f)
            {
                QLog.Error("注册计时器失败, 持续时间不能小于0.");
                duration = 0f;
            }

            int timerId = m_nextTimerId++;
            GameTimerData timerData = new GameTimerData(timerId, duration, callback, isLooping);
            m_timers.Add(timerData);
            return timerId;
        }

        //取消指定计时任务
        public void Cancel(int timerId)
        {
            for (int i = 0; i < Count; i++)
            {
                if(m_timers[i].Id != timerId) continue;
                
                m_timers[i].Cancel();
                return;
            }
        }
        
        //执行全部计时任务
        public void Tick(float deltaTime)
        {
            for(int i = Count - 1; i >= 0; i--)
            {
                GameTimerData timerData = m_timers[i];
                timerData.Tick(deltaTime);
                
                if(timerData.IsFinished)
                    m_timers.RemoveAt(i);
            }
        }

        //清空全部计时任务
        public void ClearAll()
        {
            m_timers.Clear();
        }
    }
}
