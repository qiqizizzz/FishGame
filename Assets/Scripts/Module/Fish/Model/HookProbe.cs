/*
* ┌──────────────────────────────────┐
* │  描    述: 钩子威胁探针，随鱼群批量推进一起下发给行为策略
* │  类    名: HookProbe.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 只带「此刻算不算威胁」与钩尖捕获点坐标，不引用任何钩子实现，行为层因此不必知道状态机存在
* default 即无威胁：钩子在 idle、回收、缠绕时传下去的都是全零，鱼照平时的行为走
*/

namespace Module.Fish.Model
{
    /// <summary>
    /// 每帧随鱼群一起下发的钩子探针
    /// </summary>
    public struct HookProbe
    {
        /// <summary>钩子是否处于会让鱼躲避的阶段，只有下潜段为真</summary>
        public bool IsThreatening;

        /// <summary>钩尖捕获点横向坐标（米）</summary>
        public float X;

        /// <summary>钩尖捕获点深度（米），向下为正</summary>
        public float Depth;
    }
}
