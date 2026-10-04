/*
* ┌──────────────────────────────────┐
* │  描    述: 下潜态，按住匀速下沉并允许井道内横向拖动
* │  类    名: DescendingHookState.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 横向带轻微惯性：把本帧拖拽位移折算成期望速度，再按 LateralDamping 指数逼近
* 指数插值用 1-exp(-k*dt) 而非 Mathf.Lerp，保证不同帧率下手感一致
*/

using Module.Fish.Data;
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

            // 松手即开始回收，触底与首次捕获是另外两个回收触发点（见 01 篇 3.1）
            if (!runtime.IsPressing)
            {
                runtime.TransitionTo(HookPhase.Reeling);
                return;
            }

            HookConfigSO config = runtime.HookConfig;
            float desiredSpeed = Mathf.Clamp(runtime.LateralInput / deltaTime, -config.LateralSpeed, config.LateralSpeed);
            float blend = 1f - Mathf.Exp(-config.LateralDamping * deltaTime);

            runtime.LateralVelocity = Mathf.Lerp(runtime.LateralVelocity, desiredSpeed, blend);

            float x = Mathf.Clamp(runtime.Horizontal + runtime.LateralVelocity * deltaTime,
                -runtime.ShaftHalfWidth, runtime.ShaftHalfWidth);
            float depth = Mathf.Min(runtime.Depth + config.DescendSpeed * deltaTime, runtime.MaxDepth);

            runtime.MoveTo(x, depth);

            if (depth >= runtime.MaxDepth)
                runtime.TransitionTo(HookPhase.Reeling);
        }

        public void Exit(IHookRuntime runtime)
        {
            runtime.LateralVelocity = 0f;
        }
    }
}