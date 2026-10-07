/*
* ┌──────────────────────────────────┐
* │  描    述: 下潜态，按住匀速下沉并允许井道内横向拖动
* │  类    名: DescendingHookState.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 下潜阶段碰到任何鱼都算失误，所以这里用 catching=false 让裁决按误触处理
* 松手即回收是玩法上的假设，不是既定规则，要改判只动这一处
*/

using Module.Fish.Type;
using UnityEngine;

namespace Module.Fish.Model
{
    public sealed class DescendingHookState : IHookState
    {
        public HookPhase Phase => HookPhase.Descending;

        public void Enter(IHookRuntime runtime)
        {
            runtime.LateralVelocity = 0f;
        }

        public void Update(IHookRuntime runtime, float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            if (!runtime.IsPressing)
            {
                runtime.TransitionTo(HookPhase.Reeling);
                return;
            }

            float x = HookLateralMover.Advance(runtime, deltaTime);
            float depth = Mathf.Min(runtime.Depth + runtime.HookConfig.DescendSpeed * deltaTime, runtime.MaxDepth);

            runtime.MoveTo(x, depth);

            if (runtime.Encounter != null && runtime.Encounter.ResolveContact(runtime, false))
                runtime.TransitionTo(HookPhase.Tangled);

            if (depth >= runtime.MaxDepth)
                runtime.TransitionTo(HookPhase.Reeling);
        }

        public void Exit(IHookRuntime runtime)
        {
            runtime.LateralVelocity = 0f;
        }
    }
}