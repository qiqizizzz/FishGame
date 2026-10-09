/*
* ┌──────────────────────────────────┐
* │  描    述: 背包物品可修改的能力线，与 IFishingStats 一一对应
* │  类    名: BackpackStatMod.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 词表只认一条规矩：IFishingStats 里下游真有读取点的字段才能配成 BUFF
* 新增枚举项前必须先在玩法层找到读取点，避免配了没人读的死能力
*
* 已排除项与理由（不要往回加）：
*   TensionMax / TensionRelief     本版鱼线不会断，断线玩法未实现，无消费方
*   TensionLoadUnit / TensionDecay 同上，且张力负载目前只写不读
*   DetectRadius                   显形裁剪逻辑未实现，判定走 HookRadius + 容差
*   StashCapacity                  背包侧概念，IFishingStats 明令下潜层不得读背包概念
*   ComboStep / DepthBonusScale    挂在 FishingSessionConfigSO 上的关卡难度参数，
*                                  让玩家能买就等于让玩家能改肉鸽裁决的目标值
*/

namespace Module.Backpack.Data
{
    /// <summary>
    /// 可被装备与布料板块的 BUFF 修改的能力线枚举
    /// 低位项已被资产序列化成整数，只可追加不可重排
    /// </summary>
    public enum BackpackStatMod
    {
        LineLength = 0,
        ReelSpeed = 1,
        MaxCarryCount = 2,
        LurePull = 3,
        CoinBonus = 4,
        DescendSpeed = 5,
        LateralSpeed = 6,
        LateralDamping = 7,
        HookRadius = 8,
        DetachChance = 9,
        TangledDuration = 10,

        /// <summary>
        /// 空白布专用：只填缝不给加成，聚合侧按越界下标直接跳过
        /// 取 99 是为了和上面的真实能力线拉开距离，新增线时不必担心撞号
        /// </summary>
        None = 99
    }
}
