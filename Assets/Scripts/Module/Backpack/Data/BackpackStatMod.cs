/*
* ┌──────────────────────────────────┐
* │  描    述: 背包物品可修改的能力线，与 IFishingStats 一一对应
* │  类    名: BackpackStatMod.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 只列当前真正有消费方的四条线，张力与显形半径等无消费方的能力先不进表
* 新增枚举项前必须先在玩法层找到读取点，避免配了没人读的死能力
*/

namespace Module.Backpack.Data
{
    public enum BackpackStatMod
    {
        LineLength = 0,
        ReelSpeed = 1,
        MaxCarryCount = 2,
        LurePull = 3
    }
}
