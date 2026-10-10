/*
* ┌──────────────────────────────────┐
* │  描    述: 商店配色规则，布料板块走布自己的底色、装备走档位品质色，文字色随底色深浅翻转
* │  类    名: UIShopQuality.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 板块一律用这块布自己的颜色，饱和度天生比装备低，一眼就能分出常驻的小额与大头的那件
* 颜色不再绕阵营与纸页两层去取，布上写什么色就染什么色
* 装备不靠自身染色，底色只在白蓝紫金四档之间取值
*/

using Module.Backpack.Data;
using UnityEngine;

namespace UI.Shop
{
    public static class UIShopQuality
    {
        // 名称文字走深色还是浅色，阈值按四档品质色的亮度取的
        private const float LABEL_LUMINANCE_THRESHOLD = 0.55f;

        private static readonly Color m_labelDark = new Color32(0x4A, 0x32, 0x20, 0xFF);
        private static readonly Color m_labelLight = new Color32(0xF5, 0xE6, 0xC8, 0xFF);

        // ==================== 属性 ====================
        public static Color White { get; } = new Color32(0xC9, 0xCD, 0xD2, 0xFF);
        public static Color Blue { get; } = new Color32(0x3E, 0x7B, 0xFA, 0xFF);
        public static Color Purple { get; } = new Color32(0x8B, 0x5C, 0xF6, 0xFF);
        public static Color Gold { get; } = new Color32(0xF5, 0xB9, 0x3B, 0xFF);

        // 买不起时的价钱色，压在木色底上够扎眼又不刺眼，商品卡与详情小窗共用这一个
        public static Color PriceUnaffordable { get; } = new Color32(0xB0, 0x3A, 0x2E, 0xFF);

        // ==================== Public Function ====================
        /// <summary>
        /// 取存放物该染的颜色，板块用布自己的底色，装备用档位品质色
        /// </summary>
        /// <param name="definition">存放物定义</param>
        /// <param name="tier">档位，只有装备用得上</param>
        public static Color ColorFor(IBackpackDef definition, int tier)
        {
            if (definition == null)
                return White;

            if (definition.Kind == BackpackDefKind.Cloth)
                return definition.TintColor;

            return TierColor(tier);
        }

        /// <summary>
        /// 取档位对应的品质色，一到三级走白蓝紫，再往上的档位留给合成
        /// </summary>
        /// <param name="tier">档位，从 1 开始</param>
        public static Color TierColor(int tier)
        {
            switch (tier)
            {
                case 1:
                    return White;
                case 2:
                    return Blue;
                case 3:
                    return Purple;
                default:
                    return Gold;
            }
        }

        /// <summary>
        /// 取品质色上用于标注名称的文字色
        /// </summary>
        /// <param name="qualityColor">品质色</param>
        public static Color LabelColor(Color qualityColor)
        {
            float luminance = qualityColor.r * 0.2126f + qualityColor.g * 0.7152f + qualityColor.b * 0.0722f;
            return luminance > LABEL_LUMINANCE_THRESHOLD ? m_labelDark : m_labelLight;
        }
    }
}
