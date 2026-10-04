/*
* ┌──────────────────────────────────┐
* │  描    述: 一条在场鱼的运行时实例，纯数据不含 Unity 组件
* │  类    名: FishInstance.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 表现变换与业务状态分开存，FishAiService 批量推进时不碰 MonoBehaviour.Update
*/

using Module.Fish.Data;
using Module.Fish.Type;
using UnityEngine;

namespace Module.Fish.Model
{
    public sealed class FishInstance
    {
        public FishDataSO Data;
        public GameObject View;
        public Transform Root;

        /// <summary>所属鱼种的行为模板</summary>
        public FishBehaviorType Behavior;

        /// <summary>生成时锁定的深度（米），巡游在此深度附近起伏</summary>
        public float BaseDepth;

        /// <summary>当前横向坐标（米）</summary>
        public float X;

        /// <summary>当前深度（米）</summary>
        public float Depth;

        /// <summary>巡游方向，+1 向右，-1 向左</summary>
        public int Direction;

        /// <summary>巡游速度（米/秒），生成时按档位体型抖动</summary>
        public float Speed;

        /// <summary>相位偏移，让同屏各鱼的摆尾与起伏不同步</summary>
        public float Phase;

        /// <summary>群游编组号，同号共享一个虚拟领队点</summary>
        public int SchoolId;

        /// <summary>群游成员相对编组中心的固定偏移（米），保证成团不散</summary>
        public Vector2 Offset;

        /// <summary>是否已挂在钩子上，为真时退出 AI 驱动，改由携带服务定位</summary>
        public bool IsHooked;

        /// <summary>惊散剩余秒数，大于 0 时高速逃离，结束后恢复原行为</summary>
        public float FleeTimer;

        public bool IsAlive => View != null;
    }
}