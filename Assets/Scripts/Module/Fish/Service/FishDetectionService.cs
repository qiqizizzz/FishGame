/*
* ┌──────────────────────────────────┐
* │  描    述: 鱼群空间索引，按深度一维分桶做钩子接触查询
* │  类    名: FishDetectionService.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 井道是窄竖屏，鱼在纵向上天然分散、横向上几乎重叠，所以一维深度分桶就够把每帧检测
* 从「全场鱼数」压到「附近三桶」，比上二维网格少一半内存与重建开销
*/

using System.Collections.Generic;
using Module.Fish.Model;
using UnityEngine;

namespace Module.Fish.Service
{
    public sealed class FishDetectionService
    {
        private const float BUCKET_SIZE = 2f;
        private const int BUCKET_COUNT = 64;
        private const float MAX_QUERY_RADIUS = 1.6f;

        private readonly List<FishInstance>[] m_buckets = new List<FishInstance>[BUCKET_COUNT];

        public FishDetectionService()
        {
            for (int i = 0; i < BUCKET_COUNT; i++)
                m_buckets[i] = new List<FishInstance>();
        }

        /// <summary>
        /// 每帧重建索引，钩上的鱼不再参与接触判定
        /// </summary>
        public void Rebuild(IReadOnlyList<FishInstance> fishes)
        {
            for (int i = 0; i < BUCKET_COUNT; i++)
                m_buckets[i].Clear();

            if (fishes == null)
                return;

            for (int i = 0; i < fishes.Count; i++)
            {
                FishInstance fish = fishes[i];

                if (fish == null || !fish.IsAlive || fish.IsHooked)
                    continue;

                m_buckets[bucketIndexOf(fish.Depth)].Add(fish);
            }
        }

        /// <summary>
        /// 取与钩子接触到的最近一条鱼
        /// </summary>
        /// <param name="headOnly">true 只认鱼头接触（回收捕获），false 鱼身任意接触都算（下潜误触、河豚）</param>
        public bool TryFindTouched(float x, float depth, float hookRadius, bool headOnly, out FishInstance touched)
        {
            touched = null;

            float bestDistance = float.MaxValue;
            int center = bucketIndexOf(depth);
            int span = Mathf.CeilToInt(MAX_QUERY_RADIUS / BUCKET_SIZE) + 1;

            for (int bucket = center - span; bucket <= center + span; bucket++)
            {
                if (bucket < 0 || bucket >= BUCKET_COUNT)
                    continue;

                List<FishInstance> fishes = m_buckets[bucket];

                for (int i = 0; i < fishes.Count; i++)
                {
                    FishInstance fish = fishes[i];
                    float distance = TouchDistance(x, depth, fish, headOnly);

                    if (distance > TouchRadius(fish, hookRadius) || distance >= bestDistance)
                        continue;

                    bestDistance = distance;
                    touched = fish;
                }
            }

            return touched != null;
        }

        // 钩子到鱼的有效距离：headOnly 按鱼头点算，否则按鱼身中心算
        public static float TouchDistance(float x, float depth, FishInstance fish, bool headOnly)
        {
            float length = fish.Data != null ? fish.Data.DisplayLength : 1f;
            float targetX = headOnly ? fish.X + fish.Direction * length * 0.5f : fish.X;
            float dx = x - targetX;
            float dy = depth - fish.Depth;

            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        // 该鱼的有效接触半径，鱼越大越容易蹭到
        public static float TouchRadius(FishInstance fish, float hookRadius)
        {
            float length = fish.Data != null ? fish.Data.DisplayLength : 1f;

            return hookRadius + length * 0.4f;
        }

        private static int bucketIndexOf(float depth)
        {
            return Mathf.Clamp(Mathf.FloorToInt(depth / BUCKET_SIZE), 0, BUCKET_COUNT - 1);
        }
    }
}