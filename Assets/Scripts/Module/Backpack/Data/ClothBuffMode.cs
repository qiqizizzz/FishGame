/*
* ┌──────────────────────────────────┐
* │  描    述: 布料 BUFF 的计价方式，决定这块布给加成时按格数算还是整块定额
* │  类    名: ClothBuffMode.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* PerCell 是默认口径，每占一格给一份，适合线长、诱鱼、卖价这些允许小数的能力线
* Flat 是整块定额，不管剪成几格都给同一个数，专给负载这种取整才有意义的线
* 负载线一旦走 PerCell 就会出「3 格 x 0.25 = 0.75，取整归零」这种花金币买废品的事故
*/

namespace Module.Backpack.Data
{
    /// <summary>
    /// 布料 BUFF 的计价方式
    /// </summary>
    public enum ClothBuffMode
    {
        /// <summary>按占格数累加，取量 = 每格额度 x 占格数</summary>
        PerCell = 0,

        /// <summary>整块布定额给值，与占格数无关</summary>
        Flat = 1
    }
}
