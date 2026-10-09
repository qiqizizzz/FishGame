/*
* ┌──────────────────────────────────┐
* │  描    述: 单局下潜属性包，把背包里的装备与布料板块聚合成一份只读能力线
* │  类    名: BackpackFishingStats.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 基准值取钩子配置，加成取背包，两条封顶各夹各的：装备那份夹配置里的线封顶，布料那份夹每块布自己写在 BUFF 上的封顶
* 夹完再相加，所以一块布铺满整张网也压不动装备，装备堆得再多也顶不掉布的封顶，两边互不干涉
* 布料的封顶按 ClothBuffEntry 的实例归组，同一块布散在包里的所有碎片共用一个界
* 汇总结果懒算，背包变动事件只置脏标记，属性被读时才重算一次
* 条目数按背包格数量级设计，每帧反复读属性也不会成为热点
*/

using System;
using System.Collections.Generic;
using Module.Backpack.Data;
using Module.Fish.Data;
using Module.Fish.Model;
using UnityEngine;

namespace Module.Backpack.Model
{
    /// <summary>
    /// 带背包加成的下潜属性包，随背包变动自动重算
    /// </summary>
    public sealed class BackpackFishingStats : IFishingStats, IDisposable
    {
        // ==================== 常量 ====================
        private const int MOD_SLOT_COUNT = 16;
        private const float COIN_BONUS_HARD_CAP = 0.6f;

        // ==================== 字段[私有] ====================
        private readonly HookConfigSO m_config;
        private readonly BackpackModel m_model;
        private readonly float[] m_itemSums = new float[MOD_SLOT_COUNT];
        private readonly float[] m_clothSums = new float[MOD_SLOT_COUNT];
        private readonly List<object> m_capOwners = new();
        private readonly List<int> m_capModIndices = new();
        private readonly List<float> m_capSums = new();
        private readonly List<float> m_capLimits = new();
        private readonly List<BackpackBuff> m_buffer = new();
        private bool m_dirty = true;

        // ==================== Public Function ====================
        /// <summary>
        /// 绑一份基准配置与一个背包模型，订阅它的变动事件
        /// </summary>
        /// <param name="config">能力线基准值与封顶地板的来源</param>
        /// <param name="model">要聚合的背包模型</param>
        public BackpackFishingStats(HookConfigSO config, BackpackModel model)
        {
            m_config = config;
            m_model = model;

            if (m_model != null)
                m_model.OnChanged += markDirty;
        }

        /// <summary>
        /// 解除对背包模型的订阅，避免旧属性包挂在事件上
        /// </summary>
        public void Dispose()
        {
            if (m_model != null)
                m_model.OnChanged -= markDirty;
        }

        public float LineLength => m_config.LineLength + upBonusAt(BackpackStatMod.LineLength, m_config.LineLengthBonusCap);
        public float ReelSpeed => m_config.ReelSpeed + upBonusAt(BackpackStatMod.ReelSpeed, m_config.ReelSpeedBonusCap);
        public float DescendSpeed => m_config.DescendSpeed + upBonusAt(BackpackStatMod.DescendSpeed, m_config.DescendSpeedBonusCap);
        public float LateralSpeed => m_config.LateralSpeed + upBonusAt(BackpackStatMod.LateralSpeed, m_config.LateralSpeedBonusCap);
        public float LateralDamping => m_config.LateralDamping + upBonusAt(BackpackStatMod.LateralDamping, m_config.LateralDampingBonusCap);
        public float HookRadius => m_config.HookRadius + upBonusAt(BackpackStatMod.HookRadius, m_config.HookRadiusBonusCap);
        public float LurePull => m_config.LurePull + upBonusAt(BackpackStatMod.LurePull, m_config.LurePullBonusCap);

        public int MaxCarryCount
        {
            get
            {
                float bonus = upBonusAt(BackpackStatMod.MaxCarryCount, m_config.MaxCarryCountBonusCap);
                return m_config.MaxCarryCount + Mathf.FloorToInt(Mathf.Max(0f, bonus));
            }
        }

