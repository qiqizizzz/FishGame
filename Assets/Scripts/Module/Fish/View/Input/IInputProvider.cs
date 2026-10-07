/*
* ┌──────────────────────────────────┐
* │  描    述: 输入意图抽象，隔离具体输入设备
* │  类    名: IInputProvider.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 工程未装 Input System，先用旧 Input Manager 实现，接口留着日后换
*/

using UnityEngine;

namespace Module.Fish.View
{
    public interface IInputProvider
    {
        /// <summary>本帧是否处于按住状态</summary>
        bool IsPressed { get; }

        /// <summary>本帧刚按下的上升沿</summary>
        bool PressBegan { get; }

        /// <summary>本帧拖拽的屏幕像素位移</summary>
        Vector2 DragDeltaPixels { get; }
    }
}