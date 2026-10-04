/*
* ┌──────────────────────────────────┐
* │  描    述: 钩子与鱼的接触裁决接口，由玩法控制器实现
* │  类    名: IFishingEncounter.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 规则全部收在实现侧（见主设计文档 3.3）：下潜碰鱼是失误、回收蹭鱼头是捕获、河豚任何阶段都受罚
* 状态类只负责说「我这个阶段碰到了东西」，不自己判后果，避免规则散进五个状态
*/

namespace Module.Fish.Model
{
    public interface IFishingEncounter
    {
        /// <summary>
        /// 裁决钩子当前位置与周围鱼的一次接触
        /// </summary>
        /// <param name="hook">当前钩子上下文，取坐标与钩头半径</param>
        /// <param name="catching">true 为回收阶段的鱼头捕获判定，false 为下潜阶段的任意接触即失误</param>
        /// <returns>需要迁移到缠绕态时返回 true</returns>
        bool ResolveContact(IHookRuntime hook, bool catching);
    }
}