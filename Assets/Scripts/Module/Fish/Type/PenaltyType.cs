/*
* ┌──────────────────────────────────┐
* │  描    述: 受罚类型枚举，判定层与 HUD 的共用语义
* │  类    名: PenaltyType.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
*/

namespace Module.Fish.Type
{
    /// <summary>
    /// 玩家本局受到的惩罚类型
    /// </summary>
    public enum PenaltyType
    {
        /// <summary>下潜阶段误触可捕获鱼，鱼被惊散</summary>
        ScaredFish = 0,

        /// <summary>任何阶段碰到河豚，钩子缠绕</summary>
        Pufferfish = 1
    }
}