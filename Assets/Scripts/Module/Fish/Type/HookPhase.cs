/*
* ┌──────────────────────────────────┐
* │  描    述: 钩子阶段枚举，状态机与状态灯的共用标识
* │  类    名: HookPhase.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
*/

namespace Module.Fish.Type
{
    /// <summary>
    /// 钩子所处阶段，取值顺序与状态机流程一致
    /// </summary>
    public enum HookPhase
    {
        /// <summary>待命：钩子停在井口，等待按下</summary>
        Idle = 0,

        /// <summary>下潜：按住期间匀速下沉，可横向拖动</summary>
        Descending = 1,

        /// <summary>回收：触底或捕获后自动收线回水面，无需玩家操作</summary>
        Reeling = 2,

        /// <summary>缠绕：挂上河豚的受罚态</summary>
        Tangled = 3,

        /// <summary>出水：回到水面，短暂停顿后回待命</summary>
        Landed = 4
    }
}