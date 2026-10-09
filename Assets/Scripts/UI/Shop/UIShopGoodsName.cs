/*
* ┌──────────────────────────────────┐
* │  描    述: 存放物的界面显示名，装备带档位罗马数字，布料板块只写名字
* │  类    名: UIShopGoodsName.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 商品卡、背包块、暂存格三处都走这里取名，避免同一件东西在三个地方写成三个样
* 布料板块不分档，名字后面再挂个Ⅰ反而是噪音
*/

using Module.Backpack.Data;

namespace UI.Shop
{
    public static class UIShopGoodsName
    {
        private static readonly string[] m_tierNumerals = { "Ⅰ", "Ⅱ", "Ⅲ", "Ⅳ", "Ⅴ" };

        // ==================== Public Function ====================
        /// <summary>
        /// 取存放物在界面上的名字
        /// </summary>
        /// <param name="definition">存放物定义</param>
        /// <param name="tier">档位，从 1 开始</param>
        public static string Of(IBackpackDef definition, int tier)
        {
            if (definition == null)
                return string.Empty;

            if (definition.Kind == BackpackDefKind.Cloth)
                return definition.Label;

            return $"{definition.Label} {TierMark(tier)}";
        }

        /// <summary>
        /// 取档位对应的罗马数字，越界时退回阿拉伯数字
        /// </summary>
        /// <param name="tier">档位，从 1 开始</param>
        public static string TierMark(int tier)
        {
            int index = tier - 1;

            if (index < 0 || index >= m_tierNumerals.Length)
                return tier.ToString();

            return m_tierNumerals[index];
        }
    }
}
