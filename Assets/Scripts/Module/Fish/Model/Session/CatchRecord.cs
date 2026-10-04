/*
* ┌──────────────────────────────────┐
* │  描    述: 钩上负载记录，一条被挂住的鱼需要跟踪的全部数据
* │  类    名: CatchRecord.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 见主设计文档第五章：钩上负载用队列承载，脱钩按后进先出反向弹出，符合视觉上的鱼串
*/

namespace Module.Fish.Model
{
    public sealed class CatchRecord
    {
        /// <summary>被挂住的鱼实例</summary>
        public FishInstance Fish;

        /// <summary>挂上瞬间的深度（米），结算 DepthBonus 以此为准，回收途中上浮不再加成</summary>
        public float AttachedDepth;

        /// <summary>该鱼占用的携带容量点数</summary>
        public int CarryPoints;

        /// <summary>该鱼的张力负载档位，接张力模型时直接累加</summary>
        public int TensionLoad;
    }
}