/*
* ┌──────────────────────────────────┐
* │  描    述: 存放物的界面显示名，装备带档位字母标记，布料板块只写名字
* │  类    名: UIShopGoodsName.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 商品卡、背包块、暂存格三处都走这里取名，避免同一件东西在三个地方写成三个样
* 布料板块不分档，名字后面再挂个 I 反而是噪音
*/

using Module.Backpack.Data;

namespace UI.Shop
{
    public static class UIShopGoodsName
    {
        // 档位标记用字母 I 一个一个拼，不用 U+2160 那一档整字罗马数字
        // ZCOOL 快乐体源字体里没有那段字形，硬用会掉进回退字体，显示成两根跟汉字不搭的细竖条
        private static readonly string[] m_tierNumerals = { "I", "II", "III", "IV", "V" };

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
        /// 取档位对应的字母标记，越界时退回阿拉伯数字
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
