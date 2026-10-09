/*
* ┌──────────────────────────────────┐
* │  描    述: 背包存放物的统一定义接口，装备与布料板块共用一条摆放链路
* │  类    名: IBackpackDef.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 实例、模型、商店、界面一律只认这个接口，新增存放物类型不用改摆放与聚合逻辑
* 形状仍按档位取，板块只有单档，把 MaxTier 报成 1 即可走同一条代码
* 加成一律走 FillBuffs 数组报出来，一条线一个条目，接口这边不再问「你改哪条线、改多少」
*/

using System.Collections.Generic;
using UnityEngine;

namespace Module.Backpack.Data
{
    public interface IBackpackDef
    {
        /// <summary>程序内唯一标识</summary>
        string Id { get; }

        /// <summary>界面显示名</summary>
        string Label { get; }

        /// <summary>图标，素材未就位时为空，界面按名称显示</summary>
        Sprite Icon { get; }

        /// <summary>本件是装备还是布料板块</summary>
        BackpackDefKind Kind { get; }

        /// <summary>最高档位，布料板块恒为 1</summary>
        int MaxTier { get; }

        /// <summary>
        /// 本件自身的底色：板块用布的颜色，装备不靠自身染色，返回白色由界面按档位取品质色
        /// </summary>
        Color TintColor { get; }

        /// <summary>
        /// 取指定档位的形状模板
        /// </summary>
        /// <param name="tier">档位，从 1 开始</param>
        GridShapeSO ShapeAt(int tier);

        /// <summary>
        /// 追加报出本件在指定档位上的全部加成，一条能力线一个条目
        /// </summary>
        /// <param name="tier">档位，从 1 开始</param>
        /// <param name="result">要追加条目的列表，由调用方负责清空</param>
        void FillBuffs(int tier, List<BackpackBuff> result);
    }
}
