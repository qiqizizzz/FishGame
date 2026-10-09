/*
* ┌──────────────────────────────────┐
* │  描    述: 背包存放物的种类，区分装备与布料板块
* │  类    名: BackpackDefKind.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
*/

namespace Module.Backpack.Data
{
    /// <summary>
    /// 背包里能放的两类东西：装备吃档位与售价表，布料板块只占格并按异形给常驻小额加成
    /// </summary>
    public enum BackpackDefKind
    {
        /// <summary>装备藏品，一到三级，升档换形状也换强度</summary>
        Item = 0,

        /// <summary>布料板块，单档，颜色是这块布自己的</summary>
        Cloth = 1
    }
}
