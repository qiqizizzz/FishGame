/*
* ┌──────────────────────────────────┐
* │  描    述: 背包格子的操作入口，视图把旋转、挪位、取回交给宿主面板去做
* │  类    名: IShopBackpackHost.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 视图只负责把手势翻译成「第几列第几行」，摆不放摆得放一律由模型裁决，视图不自己改状态
* 宿主做完任何一笔操作都要重铺一次界面，摆不成时靠重铺把物品弹回原位
* 布与装备分两套入口：装备动的只是压在布上的那一件，布动的是一整块底板
* 布不吃单击旋转，它上头压着装备、卖掉还换金币，误点的代价比装备大得多，一律走选中后的操作条
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

        /// <summary>
        /// 把一块布挪到新的锚点格，压在它上面的装备会跟着让位
        /// </summary>
        /// <param name="plate">要挪的布</param>
        /// <param name="anchorColumn">落点锚列</param>
        /// <param name="anchorRow">落点锚行</param>
        bool TryMovePlate(BackpackItemInstance plate, int anchorColumn, int anchorRow);

        /// <summary>
        /// 原地转一档布的朝向
        /// </summary>
        /// <param name="plate">要转的布</param>
        bool TryRotatePlate(BackpackItemInstance plate);

        /// <summary>
        /// 把一块布从网格拆进暂存架，拆下来的布既不铺格也不给 BUFF
        /// </summary>
        /// <param name="plate">要拆的布</param>
        bool TryStashPlate(BackpackItemInstance plate);

        /// <summary>
        /// 把一块布从网格拆走换成金币，布不进暂存架
        /// </summary>
        /// <param name="plate">要卖的布</param>
        bool TrySellPlate(BackpackItemInstance plate);

        /// <summary>
        /// 点中一块布，是选中还是取消由宿主判，视图不自己保管选中态
        /// </summary>
        /// <param name="plate">被点的布</param>
        void SelectPlate(BackpackItemInstance plate);

        /// <summary>
        /// 问一块布这会儿是不是选中的，视图铺布色时用来提亮
        /// </summary>
        /// <param name="plate">要问的布</param>
        bool IsPlateSelected(BackpackItemInstance plate);
    }
}
