/*
* ┌──────────────────────────────────┐
* │  描    述: 警戒漂行行为，障碍鱼慢速横穿泳道并带轻微纵向悬浮
* │  类    名: GuardFishBehavior.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 挡路但不能定死：钉在一点的河豚会让「规划穿行路线」退化成认死一个静态路障，看穿就没有博弈
* 横向跟普通鱼同一条路子：只做速度积分加撞壁折返，绝不做绝对定位，否则被惊散游远后会在逃逸结束的当帧瞬移回原处
* 快慢只由 fish.Speed 决定，生成服务给 Hazard 档一份低基准速度，本类不再自带一套巡守速度
*/

using Module.Fish.Model;
using Module.Fish.Type;
using UnityEngine;

namespace Module.Fish.Service
{
    public sealed class GuardFishBehavior : IFishBehavior
    {
        // 悬浮幅度比巡游鱼大些：慢鱼也得让人看出它在水里浮着，而不是一块贴在水里的贴图
        private const float HOVER_AMPLITUDE = 0.45f;
        private const float HOVER_FREQUENCY = 0.5f;

        public FishBehaviorType Behavior => FishBehaviorType.Guard;

        public void Advance(FishInstance fish, float deltaTime, float swimHalfWidth, in HookProbe hook)
        {
            fish.Phase += deltaTime;
            fish.X += fish.Direction * fish.Speed * deltaTime;

            if (Mathf.Abs(fish.X) >= swimHalfWidth)
            {
                fish.X = Mathf.Clamp(fish.X, -swimHalfWidth, swimHalfWidth);
                fish.Direction = -fish.Direction;
            }

            fish.Depth = fish.BaseDepth + Mathf.Sin(fish.Phase * HOVER_FREQUENCY) * HOVER_AMPLITUDE;
        }
    }
}
