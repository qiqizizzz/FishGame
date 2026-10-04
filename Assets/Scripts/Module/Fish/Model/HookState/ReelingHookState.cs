/*
* ┌──────────────────────────────────┐
* │  描    述: 回收态，自动收线回水面，玩家不干预
* │  类    名: ReelingHookState.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 回收中横向缓慢回中，避免钩子停在井壁上导致下一次下潜起点偏移
*/

using Module.Fish.Type;
using UnityEngine;

namespace Module.Fish.Model
{
    public sealed class ReelingHookState : IHookState
    {
        private const float RETURN_TO_CENTER_RATE = 2.5f;

        public HookPhase Phase => HookPhase.Reeling;

        public void Enter(IHookRuntime runtime)
        {
            runtime.LateralVelocity = 0f;
        }

        public void Update(IHookRuntime runtime, float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            float depth = Mathf.Max(runtime.Depth - runtime.Stats.ReelSpeed * deltaTime, 0f);
            float x = Mathf.Lerp(runtime.Horizontal, runtime.HolePosition.x, 1f - Mathf.Exp(-RETURN_TO_CENTER_RATE * deltaTime));

            runtime.MoveTo(x, depth);

            if (depth <= 0f)
                runtime.TransitionTo(HookPhase.Landed);
        }

        public void Exit(IHookRuntime runtime)
        {
        }
    }
}