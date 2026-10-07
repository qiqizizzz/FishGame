/*
* ┌──────────────────────────────────┐
* │  描    述: 钩子状态机的上下文接口，由钩子表现对象实现
* │  类    名: IHookRuntime.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 状态只通过这个接口读写运行时，不直接引用 MonoBehaviour，便于脱离场景单测
*/

using Module.Fish.Data;
using Module.Fish.Type;
using UnityEngine;

namespace Module.Fish.Model
{
    public interface IHookRuntime
    {
        HookConfigSO HookConfig { get; }
        IFishingStats Stats { get; }

        /// <summary>接触裁决入口，由玩法控制器实现</summary>
        IFishingEncounter Encounter { get; }

        /// <summary>井道总深度（米），世界几何边界</summary>
        float ShaftDepth { get; }

        /// <summary>井道横向半宽（米），可活动边界</summary>
        float ShaftHalfWidth { get; }

        /// <summary>本阶段允许到达的最大深度，取井道深度与线长的较小值</summary>
        float MaxDepth { get; }

        /// <summary>钩子当前深度（米，水面为 0，向下为正）</summary>
        float Depth { get; }

        /// <summary>钩子当前横向坐标（米）</summary>
        float Horizontal { get; }

        /// <summary>钩尖捕获点横向坐标（米）</summary>
        float CaptureHorizontal { get; }

        /// <summary>钩尖捕获点深度（米，水面为 0，向下为正）</summary>
        float CaptureDepth { get; }

        /// <summary>井口锚点，回收与待命都回到这里</summary>
        Vector3 HolePosition { get; }

        /// <summary>横向残余速度，带惯性用，由状态读写</summary>
        float LateralVelocity { get; set; }

        /// <summary>本帧横向拖拽意图位移（米，已换算到世界单位）</summary>
        float LateralInput { get; }

        /// <summary>玩家是否处于按住状态</summary>
        bool IsPressing { get; }

        /// <summary>把钩子移动到指定横向坐标与深度</summary>
        void MoveTo(float x, float depth);

        /// <summary>请求状态迁移，由绑定的状态机执行</summary>
        void TransitionTo(HookPhase phase);
    }
}