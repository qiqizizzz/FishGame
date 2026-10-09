/*
* ┌──────────────────────────────────┐
* │  描    述: BUFF 阵营定义，只存阵营标识与纸页底色
* │  类    名: BuffFactionSO.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 颜色是阵营唯一的职责，能力线、每格值与封顶一律挂在纸页上
* 同一阵营可以有多张纸页，纸页共享这一张色卡，界面染色只问这里
*
* 已被 ClothCatalogSO 取代：颜色改成直接写在布料总表的每一行上，不再有独立阵营资产，
* 本类只是遗留容器，右键新建入口已撤掉，要清理旧资产时说一声即可
*/

using UnityEngine;
using Utils.log;

namespace Module.Backpack.Data
{
    public sealed class BuffFactionSO : ScriptableObject
    {
        // ==================== 字段[外部设置] ====================
        [Tooltip("程序内唯一标识，例如 swift")]
        [SerializeField] private string FactionId;
        [Tooltip("界面显示的阵营名，例如 迅捷")]
        [SerializeField] private string FactionLabel;
        [Tooltip("阵营下所有纸页的底色，一律取低饱和，装备才走高饱和档位色")]
        [SerializeField] private Color FactionPageColor = new Color32(0xC9, 0xCD, 0xD2, 0xFF);

        // ==================== 属性 ====================
        public string Id => FactionId;
        public string Label => FactionLabel;
        public Color PageColor => FactionPageColor;

        // ==================== Private Function ====================
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(FactionId))
                QLog.Error($"[{nameof(BuffFactionSO)}] 阵营缺少标识：{name}");
        }
    }
}
