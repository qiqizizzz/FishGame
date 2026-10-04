/*
* ┌──────────────────────────────────┐
* │  描    述: 钩上负载服务，管容量校验、挂鱼跟随与受罚脱钩
* │  类    名: FishCarryService.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 挂上钩的鱼退出 AI 驱动，改由本服务排成鱼串贴在钩子下方
* 脱钩一律从最外侧（最后挂上的）开始，先贪上去的最后掉，符合「贪心代价」的视觉
*/

using System.Collections.Generic;
using Module.Fish.Data;
using Module.Fish.Model;
using UnityEngine;

namespace Module.Fish.Service
{
    public sealed class FishCarryService
    {
        private const float CARRY_SPACING = 0.5f;
        private const int MAX_CARRIED = 6;

        private readonly List<CatchRecord> m_carried = new();

        private IFishingStats m_stats;
        private RarityTableSO m_rarityTable;

        public IReadOnlyList<CatchRecord> Carried => m_carried;

        public int Count => m_carried.Count;

        /// <summary>已占用的容量点数</summary>
        public int UsedPoints { get; private set; }

        /// <summary>
        /// 开局绑定属性包与稀有度表并清空负载
        /// </summary>
        public void BeginSession(IFishingStats stats, RarityTableSO rarityTable)
        {
            m_stats = stats;
            m_rarityTable = rarityTable;
            m_carried.Clear();
            UsedPoints = 0;
        }

        /// <summary>
        /// 能否再挂这条鱼：容量够、没挂过、没到条数上限
        /// </summary>
        public bool CanAttach(FishInstance fish)
        {
            if (fish == null || fish.IsHooked || m_carried.Count >= MAX_CARRIED)
                return false;

            return UsedPoints + carryCost(fish) <= m_stats.CarryCapacity;
        }

        /// <summary>
        /// 把鱼挂上钩子，不满足条件返回 null
        /// </summary>
        public CatchRecord Attach(FishInstance fish)
        {
            if (!CanAttach(fish))
                return null;

            CatchRecord record = new()
            {
                Fish = fish,
                AttachedDepth = fish.Depth,
                CarryPoints = carryCost(fish),
                TensionLoad = fish.Data != null ? fish.Data.ResolveTensionLoad(m_rarityTable) : 0
            };

            fish.IsHooked = true;
            fish.FleeTimer = 0f;
            m_carried.Add(record);
            UsedPoints += record.CarryPoints;

            return record;
        }

        /// <summary>
        /// 让钩上的鱼排成鱼串贴在钩子下方，最先进钩的离钩子最近
        /// </summary>
        public void Follow(float x, float depth)
        {
            for (int i = 0; i < m_carried.Count; i++)
            {
                FishInstance fish = m_carried[i].Fish;

                if (fish == null || !fish.IsAlive)
                    continue;

                fish.X = x;
                fish.Depth = depth + (i + 1) * CARRY_SPACING;
                fish.Root.localPosition = new Vector3(x, -fish.Depth, fish.Root.localPosition.z);
            }
        }

        /// <summary>
        /// 受罚时按概率甩掉钩上的鱼，从最外侧开始脱，脱掉的写进 released 交调用方处理
        /// </summary>
        public void DetachByChance(float chance, List<CatchRecord> released)
        {
            if (chance <= 0f)
                return;

            for (int i = m_carried.Count - 1; i >= 0; i--)
            {
                if (Random.value >= chance)
                    continue;

                releaseAt(i, released);
            }
        }

        /// <summary>
        /// 出水结算：钩上的鱼全部交给调用方并清空负载
        /// </summary>
        public void Unload(List<CatchRecord> landed)
        {
            for (int i = m_carried.Count - 1; i >= 0; i--)
                releaseAt(i, landed);
        }

        private void releaseAt(int index, List<CatchRecord> sink)
        {
            CatchRecord record = m_carried[index];
            m_carried.RemoveAt(index);
            UsedPoints = Mathf.Max(0, UsedPoints - record.CarryPoints);
            sink.Add(record);
        }

        private static int carryCost(FishInstance fish)
        {
            return fish.Data != null ? Mathf.Max(1, fish.Data.CarryCost) : 1;
        }
    }
}