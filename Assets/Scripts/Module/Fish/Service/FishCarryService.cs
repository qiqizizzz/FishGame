/*
* ┌──────────────────────────────────┐
* │  描    述: 钩上负载服务，管挂鱼登记、挂鱼跟随与受罚脱钩
* │  类    名: FishCarryService.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 挂上钩的鱼退出 AI 驱动，改由本服务排成鱼串贴在钩子下方
* 脱钩一律从最外侧（最后挂上的）开始，先贪上去的最后掉，符合「贪心代价」的视觉
* 鱼串按每条鱼的实测身高累加间距，相邻两鱼刻意叠掉一部分再左右交替错位，
* 整串才像挂在钩子上的一串鱼，而不是顺着鱼线散开的几条
* 挂住的鱼比自由鱼前移一个纵深层，否则回收时会被井口的冰层方块挡住
*/

using System.Collections.Generic;
using Module.Fish.Data;
using Module.Fish.Model;
using UnityEngine;

namespace Module.Fish.Service
{
    public sealed class FishCarryService
    {
        // 实测身高缺失时的兜底间距（米）
        private const float CARRY_SPACING = 0.5f;
        private const float HOOK_CLEARANCE = 0.3f;
        private const float FISH_GAP = 0.06f;
        // 相邻两鱼身高的重叠系数，1 为首尾相接，越小叠得越紧
        private const float BODY_OVERLAP = 0.55f;
        private const float SWAY_RATIO = 0.14f;
        private const float MAX_SWAY = 0.22f;
        private const float HANG_TILT_DEGREES = 16f;
        // 井道冰层方块的前表面在世界 z=-2，挂住的鱼必须比它更靠近相机才不会被井口切掉
        private const float CARRY_WORLD_Z = -2.5f;

        private readonly List<CatchRecord> m_carried = new();

        private IFishingStats m_stats;
        private RarityTableSO m_rarityTable;

        public IReadOnlyList<CatchRecord> Carried => m_carried;

        public int Count => m_carried.Count;

        /// <summary>钩上可挂的鱼条数上限，HUD 负载行拿它做分母；开局绑定前返回 0
        /// HUD 在预加载阶段就会刷新，此时属性包尚未 BeginSession，故允许判空</summary>
        public int Capacity => m_stats != null ? m_stats.MaxCarryCount : 0;

        /// <summary>
        /// 开局绑定属性包与稀有度表并清空负载
        /// </summary>
        public void BeginSession(IFishingStats stats, RarityTableSO rarityTable)
        {
            m_stats = stats;
            m_rarityTable = rarityTable;
            m_carried.Clear();
        }

        /// <summary>
        /// 能否再挂这条鱼：鱼要还活着、没被挂过，钩上还得留着空名额
        /// 每条鱼一律占 1 个名额，名额上限来自钩子配置，不再按体型折算容量点数
        /// </summary>
        public bool CanAttach(FishInstance fish)
        {
            return fish != null && !fish.IsHooked && fish.IsAlive && m_carried.Count < Capacity;
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
                TensionLoad = fish.Data != null ? fish.Data.ResolveTensionLoad(m_rarityTable) : 0
            };

            fish.IsHooked = true;
            fish.FleeTimer = 0f;
            m_carried.Add(record);

            return record;
        }

        /// <summary>
        /// 让钩上的鱼排成鱼串贴在钩子下方，最先进钩的离钩子最近
        /// </summary>
        public void Follow(float x, float depth)
        {
            float offset = HOOK_CLEARANCE;
            float previousHeight = 0f;

            for (int i = 0; i < m_carried.Count; i++)
            {
                FishInstance fish = m_carried[i].Fish;

                if (fish == null || !fish.IsAlive)
                    continue;

                float height = fish.DisplayHeight > 0f ? fish.DisplayHeight : CARRY_SPACING;
                float length = fish.Data != null ? fish.Data.DisplayLength : height;

                offset += i == 0
                    ? height * 0.5f
                    : (previousHeight + height) * 0.5f * BODY_OVERLAP + FISH_GAP;
                previousHeight = height;

                int swaySign = i % 2 == 0 ? 1 : -1;
                float sway = Mathf.Min(length * SWAY_RATIO, MAX_SWAY) * swaySign;

                fish.X = x + sway;
                fish.Depth = depth + offset;

                Transform root = fish.Root;
                root.localPosition = new Vector3(fish.X, -fish.Depth, chainLocalZ(root));
                root.localRotation = Quaternion.Euler(0f, 0f, HANG_TILT_DEGREES * swaySign)
                    * Quaternion.Euler(0f, fish.Direction >= 0 ? 90f : -90f, 0f);
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

        // 把鱼串的世界纵深换算成鱼根节点的局部 z，父级无旋转所以只差一个平移
        private static float chainLocalZ(Transform root)
        {
            Transform parent = root != null ? root.parent : null;
            return CARRY_WORLD_Z - (parent != null ? parent.position.z : 0f);
        }

        private void releaseAt(int index, List<CatchRecord> sink)
        {
            CatchRecord record = m_carried[index];
            m_carried.RemoveAt(index);
            sink.Add(record);
        }
    }
}