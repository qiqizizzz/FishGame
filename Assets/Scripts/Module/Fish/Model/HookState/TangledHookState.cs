/*
* ┌──────────────────────────────────┐
* │  描    述: 缠绕态，碰到河豚后的受罚僵直，计时结束继续回收
* │  类    名: TangledHookState.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 僵直期间钩子原地不动、不接受输入，已经挂上的鱼仍由携带服务跟着钩子停在同处
* 河豚在裁决侧会被一并惊散，所以退出后不会立刻又蹭到同一条
*/

using Module.Fish.Type;

namespace Module.Fish.Model
{
    public sealed class TangledHookState : IHookState
    {
        private float m_elapsed;

        public HookPhase Phase => HookPhase.Tangled;

        public void Enter(IHookRuntime runtime)
        {
            m_elapsed = 0f;
            runtime.LateralVelocity = 0f;
        }

        public void Update(IHookRuntime runtime, float deltaTime)
        {
            m_elapsed += deltaTime;

            if (m_elapsed >= runtime.HookConfig.TangledDuration)
                runtime.TransitionTo(HookPhase.Reeling);
        }

        public void Exit(IHookRuntime runtime)
        {
        }
    }
}