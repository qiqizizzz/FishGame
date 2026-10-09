/*
* ┌──────────────────────────────────┐
* │  描    述: 背包格子的操作入口，视图把旋转、挪位、取回交给宿主面板去做
* │  类    名: IShopBackpackHost.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 视图只负责把手势翻译成「第几列第几行」，摆不放摆得放一律由模型裁决，视图不自己改状态
* 宿主做完任何一笔操作都要重铺一次界面，摆不成时靠重铺把物品弹回原位
*/

using Module.Backpack.Model;

namespace UI.Shop
{
    public interface IShopBackpackHost
    {
        /// <summary>网格列数，拖拽换算落点用</summary>
        int Columns { get; }

        /// <summary>网格行数，拖拽换算落点用</summary>
        int Rows { get; }

        /// <summary>
        /// 原地转一档
        /// </summary>
        /// <param name="item">要转的存放物</param>
        bool TryRotate(BackpackItemInstance item);

        /// <summary>
        /// 挪到指定的锚点格
        /// </summary>
        /// <param name="item">要挪的存放物</param>
        /// <param name="anchorColumn">落点锚列</param>
        /// <param name="anchorRow">落点锚行</param>
        bool TryMove(BackpackItemInstance item, int anchorColumn, int anchorRow);

        /// <summary>
        /// 从暂存架取回一件上网格
        /// </summary>
        /// <param name="stashIndex">暂存架下标</param>
        bool TryRecallFromStash(int stashIndex);
    }
}
