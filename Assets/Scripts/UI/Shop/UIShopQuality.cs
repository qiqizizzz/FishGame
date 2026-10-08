/*
* ┌──────────────────────────────────┐
* │  描    述: 商店背包界面的品质色板，提供四档品质色与配套文字色
* │  类    名: UIShopQuality.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 只是显示用色，数据层只出档位不出颜色，色板换风格不动数据
* 文字色按品质色亮度自动切深棕或浅米，保证任何色块上的名称都看得清
*/

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

        // ==================== Public Function ====================
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
