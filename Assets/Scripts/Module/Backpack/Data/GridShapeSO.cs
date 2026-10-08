/*
* ┌──────────────────────────────────┐
* │  描    述: 网格形状模板，俄罗斯方块式背包的最小占位单元
* │  类    名: GridShapeSO.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 模板库与装备解耦：一件装备在第几档用哪个模板由此处的形状决定，格数由形状推导
* 位图不在 Inspector 手填数字，由 Editor 下的网格绘制器点选写入
* 格数下限 2 上限 6 是设计约束：单格件没有取舍可言，超过 6 格在小尺寸容器里铺不开
*/

using UnityEngine;
using Utils.log;

namespace Module.Backpack.Data
{
    [CreateAssetMenu(menuName = "FishGame/Config/Grid Shape", fileName = "GridShape")]
    public sealed class GridShapeSO : ScriptableObject
    {
        // ==================== 常量 ====================
        public const int MIN_CELLS = 2;
        public const int MAX_CELLS = 6;

        // ==================== 字段[外部设置] ====================
        [Tooltip("程序内唯一标识，商店与存档按这个键找模板")]
        [SerializeField] private string ShapeId;
        [Tooltip("界面显示名")]
        [SerializeField] private string ShapeLabel;
        [Tooltip("占用位图，位序 = 行 × 8 + 列，请用 Inspector 顶部的网格绘制器编辑")]
        [HideInInspector] [SerializeField] private long MaskBits;

        // ==================== 字段[私有] ====================
        private Model.ShapeMask m_base;
        private Model.ShapeMask[] m_orientations;
        private bool m_parsed;

        // ==================== 属性 ====================
        public string Id => ShapeId;
        public string Label => ShapeLabel;
        public long Bits => MaskBits;
        public Model.ShapeMask Base => baseMask();
        public Model.ShapeMask[] Orientations => orientations();
        public int CellCount => baseMask().CellCount;
        public int OrientationCount => orientations().Length;

        // 形状是否可用作模板：非空、格数合规、四连通
        public bool IsValid
        {
            get
            {
                Model.ShapeMask mask = baseMask();

                return mask.CellCount >= MIN_CELLS && mask.CellCount <= MAX_CELLS && mask.IsConnected();
            }
        }

        // ==================== Public Function ====================
        /// <summary>
        /// 取指定朝向的形状，索引自动回绕，越界不抛只绕，界面反复点旋转不会崩
        /// </summary>
        /// <param name="orientationIndex">朝向索引</param>
        public Model.ShapeMask OrientationAt(int orientationIndex)
        {
            Model.ShapeMask[] list = orientations();

            if (list.Length == 0)
                return baseMask();

            int index = ((orientationIndex % list.Length) + list.Length) % list.Length;
            return list[index];
        }

        /// <summary>
        /// 丢弃解析缓存，供绘制器改到位图后强制重算
        /// </summary>
        public void InvalidateCache()
        {
            m_parsed = false;
        }

        // ==================== Private Function ====================
        private Model.ShapeMask baseMask()
        {
            parseIfNeeded();
            return m_base;
        }

        private Model.ShapeMask[] orientations()
        {
            parseIfNeeded();
            return m_orientations;
        }

        private void parseIfNeeded()
        {
            if (m_parsed)
                return;

            Model.ShapeMask.TryCreate(MaskBits, out m_base);
            m_orientations = m_base.CellCount > 0 ? Model.ShapeMask.BuildOrientations(m_base) : System.Array.Empty<Model.ShapeMask>();
            m_parsed = true;
        }

        private void OnValidate()
        {
            m_parsed = false;

            if (string.IsNullOrEmpty(ShapeId))
                QLog.Error($"形状模板缺少标识，无法被商店与存档引用：{name}");

            if (MaskBits == 0)
            {
                QLog.Error($"形状模板没有点亮任何格：{ShapeId}");
                return;
            }

            if (!Model.ShapeMask.TryCreate(MaskBits, out Model.ShapeMask mask))
            {
                QLog.Error($"形状模板位图解析失败：{ShapeId}");
                return;
            }

            if (mask.CellCount < MIN_CELLS || mask.CellCount > MAX_CELLS)
                QLog.Error($"形状模板格数必须在 {MIN_CELLS} 到 {MAX_CELLS} 之间：{ShapeId} 当前 {mask.CellCount} 格");

            if (!mask.IsConnected())
                QLog.Error($"形状模板不连通，无法一次摆放：{ShapeId}");
        }
    }
}
