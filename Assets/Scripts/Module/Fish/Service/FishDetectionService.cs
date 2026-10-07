/*
* ┌──────────────────────────────────┐
* │  描    述: 鱼群空间索引，按深度一维分桶做钩子接触查询
* │  类    名: FishDetectionService.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 井道是窄竖屏，鱼在纵向上天然分散、横向上几乎重叠，所以一维深度分桶就够把每帧检测
* 从「全场鱼数」压到「附近三桶」，比上二维网格少一半内存与重建开销
* 判定盒用工厂实测的真实渲染盒：屏幕上鱼身画到哪里，钩尖就能在哪里挂上，
* 换成配置体长凑的盒子会比画面小，大鱼头尾各差几十厘米，看着勾中却直接穿过去
*/

using System;
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
        // 渲染盒之外再留的容差（米），极小杂鱼只有十几厘米，一点容差都不留会点不中
        private const float TOUCH_TOLERANCE = 0.1f;
        // 没有实测盒时按体长的三成估半高，别让判定塌成一条线
        private const float FALLBACK_HEIGHT_RATIO = 0.3f;

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
        /// <param name="catching">true 为回收捕获，false 为下潜误触</param>
        public bool TryFindTouched(float x, float depth, float hookRadius, bool catching, out FishInstance touched)
        {
            return TryFindTouched(x, depth, hookRadius, catching, null, out touched);
        }

        /// <summary>
        /// 取与钩子接触到的最近一条鱼，filter 用于跳过已上钩等不可捕获目标
        /// </summary>
        /// <param name="catching">true 为回收捕获，false 为下潜误触</param>
        public bool TryFindTouched(float x, float depth, float hookRadius, bool catching,
            Func<FishInstance, bool> filter, out FishInstance touched)
        {
            touched = null;

            float reach = hookRadius + TOUCH_TOLERANCE;
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

                    if (filter != null && !filter(fish))
                        continue;

                    float distance = TouchDistance(x, depth, fish);

                    if (distance > reach || distance >= bestDistance)
                        continue;

                    bestDistance = distance;
                    touched = fish;
                }
            }

            return touched != null;
        }

        /// <summary>
        /// 钩尖到鱼身渲染盒的最短距离，落在盒内时为 0
        /// </summary>
        public static float TouchDistance(float x, float depth, FishInstance fish)
        {
            resolveBox(fish, out float centerX, out float centerDepth, out float halfX, out float halfY);

            float dx = Mathf.Max(0f, Mathf.Abs(x - centerX) - halfX);
            float dy = Mathf.Max(0f, Mathf.Abs(depth - centerDepth) - halfY);

            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 该鱼的有效接触半径：钩尖半径加固定容差，盒子本身已经提供了鱼身范围
        /// </summary>
        public static float TouchRadius(FishInstance fish, float hookRadius)
        {
            return hookRadius + TOUCH_TOLERANCE;
        }

        // 把渲染盒换算到判定平面：盒心不在根节点上，横向偏移沿鱼头方向、竖向偏移向上为正
        private static void resolveBox(FishInstance fish, out float centerX, out float centerDepth,
            out float halfX, out float halfY)
        {
            Vector2 half = fish.BodyHalf;
            Vector2 offset = fish.BodyOffset;

            if (half.x <= 0f || half.y <= 0f)
            {
                float length = fish.Data != null ? fish.Data.DisplayLength : TOUCH_TOLERANCE * 2f;
                float height = fish.DisplayHeight > 0f ? fish.DisplayHeight : length * FALLBACK_HEIGHT_RATIO;

                half = new Vector2(length * 0.5f, Mathf.Max(height * 0.5f, TOUCH_TOLERANCE));
            }

            centerX = fish.X + fish.Direction * offset.x;
            centerDepth = fish.Depth - offset.y;
            halfX = half.x;
            halfY = half.y;
        }

        private static int bucketIndexOf(float depth)
        {
            return Mathf.Clamp(Mathf.FloorToInt(depth / BUCKET_SIZE), 0, BUCKET_COUNT - 1);
        }
    }
}