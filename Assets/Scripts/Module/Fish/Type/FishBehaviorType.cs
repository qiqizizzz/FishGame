/*
 * ┌──────────────────────────────────┐
 * │  描    述: 鱼种行为模板枚举，供 IFishBehavior 策略选择实现
 * │  类    名: FishBehaviorType.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 * 只描述鱼「平时怎么动」；惊散与逃逸是受事件触发的反应状态，由运行时服务叠加，不在此枚举内
 */

namespace Module.Fish.Type
{
    /// <summary>
    /// 鱼种的行为模板
    /// </summary>
    public enum FishBehaviorType
    {
        /// <summary>巡游：单条在深度带内水平折返</summary>
        Cruise = 0,

        /// <summary>群游：成团跟随，密度由会话配置决定</summary>
        School = 1,

        /// <summary>飘动：带状身体随水流摆动，速度低但覆盖深度大</summary>
        Drift = 2,

        /// <summary>警戒漂行：障碍专用，慢速横穿泳道，靠体积与走位形成路线阻碍</summary>
        Guard = 3,

        /// <summary>闪避：受威胁时朝背离钩子的方向随机短冲，冷却窗口内可靠预判捕获</summary>
        Evasive = 4
    }
}
