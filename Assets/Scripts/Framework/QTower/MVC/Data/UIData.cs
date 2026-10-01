/*
 * ┌──────────────────────────────────┐
 * │  描    述: UI 注册数据，保存资源地址、父节点、控制器与排序信息
 * │  类    名: UIData.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 */

using Framework.QTower.Controller;
using UnityEngine;

namespace Framework.QTower
{
    public sealed class UIData
    {
        public string Address { get; set; }
        public Transform Parent { get; set; }
        public BaseController Controller { get; set; }
        public int SortingOrder { get; set; }
    }
}
