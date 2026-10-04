/*
* ┌──────────────────────────────────┐
* │  描    述: 群游行为，成员围绕编组中心折返并保持固定偏移
* │  类    名: SchoolFishBehavior.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 同组共享方向与速度、只加静态偏移，零邻居计算就能成团，比 boids 便宜得多
*/

using System;
using Module.Fish.Model;
using Module.Fish.Type;
using UnityEngine;

namespace Module.Fish.Service
{
    public sealed class SchoolFishBehavior : IFishBehavior
    {
        private const float BOB_AMPLITUDE = 0.12f;
        private const float BOB_FREQUENCY = 1.4f;

        public FishBehaviorType Behavior => FishBehaviorType.School;

        public void Advance(FishInstance fish, float deltaTime, float swimHalfWidth)
        {
            fish.Phase += deltaTime;
            fish.X += fish.Direction * fish.Speed * deltaTime;

            float limit = swimHalfWidth + Math.Abs(fish.Offset.x);

            if (Math.Abs(fish.X) >= limit)
            {
                fish.X = Mathf.Clamp(fish.X, -limit, limit);
                fish.Direction = -fish.Direction;
            }

            fish.Depth = fish.BaseDepth + fish.Offset.y
                + Mathf.Sin(fish.Phase * BOB_FREQUENCY) * BOB_AMPLITUDE;
        }
    }
}