/*
* ┌──────────────────────────────────┐
* │  描    述: 指针输入实现，鼠标与单指触摸等价
* │  类    名: MouseInputProvider.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* Input.GetMouseButton 在 Android 上等价单指按下，因此不需要两套逻辑
* 横向只走 DragDeltaPixels，LateralAxis 恒为 0，不与键盘抢同一根轴
*/

using UnityEngine;

namespace Module.Fish.View
{
    public sealed class MouseInputProvider : IInputProvider
    {
        private Vector3 m_lastPosition;

        public bool IsPressed { get; private set; }

        public bool PressBegan { get; private set; }

        public Vector2 DragDeltaPixels { get; private set; }

        public float LateralAxis => 0f;

        /// <summary>
        /// 每帧采样一次，须在控制器 Tick 内调用，顺序固定才能保证帧内一致
        /// </summary>
        /// <param name="deltaTime">本帧时长，指针按像素位移上报，这里用不上</param>
        public void Tick(float deltaTime)
        {
            Vector3 current = Input.mousePosition;

            IsPressed = Input.GetMouseButton(0);
            PressBegan = Input.GetMouseButtonDown(0);
            DragDeltaPixels = IsPressed ? current - m_lastPosition : Vector2.zero;

            m_lastPosition = current;
        }
    }
}
