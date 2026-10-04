/*
* ┌──────────────────────────────────┐
* │  描    述: 钩子状态机，用字典索引状态替代 enum 加 switch
* │  类    名: HookStateMachine.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 状态迁移集中在这里，新增阶段只加一个 IHookState 实现并注册，不改迁移逻辑
*/

using System.Collections.Generic;
using Module.Fish.Type;
using Utils.log;

namespace Module.Fish.Model
{
    public sealed class HookStateMachine
    {
        private readonly Dictionary<HookPhase, IHookState> m_states = new();

        private IHookState m_current;

        /// <summary>
        /// 以可变参数注册全部状态，必须包含 Idle 作为初始态
        /// </summary>
        public HookStateMachine(params IHookState[] states)
        {
            foreach (IHookState state in states)
            {
                if (state == null)
                    continue;

                if (m_states.ContainsKey(state.Phase))
                {
                    QLog.Error($"[{nameof(HookStateMachine)}] 阶段重复注册：{state.Phase}");
                    continue;
                }

                m_states.Add(state.Phase, state);
            }
        }

        public HookPhase Phase => m_current == null ? HookPhase.Idle : m_current.Phase;

        public bool IsIdle => m_current != null && m_current.Phase == HookPhase.Idle;

        /// <summary>
        /// 进入初始态，由运行时提供上下文
        /// </summary>
        public void Start(IHookRuntime runtime)
        {
            if (!m_states.TryGetValue(HookPhase.Idle, out m_current))
            {
                QLog.Error($"[{nameof(HookStateMachine)}] 未注册 Idle 状态，钩子不会响应输入");
                return;
            }

            m_current.Enter(runtime);
        }

        public void Update(IHookRuntime runtime, float deltaTime)
        {
            m_current?.Update(runtime, deltaTime);
        }

        /// <summary>
        /// 迁移到目标阶段，未注册的阶段保持原状态并报错
        /// </summary>
        public void Change(HookPhase phase, IHookRuntime runtime)
        {
            if (!m_states.TryGetValue(phase, out IHookState next))
            {
                QLog.Error($"[{nameof(HookStateMachine)}] 未注册阶段 {phase}，忽略本次迁移");
                return;
            }

            if (next == m_current)
                return;

            m_current?.Exit(runtime);
            m_current = next;
            m_current.Enter(runtime);
        }
    }
}