/*
* ┌──────────────────────────────────┐
* │  描    述: 出水态，短暂停顿后回到待命开启下一轮
* │  类    名: LandedHookState.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 停顿是为了给 M3 的结算飘字与音效留出可对齐的时间点
*/

using Module.Fish.Type;

namespace Module.Fish.Model
{
    public sealed class LandedHookState : IHookState
    {
        private const float PAUSE_SECONDS = 0.35f;

        private float m_elapsed;

        public HookPhase Phase => HookPhase.Landed;

        public void Enter(IHookRuntime runtime)
        {
            m_elapsed = 0f;
            runtime.MoveTo(runtime.HolePosition.x, 0f);
        }

        public void Update(IHookRuntime runtime, float deltaTime)
        {
            m_elapsed += deltaTime;

            if (m_elapsed >= PAUSE_SECONDS)
                runtime.TransitionTo(HookPhase.Idle);
        }

        public void Exit(IHookRuntime runtime)
        {
        }
    }
}