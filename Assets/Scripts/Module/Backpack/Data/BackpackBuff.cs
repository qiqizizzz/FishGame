/*
* ┌──────────────────────────────────┐
* │  描    述: 一条能力线上的加成条目，连同它该受的封顶一起交给聚合侧
* │  类    名: BackpackBuff.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 存放物只负责报出「我往哪条线加了多少」，不懂封顶规则，规则写在给出条目的那一方身上
* CapOwner 为空表示这条加成不设来源封顶，直接进线去吃配置里的全局封顶，装备走这一条
* CapOwner 非空表示它隶属某个封顶主体，聚合时同一主体的同一条线先各自夹完再进线，布料走这一条
* 做成只读结构体是为了让 FillBuffs 能反复调用而不产生堆分配，一条线一个条目而已
*/

namespace Module.Backpack.Data
{
    /// <summary>
    /// 单条 BUFF 的结算条目，值与封顶成对给出，聚合侧照 CapOwner 分组
    /// </summary>
    public readonly struct BackpackBuff
    {
        // ==================== 属性 ====================
        /// <summary>本条加成落在哪条能力线上</summary>
        public BackpackStatMod Mod { get; }

        /// <summary>本件给出的加成量，负向线为负数</summary>
        public float Value { get; }

        /// <summary>封顶归属主体，为空表示不设来源封顶</summary>
        public object CapOwner { get; }

        /// <summary>归属主体在这条线上最多叠到多少，负向线为负数</summary>
        public float Cap { get; }

        /// <summary>本条是否需要先夹来源封顶再进线</summary>
        public bool IsCapped => CapOwner != null;

        // ==================== Constructor ====================
        public BackpackBuff(BackpackStatMod mod, float value, object capOwner, float cap)
        {
            Mod = mod;
            Value = value;
            CapOwner = capOwner;
            Cap = cap;
        }
    }
}
