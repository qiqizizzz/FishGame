/*
* ┌──────────────────────────────────┐
* │  描    述: 巡游行为，单条在锁定深度上水平折返
* │  类    名: CruiseFishBehavior.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
*/

using System;
using Module.Fish.Model;
using Module.Fish.Type;
using UnityEngine;

namespace Module.Fish.Service
{
    public sealed class CruiseFishBehavior : IFishBehavior
    {
        private const float BOB_AMPLITUDE = 0.22f;
        private const float BOB_FREQUENCY = 0.9f;

        public FishBehaviorType Behavior => FishBehaviorType.Cruise;

        public void Advance(FishInstance fish, float deltaTime, float swimHalfWidth)
        {
            fish.Phase += deltaTime;
            fish.X += fish.Direction * fish.Speed * deltaTime;

            if (Math.Abs(fish.X) >= swimHalfWidth)
            {
                fish.X = Mathf.Clamp(fish.X, -swimHalfWidth, swimHalfWidth);
                fish.Direction = -fish.Direction;
            }

            fish.Depth = fish.BaseDepth + Mathf.Sin(fish.Phase * BOB_FREQUENCY) * BOB_AMPLITUDE;
        }
    }
}