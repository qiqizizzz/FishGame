/*
* ┌──────────────────────────────────┐
* │  描    述: 回合阶段枚举，商店与钓鱼两大阶段的标识
* │  类    名: GamePhase.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
*/

namespace UI.RoundHUD
{
    /// <summary>
    /// 一个回合内的两大阶段
    /// </summary>
    public enum GamePhase
    {
        /// <summary>商店：花金币购买与合成</summary>
        Shop = 0,

        /// <summary>钓鱼：下潜采集渔获</summary>
        Fishing = 1
    }
}
