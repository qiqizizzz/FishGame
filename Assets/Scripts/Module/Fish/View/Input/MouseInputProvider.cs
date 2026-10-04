/*
* ┌──────────────────────────────────┐
* │  描    述: 旧 Input Manager 输入实现，鼠标与单指触摸等价
* │  类    名: MouseInputProvider.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* Input.GetMouseButton 在 Android 上等价单指按下，因此不需要两套逻辑
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

        /// <summary>
        /// 每帧采样一次，须在控制器 Tick 内调用，顺序固定才能保证帧内一致
        /// </summary>
        public void Tick()
        {
            Vector3 current = Input.mousePosition;

            IsPressed = Input.GetMouseButton(0);
            PressBegan = Input.GetMouseButtonDown(0);
            DragDeltaPixels = IsPressed ? current - m_lastPosition : Vector2.zero;

            m_lastPosition = current;
        }
    }
}