/*
* ┌──────────────────────────────────┐
* │  描    述: 输入意图抽象，隔离具体输入设备
* │  类    名: IInputProvider.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 工程未装 Input System，先用旧 Input Manager 实现，接口留着日后换
* 指针设备上报「本帧屏幕像素位移」，键盘上报「归一化横轴」，
* 两条各只在自己那一端贡献非零值，由控制器分别折算成米后合流
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

        /// <summary>本帧拖拽的屏幕像素位移，键盘类设备恒为 0</summary>
        Vector2 DragDeltaPixels { get; }

        /// <summary>本帧横向意图轴，-1 左 / 0 中 / 1 右，指针类设备恒为 0</summary>
        float LateralAxis { get; }

        /// <summary>
        /// 每帧采样一次输入设备，须在控制器 Tick 内调用，顺序固定才能保证帧内一致
        /// </summary>
        /// <param name="deltaTime">本帧时长，按时间积分的设备需要，指针设备忽略即可</param>
        void Tick(float deltaTime);
    }
}
