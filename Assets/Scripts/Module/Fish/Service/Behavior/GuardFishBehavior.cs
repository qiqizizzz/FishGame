/*
* ┌──────────────────────────────────┐
* │  描    述: 警戒漂行行为，障碍专用，几乎不位移靠体积阻碍路线
* │  类    名: GuardFishBehavior.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 河豚不做折返也不越界，只在原地小幅浮动，位置稳定才谈得上「规划穿行路线」
*/

using Module.Fish.Model;
using Module.Fish.Type;
using UnityEngine;

namespace Module.Fish.Service
{
    public sealed class GuardFishBehavior : IFishBehavior
    {
        private const float HOVER_AMPLITUDE = 0.45f;
        private const float HOVER_FREQUENCY = 0.5f;

        public FishBehaviorType Behavior => FishBehaviorType.Guard;

        public void Advance(FishInstance fish, float deltaTime, float swimHalfWidth)
        {
            fish.Phase += deltaTime;
            fish.Depth = fish.BaseDepth + Mathf.Sin(fish.Phase * HOVER_FREQUENCY) * HOVER_AMPLITUDE;
        }
    }
}