/*
* ┌──────────────────────────────────┐
* │  描    述: HUD 单行计数器，标签与数值文本的绑定与刷新
* │  类    名: UIHudCounterItem.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 数值每帧都可能被调用，这里做一层内容去重，避免 TMP 无谓地重建网格
*/

using Framework.QTower.View;
using TMPro;
using UnityEngine;

namespace UI.RoundHUD
{
    public sealed class UIHudCounterItem : UIItemBase
    {
        private const string LABEL_PATH = "Txt_Label";
        private const string VALUE_PATH = "Txt_Value";

        private TMP_Text m_valueText;
        private string m_lastValue;

        /// <summary>
        /// 绑定行内文本，prefab 就绪后由 HUD 调用一次
        /// </summary>
        /// <param name="label">左侧固定标签文案</param>
        public void Setup(string label)
        {
            FindComponent<TMP_Text>(LABEL_PATH).text = label;
            m_valueText = FindComponent<TMP_Text>(VALUE_PATH);
        }

        /// <summary>
        /// 刷新数值文本，内容未变则直接返回
        /// </summary>
        public void SetValue(string value)
        {
            if (value == m_lastValue)
                return;

            m_lastValue = value;
            m_valueText.text = value;
        }
    }
}