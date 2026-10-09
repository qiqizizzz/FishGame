/*
* ┌──────────────────────────────────┐
* │  描    述: 一块剪好异形的布，布料总表在运行时把布与形状拼成的一件存放物
* │  类    名: ClothPieceDef.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 布和形状各自只有一个 SO，拼出来的组合不占资产，靠总表按同一对下标返回同一个实例
* 引用必须稳定：BUFF 条目把封顶归属报成配置里的 ClothBuffEntry，同一块布散在包里的碎片才归得了组
* 不分档，MaxTier 恒 1，档位在这条链路上没有意义
* 名字里带上占格数，玩家在同一面货架上看到两块同名布时能分出谁是二格谁是六格
*/

using System.Collections.Generic;
using UnityEngine;

namespace Module.Backpack.Data
{
    /// <summary>
    /// 布 × 异形拼出的板块定义，与 IBackpackDef 的其余实现共用一条摆放链路
    /// </summary>
    public sealed class ClothPieceDef : IBackpackDef
    {
        // ==================== 常量 ====================
        private const int PIECE_TIER = 1;

        // ==================== 属性 ====================
        /// <summary>拼出本件的布，价钱与 BUFF 都问它要</summary>
        public ClothEntry Cloth { get; }

        /// <summary>本件剪成的异形</summary>
        public GridShapeSO Shape { get; }

        public string Id => $"{Cloth.Id}@{Shape.Id}";
        public string Label => $"{Cloth.Label}·{Shape.CellCount}格";
        public Sprite Icon => null;
        public BackpackDefKind Kind => BackpackDefKind.Cloth;
        public int MaxTier => PIECE_TIER;
        public Color TintColor => Cloth.Color;

        // ==================== Constructor ====================
        public ClothPieceDef(ClothEntry cloth, GridShapeSO shape)
        {
            Cloth = cloth;
            Shape = shape;
        }

        // ==================== Public Function ====================
        /// <summary>
        /// 板块不分档，任何档位都是同一个异形
        /// </summary>
        /// <param name="tier">档位，从 1 开始</param>
        public GridShapeSO ShapeAt(int tier)
        {
            return Shape;
        }

        /// <summary>
        /// 报出本件身上的全部加成，每条都带着所属 BUFF 配置作为封顶归属
        /// </summary>
        /// <param name="tier">档位，板块忽略</param>
        /// <param name="result">要追加条目的列表，由调用方负责清空</param>
        public void FillBuffs(int tier, List<BackpackBuff> result)
        {
            if (Cloth == null || Shape == null || result == null)
                return;

            IReadOnlyList<ClothBuffEntry> buffs = Cloth.Buffs;
            int cells = Shape.CellCount;

            for (int i = 0; i < buffs.Count; i++)
            {
                ClothBuffEntry buff = buffs[i];

                if (buff == null || buff.Mod == BackpackStatMod.None)
                    continue;

                result.Add(new BackpackBuff(buff.Mod, Cloth.ValueFor(buff, cells), buff, buff.CapValue));
            }
        }
    }
}