        // 0.6 是接口侧的硬锁，配置里的线封顶只能往下收，收不出这个天花板
        public float CoinBonus => Mathf.Min(COIN_BONUS_HARD_CAP, m_config.CoinBonus + upBonusAt(BackpackStatMod.CoinBonus, m_config.CoinBonusCap));

        // 负向线不吃线封顶，只落在配置地板上方
        public float DetachChance => Mathf.Max(m_config.DetachChanceFloor, m_config.DetachChance + downBonusAt(BackpackStatMod.DetachChance));
        public float TangledDuration => Mathf.Max(m_config.TangledDurationFloor, m_config.TangledDuration + downBonusAt(BackpackStatMod.TangledDuration));

        // 预留未接线，直接透传基准，配了商品也不会改变玩法
        public float TensionMax => m_config.TensionMax;
        public float TensionRelief => m_config.TensionRelief;
        public float DetectRadius => m_config.DetectRadius;

        // ==================== Private Function ====================
        private void markDirty()
        {
            m_dirty = true;
        }

        // 正向线：装备那份先夹线封顶，布料那份已经夹过自己的封顶，两边相加
        private float upBonusAt(BackpackStatMod mod, float cap)
        {
            int index = slotIndexOf(mod);

            if (index < 0)
                return 0f;

            ensureFresh();
            return Mathf.Max(0f, Mathf.Min(m_itemSums[index], cap)) + m_clothSums[index];
        }

        // 负向线：两侧原始值透明相加，界限制在配置地板那一层
        private float downBonusAt(BackpackStatMod mod)
        {
            int index = slotIndexOf(mod);

            if (index < 0)
                return 0f;

            ensureFresh();
            return m_itemSums[index] + m_clothSums[index];
        }

        // 枚举值到数组下标的越界保护，None 这类不进数组的档位返回 -1
        private int slotIndexOf(BackpackStatMod mod)
        {
            int index = (int) mod;
            return (index < 0 || index >= MOD_SLOT_COUNT) ? -1 : index;
        }

        private void ensureFresh()
        {
            if (!m_dirty)
                return;

            m_dirty = false;
            recompute();
        }

        // 装备层与板块层各扫一遍，暂存架不在模型里，天然不参与聚合
        private void recompute()
        {
            Array.Clear(m_itemSums, 0, m_itemSums.Length);
            Array.Clear(m_clothSums, 0, m_clothSums.Length);
            m_capOwners.Clear();
            m_capModIndices.Clear();
            m_capSums.Clear();
            m_capLimits.Clear();

            if (m_model == null)
                return;

            scanList(m_model.Items);
            scanList(m_model.Plates);

            // 归完组再统一夹界：同一块布的所有碎片合起来才受这一块布的封顶约束
            for (int g = 0; g < m_capOwners.Count; g++)
            {
                float sum = m_capSums[g];
                float cap = m_capLimits[g];
                int index = m_capModIndices[g];
                m_clothSums[index] += cap >= 0f ? Mathf.Min(sum, cap) : Mathf.Max(sum, cap);
            }
        }

        // 扫一层存放物，谁报出几条就收几条：不设来源封顶的进装备账，设了的按归属主体归组
        private void scanList(IReadOnlyList<BackpackItemInstance> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                BackpackItemInstance item = list[i];
                IBackpackDef definition = item?.Definition;

                if (definition == null)
                    continue;

                m_buffer.Clear();
                definition.FillBuffs(item.Tier, m_buffer);

                for (int b = 0; b < m_buffer.Count; b++)
                {
                    BackpackBuff buff = m_buffer[b];
                    int index = slotIndexOf(buff.Mod);

                    if (index < 0)
                        continue;

                    if (buff.CapOwner == null)
                        m_itemSums[index] += buff.Value;
                    else
                        addToCapGroup(buff, index);
                }
            }
        }

        private void addToCapGroup(BackpackBuff buff, int modIndex)
        {
            int index = m_capOwners.IndexOf(buff.CapOwner);

            if (index < 0)
            {
                m_capOwners.Add(buff.CapOwner);
                m_capModIndices.Add(modIndex);
                m_capSums.Add(buff.Value);
                m_capLimits.Add(buff.Cap);
                return;
            }

            m_capSums[index] += buff.Value;
        }
    }
}
