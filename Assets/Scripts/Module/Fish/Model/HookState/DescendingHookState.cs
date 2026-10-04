/*
* ┌──────────────────────────────────┐
* │  描    述: 下潜态，按住匀速下沉并允许井道内横向拖动
* │  类    名: DescendingHookState.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 下潜阶段碰到任何鱼都算失误（见 01 篇 3.3），所以这里用 catching=false 让裁决按鱼身接触判
* 松手即回收是文档没写死的假设，已在校准问题清单里挂着
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