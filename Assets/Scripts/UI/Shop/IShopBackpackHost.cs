/*
* ┌──────────────────────────────────┐
* │  描    述: 背包格子的操作入口，视图把旋转、挪位、取回交给宿主面板去做
* │  类    名: IShopBackpackHost.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 视图只负责把手势翻译成「第几列第几行」，摆不放摆得放一律由模型裁决，视图不自己改状态
* 单击一律只报选中与详情，旋转、下架、出售三件事全在选中后浮出的操作条上按
* 宿主做完任何一笔操作都要重铺一次界面，摆不成时靠重铺把物品弹回原位
* 布与装备分两套入口：装备动的只是压在布上的那一件，布动的是一整块底板
* 布与装备共用同一条操作条，两者也共用暂存架：拖进 Panel_Storage 那块区域就下架，从暂存格拖回网格按落点摆
* 格子上不再常驻名称，点中什么由宿主把两行说明报到详情区
*/

using Module.Backpack.Model;
using UnityEngine;

namespace UI.Shop
{
    public interface IShopBackpackHost
    {
        /// <summary>网格列数，拖拽换算落点用</summary>
        int Columns { get; }

        /// <summary>网格行数，拖拽换算落点用</summary>
        int Rows { get; }

        /// <summary>幽灵形状与拖拽落点都画在这一层，它的左上角就是网格第 0 格左上角</summary>
        RectTransform GhostLayer { get; }

        /// <summary>单格边长加间距，一格在屏幕上占多宽</summary>
        float CellPitch { get; }

        /// <summary>底层格子的底图，幽灵格抄它才和真格子一个样</summary>
        Sprite GhostCellSprite { get; }

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
        /// 把暂存架里的一件按落点格摆上网格，落点摆不下就原样留在架上
        /// </summary>
        /// <param name="stashIndex">暂存架下标</param>
        /// <param name="anchorColumn">落点包围盒左上角列</param>
        /// <param name="anchorRow">落点包围盒左上角行</param>
        bool TryPlaceFromStashAt(int stashIndex, int anchorColumn, int anchorRow);

        /// <summary>
        /// 指针落在暂存架那块面板上，从背包拖出去的东西就以这里为下架落点
        /// </summary>
        /// <param name="screenPoint">屏幕坐标</param>
        bool IsPointerInStash(Vector2 screenPoint);

        /// <summary>
        /// 把屏幕坐标换算成网格上的落点格，指到网格外就返回 false
        /// </summary>
        /// <param name="screenPoint">屏幕坐标</param>
        /// <param name="anchorColumn">落点锚列</param>
        /// <param name="anchorRow">落点锚行</param>
        bool TryPointerCell(Vector2 screenPoint, out int anchorColumn, out int anchorRow);

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
        /// 点中一件装备，同样是选中与取消二选一，顺带把详情报出去
        /// </summary>
        /// <param name="item">被点的装备</param>
        void SelectItem(BackpackItemInstance item);

        /// <summary>
        /// 一件装备被抓到手上，操作条先收起来，免得拖的时候满屏控件
        /// </summary>
        /// <param name="item">被拖起的装备</param>
        void BeginItemDrag(BackpackItemInstance item);

        /// <summary>
        /// 手上的装备落下或作废，操作条按落点之后的状态复原
        /// </summary>
        /// <param name="item">被放下的装备</param>
        void EndItemDrag(BackpackItemInstance item);

        /// <summary>
        /// 把网格上的一件装备收进暂存架，暂存架满了就收不动
        /// </summary>
        /// <param name="item">要收的装备</param>
        bool TryStashItem(BackpackItemInstance item);

        /// <summary>
        /// 一块布被抓到手上，它铺出来的那几格连底板都空了，格子要跟着改脸
        /// </summary>
        /// <param name="plate">被拖起的布</param>
        void BeginPlateDrag(BackpackItemInstance plate);

        /// <summary>
        /// 手上的布落下或作废，空出来的那几格交还给模型重染
        /// </summary>
        /// <param name="plate">被放下的布</param>
        void EndPlateDrag(BackpackItemInstance plate);

        /// <summary>
        /// 把一件存放物的名字与加成报到详情区，布与装备共用这一处出口
        /// </summary>
        /// <param name="item">被点中的存放物</param>
        void ShowDetail(BackpackItemInstance item);
    }
}
