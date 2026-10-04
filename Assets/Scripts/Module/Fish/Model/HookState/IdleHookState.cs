/*
* ┌──────────────────────────────────┐
* │  描    述: 待命态，钩子停在井口等待按下
* │  类    名: IdleHookState.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
*/

using Module.Fish.Type;
using UnityEngine;

namespace Module.Fish.Model
{
    public sealed class IdleHookState : IHookState
    {
        public HookPhase Phase => HookPhase.Idle;

        public void Enter(IHookRuntime runtime)
        {
            runtime.LateralVelocity = 0f;
            runtime.MoveTo(runtime.HolePosition.x, 0f);
        }

        public void Update(IHookRuntime runtime, float deltaTime)
        {
            // 钩子在井口轻微浮动，纯表现，不产生位移累计
            float bob = Mathf.Sin(Time.unscaledTime * 1.6f) * 0.04f;
            runtime.MoveTo(runtime.HolePosition.x, Mathf.Max(0f, -bob));

            if (runtime.IsPressing)
                runtime.TransitionTo(HookPhase.Descending);
        }

        public void Exit(IHookRuntime runtime)
        {
        }
    }
}