/*
* ┌──────────────────────────────────┐
* │  描    述: 教程界面，左侧页签右侧内容，按玩法逐条讲清操作与规则
* │  类    名: UITutorial.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 文案全部内置在下面两张表里，改文案不用回 Unity 动 prefab
* 每个条目写成「标题\n正文」一段字符串，首个换行前是标题，之后都是正文
* 页签与条目都由隐藏模板复制出来留用，切页只开关不销毁，避免同帧重建把布局算乱
*/

using System;
using System.Collections.Generic;
using Framework.QTower.View;
using Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Tutorial
{
    public sealed class UITutorial : UIBase
    {
        private const string TAB_ROOT_PATH = "Panel/Panel_Tabs";
        private const string TAB_TEMPLATE_PATH = "Panel/Panel_Tabs/Tab_Template";
        private const string SCROLL_PATH = "Panel/Panel_Content/Scroll_Content";
        private const string CONTENT_PATH = "Panel/Panel_Content/Scroll_Content/Viewport/Content_Sections";
        private const string SECTION_TEMPLATE_PATH = "Panel/Panel_Content/Scroll_Content/Viewport/Content_Sections/Section_Template";
        private const string HEADING_NAME = "Txt_Heading";
        private const string BODY_NAME = "Txt_Body";
        private const string TAB_TEXT_NAME = "Txt_Tab";
        private const string CLOSE_BUTTON_PATH = "Panel/Btn_Cross";

        private const string TITLE_SEPARATOR = "\n";
        private const int FIRST_PAGE = 0;

        private static readonly string[] PAGE_TITLES =
        {
            "操作方式",
            "玩法流程",
            "下潜收线",
            "鱼与收益",
            "商店背包",
            "装备图鉴"
        };

        private static readonly string[][] PAGE_SECTIONS =
        {
            // 操作方式
            new[]
            {
                "PC 端\n按住鼠标左键 —— 抛钩下潜\n松开左键 —— 立刻收线\nA 或 ← —— 鱼竿左移\nD 或 → —— 鱼竿右移\nEsc —— 返回主菜单\n右上角齿轮 = 设置，问号 = 本教程",
                "安卓端\n按住屏幕 —— 抛钩下潜\n按住不放左右拖动 —— 鱼竿跟着手指走\n松手 —— 立刻收线\n右上角齿轮与问号 —— 设置与教程",
                "两端共同规则\n下潜和收线途中都能横向移动，钩子蹭到鱼身就能挂上\n河道只有 5 米宽，钩子贴到井壁就推不动了\nPC 左右键同按会互相抵消，宁可停在原地也不乱窜"
            },

            // 玩法流程
            new[]
            {
                "一局怎么走\n开局带 100 金币进商店\n每回合固定顺序：商店采购 → 下水 3 次 → 收竿结算\n渔获价值达标就进下一回合，不达标当场封盘",
                "回合目标\n第 1 到 8 回合的目标依次是 90 / 125 / 170 / 230 / 305 / 400 / 520 / 680\n第 8 回合之后目标维持 680，能撑几回合全看背包长多快",
                "两种钱\n渔获价值全额计入回合目标，达标只看这个数\n渔获只有 50% 折算成金币进钱包，商店只能花钱包里的钱\n卖掉布料按基准价的 50% 回收",
                "河道\n井道总深 100 米，越深的鱼越值钱\n初始鱼线只有 25 米，够不到的深度再贵也是空的\n鱼群密度随深度往下递减，越深越难找"
            },

            // 下潜与收线
            new[]
            {
                "下潜\n按住后钩子以 6 米每秒匀速下潜\n下潜途中撞到任何一条鱼都算失误，鱼会被当场惊散\n到达线长尽头自动转收线，不用自己掐表",
                "收线\n松手后以 4.5 米每秒往回卷\n收线途中贴到鱼身就把鱼挂上钩\n一次最多带 3 条回水面，名额满了就钓不进去了",
                "河豚\n碰到河豚会缠钩 1.2 秒，这段时间什么都做不了\n缠钩时钩上已有的鱼有 35% 概率被甩掉\n河道里约 15% 的位置是河豚，看到鼓肚子的圆鱼就绕开",
                "出水\n三次下潜全部用完才结算，空手出水也照扣一次机会\n鱼一出水就直接计入本回合渔获，中途不会再掉"
            },

            // 鱼与收益
            new[]
            {
                "稀有度分档\n常见 1 星 / 少见 2 星 / 稀有 3 星 / 史诗 4 星 / 传说 5 星\n危险档不标星，目前只有河豚\n出现深度：常见 0-22 米 / 少见 14-34 米 / 稀有 28-46 米 / 史诗 40-58 米 / 传说 48-68 米",
                "鱼种标价\n极小杂鱼 5　小丑鱼 12\n金鱼 20　神仙鱼 26\n黄箱豚 45　鲈鱼 60\n安康鱼 95　桨鱼 120\n虹彩微鱼 130　金枪鱼 260",
                "价值怎么算\n单条价值 = 标价 × 深度加成 × 连击加成\n深度加成：挂钩深度越接近 40 米越高，最多加 60%\n连击加成：同一次回收里每多带一条加 8%，一趟三条时最高 1.16 倍",
                "有脾气的鱼\n虹彩微鱼会闪避，钩子一靠近就溜，得把横移练快\n桨鱼随水流飘，别贴着它等它自己撞上来\n河豚不产任何收益，只会缠钩添乱"
            },

            // 商店与背包
            new[]
            {
                "货架\n每回合上架 10 件，前 5 件免费看\n后 5 件要花钱开格：50 / 80 / 110 / 150 / 200，开过的格子永久保留\n刷新一次 10 金币起，每刷一次价格乘 1.6，最高 60\n第 5 回合起免费格也能刷出最高档装备，付费格提前到第 4 回合",
                "背包\n6 列 5 行的格子，装备和布都要占格摆进去\n只有摆进格子的东西才生效，另有 7 格仓库用来暂存\n开局自带教学鱼竿与 2 块空白布",
                "价格\n装备按档位 80 / 200 / 560\n布料价格 = 每格基价 × 占格数 × 稀有度倍率\n每格基价：空白布 8，普通布 14，珍罕布 16\n稀有度倍率：常见 1 / 少见 1.6 / 稀有 2.4 / 珍罕 4",
                "合成与异形\n2 件同类装备可以升 1 档，不收金币\n同一块布有几种异形可选，占格从 2 到 6 不等\n空白布没有属性，胜在便宜，拿来垫满格子很划算"
            },

            // 装备图鉴
            new[]
            {
                "下潜与收线\n卷线手柄：收线卷得更快，一次下潜能多跑一个来回\n配重铅坠：钩身更沉，下潜到目标水层省下的时间直接变成条数\n延长鱼线：把鱼线放得更长，钓钩够得着更深的水层，贵鱼就在那里",
                "追得上、挂得住\n流线钩身：钩身阻力小，横向摆得更快，追得上溜走的鱼\n转环轴承：转环顺滑，钓钩跟手不飘，全凭手感的一条线\n宽钩门：钩门更宽，挂住的鱼就不容易脱钩跑掉，挂得住就是钱",
                "少出事\n防咬手套：手套挡牙口，被咬住时脱钩的概率更低\n快解刀：缠线一割就开，卡住浪费的时间更短\n活水舱：舱里养得住鱼，载量和教学鱼竿一样，但省下一整块格子",
                "引鱼与变现\n发光拟饵：在水下发亮，把远处的鱼引到钩边，占格紧凑前期就有效\n强力鱼饵：气味浓烈，聚鱼最强的一档，代价是铺掉一大片格子\n老主顾：相熟的收购商愿意多出点价，卖鱼时金币更值钱\n提秤：秤准分量足，卖价加成本身更高，只是格子宽了才划算",
                "教学鱼竿\n竿身结实，一次起钩能多带几条鱼回来，占格大但收益最猛\n开局就送的那根，也是前期唯一的载量来源",
                "布料加成\n深潜布 线长｜快潜布 下潜｜迅捷布 横移｜轻收布 收线\n稳手布 跟手｜重载布 载量｜诱鱼布 引鱼｜广钩布 钩径\n财运布 金币｜防脱布 防脱钩｜快解布 解缠\n珍罕档顺流布、满篓布、韧网布一块顶三条，见到就抢"
            }
        };

        // ==================== 字段[外部设置] ====================
        [Header("页签精灵")]
        [Tooltip("页签未选中时使用的底板精灵")]
        [SerializeField] private Sprite TabNormalSprite;

        [Tooltip("页签选中时使用的底板精灵")]
        [SerializeField] private Sprite TabSelectedSprite;

        [Header("页签配色")]
        [Tooltip("页签未选中时的底板染色")]
        [SerializeField] private Color TabNormalColor = new Color(0.72f, 0.66f, 0.58f, 1f);

        [Tooltip("页签选中时的底板染色")]
        [SerializeField] private Color TabSelectedColor = Color.white;

        // ==================== 字段[私有] ====================
        private ScrollRect m_scrollRect;
        private RectTransform m_content;
        private Transform m_tabRoot;
        private GameObject m_tabTemplate;
        private GameObject m_sectionTemplate;
        private Button m_closeButton;

        // 复制出来的页签与条目只开关不销毁，销毁会延到帧尾，同帧重建会被布局算进内容高度
        private readonly List<Image> m_tabImages = new();
        private readonly List<GameObject> m_sections = new();
        private readonly List<TMP_Text> m_headings = new();
        private readonly List<TMP_Text> m_bodies = new();
        private int m_currentPage = FIRST_PAGE;

        // ==================== 生命周期 ====================
        protected override void OnInit()
        {
            m_tabRoot = FindNode(TAB_ROOT_PATH);
            m_tabTemplate = FindNode(TAB_TEMPLATE_PATH).gameObject;
            m_scrollRect = FindComponent<ScrollRect>(SCROLL_PATH);
            m_content = FindComponent<RectTransform>(CONTENT_PATH);
            m_sectionTemplate = FindNode(SECTION_TEMPLATE_PATH).gameObject;
            m_closeButton = FindComponent<Button>(CLOSE_BUTTON_PATH);

            m_closeButton.onClick.AddListener(handleCloseClicked);
            buildTabs();
        }

        protected override void OnOpen(params object[] args)
        {
            showPage(FIRST_PAGE);
        }

        // ==================== Private Function ====================
        // 页签数量固定来自标题表，只在初始化时铺一次，切页不动它
        private void buildTabs()
        {
            for (int i = 0; i < PAGE_TITLES.Length; i++)
            {
                GameObject tab = Instantiate(m_tabTemplate, m_tabRoot);
                tab.name = $"Tab_{i}";
                tab.SetActive(true);

                TMP_Text label = tab.transform.Find(TAB_TEXT_NAME).GetComponent<TMP_Text>();
                label.text = PAGE_TITLES[i];

                Image image = tab.GetComponent<Image>();
                m_tabImages.Add(image);

                int index = i;
                tab.GetComponent<Button>().onClick.AddListener(() => showPage(index));
            }
        }

        // 切页：重铺条目、刷新高亮、把滚动拉回顶部
        private void showPage(int pageIndex)
        {
            m_currentPage = pageIndex;

            for (int i = 0; i < m_tabImages.Count; i++)
            {
                bool selected = i == m_currentPage;
                m_tabImages[i].sprite = selected ? TabSelectedSprite : TabNormalSprite;
                m_tabImages[i].color = selected ? TabSelectedColor : TabNormalColor;
            }

            rebuildSections(PAGE_SECTIONS[pageIndex]);
        }

        private void rebuildSections(string[] sections)
        {
            ensureSectionCount(sections.Length);

            for (int i = 0; i < m_sections.Count; i++)
            {
                bool visible = i < sections.Length;
                m_sections[i].SetActive(visible);

                if (!visible)
                    continue;

                splitSection(sections[i], out string heading, out string body);
                m_headings[i].text = heading;
                m_bodies[i].text = body;
            }

            scrollToTop();
        }

        // 首个换行前是标题，剩下的整段是正文
        private void splitSection(string section, out string heading, out string body)
        {
            int separator = section.IndexOf(TITLE_SEPARATOR, StringComparison.Ordinal);

            if (separator < 0)
            {
                heading = section;
                body = string.Empty;
                return;
            }

            heading = section.Substring(0, separator);
            body = section.Substring(separator + TITLE_SEPARATOR.Length);
        }

        private void ensureSectionCount(int count)
        {
            while (m_sections.Count < count)
            {
                // 模板本身是隐藏的，复制出来的条目要先激活才会参与布局
                GameObject section = Instantiate(m_sectionTemplate, m_content);
                section.name = $"Section_{m_sections.Count}";
                section.SetActive(true);

                m_sections.Add(section);
                m_headings.Add(section.transform.Find(HEADING_NAME).GetComponent<TMP_Text>());
                m_bodies.Add(section.transform.Find(BODY_NAME).GetComponent<TMP_Text>());
            }
        }

        // 重铺之后把内容拉回顶部，否则会停在上一次翻页的滚动位置
        // 高度先压回 0 逼 ContentSizeFitter 按当前可见条目重算，位置直接写 anchoredPosition：
        // verticalNormalizedPosition 要等 ScrollRect 的 LateUpdate 算出 bounds 才生效，同帧赋值会被吃掉
        private void scrollToTop()
        {
            m_content.sizeDelta = new Vector2(m_content.sizeDelta.x, 0f);
            LayoutRebuilder.MarkLayoutForRebuild(m_content);
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_content);

            m_content.anchoredPosition = new Vector2(m_content.anchoredPosition.x, 0f);
            m_scrollRect.velocity = Vector2.zero;
        }

        private void handleCloseClicked()
        {
            GameApp.UIManager.Close<UITutorial>();
        }
    }
}
