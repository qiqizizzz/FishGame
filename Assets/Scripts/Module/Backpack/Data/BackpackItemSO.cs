/*
* ┌──────────────────────────────────┐
* │  描    述: 背包装备定义，一档一个形状模板，格数由模板推导不在此填写
* │  类    名: BackpackItemSO.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 模板库解耦点：同一档不同装备可以挂同一个形状资产，形状只是占位外形不是装备身份
* 升档即换模板，装备变强也变占地，这就是背包格要逼出来的取舍
* 列表长度即最高档位，配到 3 就是三级封顶，超出部分不参与结算
*/

using System.Collections.Generic;
using UnityEngine;
using Utils.log;

namespace Module.Backpack.Data
{
    [CreateAssetMenu(menuName = "FishGame/Config/Backpack Item", fileName = "BackpackItem")]
    public sealed class BackpackItemSO : ScriptableObject
    {
        // ==================== 常量 ====================
        public const int MAX_TIER = 3;

        // ==================== 字段[外部设置] ====================
        [Tooltip("程序内唯一标识")]
        [SerializeField] private string ItemId;
        [Tooltip("界面显示名")]
        [SerializeField] private string ItemLabel;
        [Tooltip("物品图标，素材未就位时留空，界面按名称显示")]
        [SerializeField] private Sprite ItemIcon;
        [Tooltip("本件装备修改的能力线")]
        [SerializeField] private BackpackStatMod ModType;
        [Tooltip("索引 0 是一级，每档挂一个形状模板，格数由模板决定")]
        [SerializeField] private List<GridShapeSO> TierShapes = new();
        [Tooltip("与形状列表等长，该档对本件能力线的修改量")]
        [SerializeField] private List<float> TierValues = new();

        // ==================== 属性 ====================
        public string Id => ItemId;
        public string Label => ItemLabel;
        public Sprite Icon => ItemIcon;
        public BackpackStatMod Mod => ModType;
        public int MaxTier => TierShapes.Count;

        // ==================== Public Function ====================
        /// <summary>
        /// 取指定档位的形状模板，档位越界时钳到最高档
        /// </summary>
        /// <param name="tier">档位，从 1 开始</param>
        public GridShapeSO ShapeAt(int tier)
        {
            return TierShapes[clampTier(tier) - 1];
        }

        /// <summary>
        /// 取指定档位的能力值修改量，档位越界时钳到最高档
        /// </summary>
        /// <param name="tier">档位，从 1 开始</param>
        public float ValueAt(int tier)
        {
            int index = clampTier(tier) - 1;
            return index < TierValues.Count ? TierValues[index] : 0f;
        }

        // ==================== Private Function ====================
        private int clampTier(int tier)
        {
            return Mathf.Clamp(tier, 1, TierShapes.Count);
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(ItemId))
                QLog.Error($"背包装备缺少标识：{name}");

            if (TierShapes.Count == 0)
            {
                QLog.Error($"背包装备没有配置任何档位的形状模板：{ItemId}");
                return;
            }

            if (TierShapes.Count > MAX_TIER)
                QLog.Error($"背包装备档位超过上限 {MAX_TIER}：{ItemId} 配了 {TierShapes.Count} 档");

            if (TierShapes.Count != TierValues.Count)
                QLog.Error($"形状档数与数值档数不等：{ItemId} 形状 {TierShapes.Count} 数值 {TierValues.Count}");

            for (int i = 0; i < TierShapes.Count; i++)
            {
                if (TierShapes[i] == null)
                    QLog.Error($"第 {i + 1} 档形状模板为空：{ItemId}");
            }
        }
    }
}
