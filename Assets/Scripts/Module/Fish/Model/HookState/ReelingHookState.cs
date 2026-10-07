/*
* ┌──────────────────────────────────┐
* │  描    述: 回收态，自动收线回水面且全程保留横向操控
* │  类    名: ReelingHookState.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 回收才是真正在钓鱼的阶段：能左右蹭鱼，所以横向手感与下潜共用 HookLateralMover，
* 只是不再往下给深度。碰到整条鱼身任意位置即捕获，碰到河豚转缠绕
*/

using Module.Fish.Type;
using UnityEngine;

namespace Module.Fish.Model
{
    public sealed class ReelingHookState : IHookState
    {
        public HookPhase Phase => HookPhase.Reeling;

        public void Enter(IHookRuntime runtime)
        {
            runtime.LateralVelocity = 0f;
        }

        public void Update(IHookRuntime runtime, float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            float x = HookLateralMover.Advance(runtime, deltaTime);
            float depth = Mathf.Max(runtime.Depth - runtime.Stats.ReelSpeed * deltaTime, 0f);

            runtime.MoveTo(x, depth);

            if (runtime.Encounter != null && runtime.Encounter.ResolveContact(runtime, true))
            {
                runtime.TransitionTo(HookPhase.Tangled);
                return;
            }

            if (depth <= 0f)
                runtime.TransitionTo(HookPhase.Landed);
        }

        public void Exit(IHookRuntime runtime)
        {
        }
    }
}