/*
* ┌──────────────────────────────────┐
* │  描    述: 能力线加成的短文案，把一条线的增减写成详情小窗里一行的几个字
* │  类    名: UIShopBuffText.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 只做显示，不参与任何计算，数值一律照传入的原样写，四舍五入由格式串说了算
* 概率类与金币类按百分比书写，其余保持各自单位，避免玩家看到 0.25 不知道是几成
* 一块布可以挂好几条线，一条能力线占一行竖着排，一条没有就老实写「只铺格子，不给加成」
* 这段字写在详情小窗的名字下面，上面已经写过布名，这里就别再重复布名
*/

using System.Collections.Generic;
using Module.Backpack.Data;
using UnityEngine;

namespace UI.Shop
{
    public static class UIShopBuffText
    {
        // ==================== 常量与静态字段 ====================
        private const string BLANK_CLOTH_TEXT = "只铺格子，不给加成";

        // 多条 BUFF 之间直接换行，一块布三条线就占三行，小窗的高跟着行数一起长
        private const string SEPARATOR = "\n";

        // 兑现不了的负载要说在前头，灰字压在深底小窗上既显眼又不跟主文案抢色
        private const string MUTED_OPEN = "<color=#8E9AA8>";
        private const string MUTED_CLOSE = "</color>";
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
        /// 把一件存放物身上的多条加成拼成整段文案，一条能力线占一行
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

        /// <summary>
        /// 写清这件商品在负载线上能兑现几条，兑现不了的部分必须买之前就讲明白
        /// </summary>
        /// <param name="buff">商品给出的那条负载加成</param>
        /// <param name="headroom">负载总线当前还剩多少额度</param>
        /// <param name="ownerSpent">这块布的载量是否已经被同款吃满，成立时再买一条也加不上</param>
        public static string CarryShortfall(BackpackBuff buff, int headroom, bool ownerSpent)
        {
            if (buff.Mod != BackpackStatMod.MaxCarryCount)
                return string.Empty;

            if (ownerSpent)
                return muted("这种布的载量已经吃满，再买一块不会加载量");

            int offered = Mathf.RoundToInt(buff.Value);
            int realizable = Mathf.Min(offered, headroom);

            if (realizable >= offered)
                return string.Empty;

            if (realizable <= 0)
                return muted("负载已满，买下去一条也不会多");

            return muted($"总线只剩 {headroom} 条，这件只能兑现 {realizable} 条");
        }

        // ==================== Private Function ====================
        private static string muted(string text)
        {
            return $"{MUTED_OPEN}{text}{MUTED_CLOSE}";
        }

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
