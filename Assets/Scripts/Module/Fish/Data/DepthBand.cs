/*
 * ┌──────────────────────────────────┐
 * │  描    述: 深度带，描述鱼或档位可出现的深度区间
 * │  类    名: DepthBand.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 */

using System;

namespace Module.Fish.Data
{
    /// <summary>
    /// 深度区间，单位米，判定按 [MinDepth, MaxDepth] 闭区间
    /// </summary>
    [Serializable]
    public struct DepthBand
    {
        /// <summary>区间上界是否有效，MaxDepth 不为 0 即视为有效配置</summary>
        public bool IsValid => MaxDepth > 0f;

        /// <summary>是否包含给定深度</summary>
        public bool Contains(float depth)
        {
            if (!IsValid)
                return false;

            return depth >= MinDepth && depth <= MaxDepth;
        }

        // 取与另一深度带的交集，无交集返回无效带
        public DepthBand Intersect(DepthBand other)
        {
            if (!IsValid || !other.IsValid)
                return default;

            float min = Math.Max(MinDepth, other.MinDepth);
            float max = Math.Min(MaxDepth, other.MaxDepth);

            return max < min ? default : new DepthBand { MinDepth = min, MaxDepth = max };
        }

        public float MinDepth;
        public float MaxDepth;
    }
}
