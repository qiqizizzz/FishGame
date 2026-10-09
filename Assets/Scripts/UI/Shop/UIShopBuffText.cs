/*
* ┌──────────────────────────────────┐
* │  描    述: 能力线加成的短文案，把一条线的增减写成商品卡上能放下的几个字
* │  类    名: UIShopBuffText.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 只做显示，不参与任何计算，数值一律照传入的原样写，四舍五入由格式串说了算
* 概率类与金币类按百分比书写，其余保持各自单位，避免玩家看到 0.25 不知道是几成
* 一块布可以挂好几条线，珍罕布三条挤一行，用全角空格隔开，一条没有就老实写「空白布 · 只占格」
*/

using System.Collections.Generic;
using Module.Backpack.Data;
using UnityEngine;

namespace UI.Shop
{
    public static class UIShopBuffText
    {
        // ==================== 常量与静态字段 ====================
        private const string BLANK_CLOTH_TEXT = "空白布 · 只占格";

        // 多条 BUFF 之间的分隔，全角空格比半角宽，挤在一行里也分得开
        private const string SEPARATOR = "　";
        // ==================== Public Function ====================
        /// <summary>
        /// 把一条能力线的加成写成一行短说明
        /// </summary>
        /// <param name="mod">能力线</param>
        /// <param name="value">加成值，负数表示往下减</param>
        public static string Of(BackpackStatMod mod, float value)
        {
            switch (mod)
            {
                case BackpackStatMod.LineLength:
                    return signed(value, " 米线长");
                case BackpackStatMod.ReelSpeed:
                    return signed(value, " 米/秒收线");
                case BackpackStatMod.DescendSpeed:
                    return signed(value, " 米/秒下潜");
                case BackpackStatMod.LateralSpeed:
                    return signed(value, " 米/秒横移");
                case BackpackStatMod.LateralDamping:
                    return signed(value, " 跟手");
                case BackpackStatMod.HookRadius:
                    return signed(value, " 米钩径");
                case BackpackStatMod.MaxCarryCount:
                    return signed(value, " 条载量");
                case BackpackStatMod.LurePull:
                    return signed(value, " 稀有权重");
                case BackpackStatMod.CoinBonus:
                    return percent(value, " 卖价");
                case BackpackStatMod.DetachChance:
                    return percent(value, " 脱钩");
                case BackpackStatMod.TangledDuration:
                    return signed(value, " 秒缠绕");
                default:
                    return BLANK_CLOTH_TEXT;
            }
        }

        /// <summary>
        /// 把一件存放物身上的多条加成拼成一行，商品卡与背包 tooltip 共用
        /// </summary>
        /// <param name="buffs">本件报出的加成条目，空列表即空白布</param>
        public static string OfMany(IReadOnlyList<BackpackBuff> buffs)
        {
            if (buffs == null || buffs.Count == 0)
                return BLANK_CLOTH_TEXT;

            if (buffs.Count == 1)
                return Of(buffs[0].Mod, buffs[0].Value);

            var builder = new System.Text.StringBuilder();

            for (int i = 0; i < buffs.Count; i++)
            {
                if (i > 0)
                    builder.Append(SEPARATOR);

                builder.Append(Of(buffs[i].Mod, buffs[i].Value));
            }

            return builder.ToString();
        }

        // ==================== Private Function ====================
        private static string signed(float value, string suffix)
        {
            string sign = value < 0f ? "-" : "+";
            return $"{sign}{Mathf.Abs(value):0.###}{suffix}";
        }

        // 比例值折成百分号，脱钩与卖价这类线只讲百分比更直观
        private static string percent(float value, string suffix)
        {
            int points = Mathf.RoundToInt(value * 100f);
            string sign = points < 0 ? "-" : "+";
            return $"{sign}{Mathf.Abs(points)}%{suffix}";
        }
    }
}
