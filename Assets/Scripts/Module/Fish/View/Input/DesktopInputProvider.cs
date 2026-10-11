/*
* ┌──────────────────────────────────┐
* │  描    述: PC 输入实现，左键放线、A/D 控制鱼竿左右飘动
* │  类    名: DesktopInputProvider.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 拖拽不适合 PC：指针位移是相对行程，钩子顶到井壁时指针也撞到屏幕边缘，
* 想继续横移只能松手重新起手，而松手等于立刻转回收，这一局直接作废
* 按键上报的是「意图」而不是「行程」，按住就一直推到井壁停住，不存在出界
* DragDeltaPixels 恒为 0，指针彻底不碰横向，避免两套输入在同一帧互相拉扯
*/

using UnityEngine;

namespace Module.Fish.View
{
    public sealed class DesktopInputProvider : IInputProvider
    {
        // 放线键沿用鼠标左键，与移动端「按住即放线」保持同一条规则
        private static readonly KeyCode[] m_pressKeys = { KeyCode.Mouse0 };
        private static readonly KeyCode[] m_leftKeys = { KeyCode.A, KeyCode.LeftArrow };
        private static readonly KeyCode[] m_rightKeys = { KeyCode.D, KeyCode.RightArrow };

        public bool IsPressed { get; private set; }

        public bool PressBegan { get; private set; }

        public Vector2 DragDeltaPixels => Vector2.zero;

        public float LateralAxis { get; private set; }

        /// <summary>
        /// 每帧采样一次按键状态，须在控制器 Tick 内调用，顺序固定才能保证帧内一致
        /// </summary>
        /// <param name="deltaTime">本帧时长，按键只报意图轴，折算成米由控制器完成</param>
        public void Tick(float deltaTime)
        {
            IsPressed = isAnyHeld(m_pressKeys);
            PressBegan = isAnyDown(m_pressKeys);
            LateralAxis = readLateralAxis();
        }

        // 左右同按视为互相抵消，宁可原地不动，也不要朝某一边乱窜
        private float readLateralAxis()
        {
            bool left = isAnyHeld(m_leftKeys);
            bool right = isAnyHeld(m_rightKeys);

            if (left == right)
                return 0f;

            return right ? 1f : -1f;
        }

        private bool isAnyHeld(KeyCode[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
                if (Input.GetKey(keys[i]))
                    return true;

            return false;
        }

        private bool isAnyDown(KeyCode[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
                if (Input.GetKeyDown(keys[i]))
                    return true;

            return false;
        }
    }
}
