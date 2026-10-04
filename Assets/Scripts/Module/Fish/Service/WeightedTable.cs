/*
* ┌──────────────────────────────────┐
* │  描    述: 加权随机表，前缀和加二分采样
* │  类    名: WeightedTable.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 见 01 篇第五章：按深度带抽鱼种用 O(log n) 采样，比逐个累加 Roll 更适合
* 密度随深度变化的场景——每个深度切片建一张表，抽一次只走一次二分
*/

using System.Collections.Generic;
using UnityEngine;

namespace Module.Fish.Service
{
    /// <summary>
    /// 权重到条目的加权随机表，Build 之后只读
    /// </summary>
    public sealed class WeightedTable<T>
    {
        private readonly List<T> m_items = new();
        private readonly List<float> m_prefix = new();

        private float m_total;

        public int Count => m_items.Count;

        public float TotalWeight => m_total;

        public void Clear()
        {
            m_items.Clear();
            m_prefix.Clear();
            m_total = 0f;
        }

        /// <summary>
        /// 追加条目，非正权重直接忽略，避免污染前缀和
        /// </summary>
        public void Add(T item, float weight)
        {
            if (weight <= 0f)
                return;

            m_total += weight;
            m_items.Add(item);
            m_prefix.Add(m_total);
        }

        /// <summary>
        /// 按 [0,1) 的随机值抽一个条目，表为空时返回 false
        /// </summary>
        public bool TrySample(float roll01, out T item)
        {
            item = default;

            if (m_items.Count == 0 || m_total <= 0f)
                return false;

            float target = Mathf.Clamp01(roll01) * m_total;

            int low = 0;
            int high = m_prefix.Count - 1;

            // 二分找第一个前缀和大于 target 的位置
            while (low < high)
            {
                int mid = (low + high) >> 1;

                if (m_prefix[mid] < target)
                    low = mid + 1;
                else
                    high = mid;
            }

            item = m_items[low];
            return true;
        }
    }
}