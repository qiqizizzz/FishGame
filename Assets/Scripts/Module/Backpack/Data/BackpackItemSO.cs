/*
* ┌──────────────────────────────────┐
* │  描    述: 背包装备定义，一档一个形状模板，格数由模板推导不在此填写
* │  类    名: BackpackItemSO.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 模板库解耦点：同一档不同装备可以挂同一个形状资产，形状只是占位外形不是装备身份
* 升档即换模板，装备变强也变占地，这就是背包格要逼出来的取舍
* 列表长度即最高档位，配到 3 就是三级封顶，超出部分不参与结算
* 价钱留三元素数组可选自带：紧凑件该卖贵、大件该卖便宜，「格子换数值」要落到价格上才成立
* 留空即回落全局的档位定价，只有一两件想单独定价时才填
* 负载这类取整生效的能力线，三档数值必须全填整数，卡面显示多少进背包就是多少
* 装备没有阵营概念，界面底色一律按档位取品质色，不再挂阵营资产
*/

using System.Collections.Generic;
using UnityEngine;
using Utils.log;

namespace Module.Backpack.Data
{
    [CreateAssetMenu(menuName = "FishGame/Config/Backpack Item", fileName = "BackpackItem")]
    public sealed class BackpackItemSO : ScriptableObject, IBackpackDef
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
        [Tooltip("与形状列表等长的三档基准价，留空则回落全局档位定价")]
        [SerializeField, Min(0)] private List<int> TierPrices = new();
        [Tooltip("详情小窗第二行的说明文案，一句话讲清这件装备在换什么")]
        [SerializeField, Multiline(2)] private string Description;

        // ==================== 属性 ====================
        public string Id => ItemId;
        public string Label => ItemLabel;
        public Sprite Icon => ItemIcon;
        public BackpackDefKind Kind => BackpackDefKind.Item;
        public BackpackStatMod Mod => ModType;
        public int MaxTier => TierShapes.Count;

        // 装备不靠自身染色，底色由界面按档位取品质色
        public Color TintColor => Color.white;

        // 自带价只在这件装备填了档位价时才生效，没填就交给全局档位定价
        public int TierPriceCount => TierPrices.Count;
        public string Desc => Description;

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

        /// <summary>
        /// 取指定档位的基准价，没配自带价或档位越界时返回 0，由调用方回落全局定价
        /// </summary>
        /// <param name="tier">档位，从 1 开始</param>
        public int TierPriceAt(int tier)
        {
            if (TierPrices.Count == 0)
                return 0;

            int index = clampTier(tier) - 1;
            return index < TierPrices.Count ? TierPrices[index] : 0;
        }

        /// <summary>
        /// 报出本件在指定档位的加成，装备不设来源封顶，交给能力线自己的全局封顶
        /// </summary>
        /// <param name="tier">档位，从 1 开始</param>
        /// <param name="result">要追加条目的列表，由调用方负责清空</param>
        public void FillBuffs(int tier, List<BackpackBuff> result)
        {
            if (result == null || ModType == BackpackStatMod.None)
                return;

            result.Add(new BackpackBuff(ModType, ValueAt(tier), null, 0f));
        }

        // ==================== Private Function ====================
        // 负载是取整生效的线，档位值带小数就会出现「卡面 +0.5、进背包 +0」这种花钱买废品的事故
        private void checkWholeTierValues()
        {
            for (int i = 0; i < TierValues.Count; i++)
            {
                float value = TierValues[i];

                if (Mathf.Abs(value - Mathf.Round(value)) > 0.0001f)
                    QLog.Error($"[BackpackItemSO] 负载线的档位值必须填整数，取整后卡面显示值与实际生效值不一致：{ItemId} 第 {i + 1} 档 {value}");
            }
        }

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

            if (TierPrices.Count > 0 && TierPrices.Count != TierShapes.Count)
                QLog.Error($"[BackpackItemSO] 自带价的档数与形状档数不等：{ItemId} 价 {TierPrices.Count} 形状 {TierShapes.Count}");

            for (int i = 0; i < TierShapes.Count; i++)
            {
                if (TierShapes[i] == null)
                    QLog.Error($"第 {i + 1} 档形状模板为空：{ItemId}");

                if (TierPrices.Count == TierShapes.Count && TierPrices[i] <= 0)
                    QLog.Error($"[BackpackItemSO] 自带价填了 0 会让这件装备白送：{ItemId} 第 {i + 1} 档");
            }

            if (ModType == BackpackStatMod.MaxCarryCount)
                checkWholeTierValues();
        }
    }
}
