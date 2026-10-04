/*
* ┌──────────────────────────────────┐
* │  描    述: 飘动行为，带状身体随水流缓慢上下飘，水平速度很低
* │  类    名: DriftFishBehavior.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 覆盖深度大（如皇带鱼），所以纵向起伏幅度远大于巡游鱼
*/

using Module.Fish.Model;
using Module.Fish.Type;
using UnityEngine;

namespace Module.Fish.Service
{
    public sealed class DriftFishBehavior : IFishBehavior
    {
        private const float DRIFT_AMPLITUDE = 2.4f;
        private const float DRIFT_FREQUENCY = 0.24f;
        private const float SWAY_AMPLITUDE = 0.5f;

        public FishBehaviorType Behavior => FishBehaviorType.Drift;

        public void Advance(FishInstance fish, float deltaTime, float swimHalfWidth)
        {
            fish.Phase += deltaTime;
            fish.X += fish.Direction * fish.Speed * deltaTime;

            if (Mathf.Abs(fish.X) >= swimHalfWidth)
            {
                fish.X = Mathf.Clamp(fish.X, -swimHalfWidth, swimHalfWidth);
                fish.Direction = -fish.Direction;
            }

            fish.Depth = fish.BaseDepth
                + Mathf.Sin(fish.Phase * DRIFT_FREQUENCY) * DRIFT_AMPLITUDE
                + Mathf.Sin(fish.Phase * 0.7f) * SWAY_AMPLITUDE;
        }
    }
}