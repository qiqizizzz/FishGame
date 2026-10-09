/*
* ┌──────────────────────────────────┐
* │  描    述: 钩子横向推进工具，下潜与回收共用同一套惯性手感
* │  类    名: HookLateralMover.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 把本帧拖拽位移折算成期望速度，再按 LateralDamping 指数逼近，
* 指数插值用 1-exp(-k*dt) 而非 Mathf.Lerp，保证不同帧率下手感一致
* 限速与收敛系数一律走属性包，背包加成才能同时作用于下潜段和回收段
*/

using UnityEngine;

namespace Module.Fish.Model
{
    public static class HookLateralMover
    {
        /// <summary>
        /// 推进横向坐标并写回残余速度，返回夹在井道内的新横坐标
        /// </summary>
        public static float Advance(IHookRuntime runtime, float deltaTime)
        {
            IFishingStats stats = runtime.Stats;
            float desiredSpeed = Mathf.Clamp(runtime.LateralInput / deltaTime, -stats.LateralSpeed, stats.LateralSpeed);
            float blend = 1f - Mathf.Exp(-stats.LateralDamping * deltaTime);

            runtime.LateralVelocity = Mathf.Lerp(runtime.LateralVelocity, desiredSpeed, blend);

            return Mathf.Clamp(runtime.Horizontal + runtime.LateralVelocity * deltaTime,
                -runtime.ShaftHalfWidth, runtime.ShaftHalfWidth);
        }
    }
}
