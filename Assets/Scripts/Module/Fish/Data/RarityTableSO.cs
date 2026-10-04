/*
 * ┌──────────────────────────────────┐
 * │  描    述: 稀有度查找表，档位到配色与数值的唯一来源
 * │  类    名: RarityTableSO.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 * 鱼身配色、HUD 星标、结算卡片与 P1 背包品质边框都读这张表，颜色和数值同源
 * OnEnable 用字典重建索引，运行时按档位 O(1) 查询，不做线性遍历
 * Inspector 手改行不会触发 OnEnable，因此 OnValidate 同样重建，避免索引与数据脱节
 */

using System.Collections.Generic;
using Module.Fish.Type;
using UnityEngine;
using Utils.log;

namespace Module.Fish.Data
{
    [CreateAssetMenu(menuName = "FishGame/Config/Rarity Table", fileName = "RarityTable")]
    public sealed class RarityTableSO : ScriptableObject
    {
        [Header("档位行")]
        [Tooltip("必须覆盖 Common / Uncommon / Rare / Epic / Legendary / Hazard 六档")]
        [SerializeField] private List<RarityRow> Rows = new();

        private readonly Dictionary<FishRarity, RarityRow> m_rowMap = new();

        public IReadOnlyList<RarityRow> Entries => Rows;

        private void OnEnable()
        {
            rebuildIndex();

#if UNITY_EDITOR
            // 新建未落盘的资产字段还是空的，跳过覆盖校验避免创建期误报
            if (!UnityEditor.AssetDatabase.Contains(this))
                return;
#endif

            validateCoverage();
        }

        private void OnValidate()
        {
            rebuildIndex();
        }

        /// <summary>
        /// 按档位取行，缺失时在编辑器报错并返回 null
        /// </summary>
        public RarityRow Get(FishRarity rarity)
        {
            if (m_rowMap.TryGetValue(rarity, out RarityRow row))
                return row;

            QLog.Error($"[{nameof(RarityTableSO)}] 缺少档位配置：{rarity}");
            return null;
        }

        public bool TryGet(FishRarity rarity, out RarityRow row)
        {
            return m_rowMap.TryGetValue(rarity, out row);
        }

        // 重建档位到行的索引，重复档位只保留首条并报错
        private void rebuildIndex()
        {
            m_rowMap.Clear();

            if (Rows == null)
                return;

            for (int i = Rows.Count - 1; i >= 0; i--)
            {
                RarityRow row = Rows[i];

                if (row == null)
                {
                    QLog.Error($"[{nameof(RarityTableSO)}] 第 {i} 行为空，请在 Inspector 中补齐");
                    Rows.RemoveAt(i);
                    continue;
                }

                if (m_rowMap.ContainsKey(row.Rarity))
                {
                    QLog.Error($"[{nameof(RarityTableSO)}] 档位重复：{row.Rarity}");
                    continue;
                }

                m_rowMap.Add(row.Rarity, row);
            }
        }

        private void validateCoverage()
        {
            FishRarity[] required =
            {
                FishRarity.Common, FishRarity.Uncommon, FishRarity.Rare,
                FishRarity.Epic, FishRarity.Legendary, FishRarity.Hazard
            };

            foreach (FishRarity rarity in required)
            {
                if (m_rowMap.ContainsKey(rarity))
                    continue;

                QLog.Error($"[{nameof(RarityTableSO)}] 未配置档位 {rarity}，鱼群抽取与结算配色都会失准");
            }
        }
    }
}
