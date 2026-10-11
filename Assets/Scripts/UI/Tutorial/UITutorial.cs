/*
* ┌──────────────────────────────────┐
* │  描    述: 教程界面，上方页签下方内容，按玩法逐条讲清操作与规则
* │  类    名: UITutorial.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 文案全部内置在下面两张表里，改文案不用回 Unity 动 prefab
* 每个条目写成「标题\n正文」一段字符串，首个换行前是标题，之后都是正文
* 文案只讲规则和手感，不写具体数值，免得调参数时两头对不上
* 正文用 <color> 标出重点，一个条目最多两处，别铺太多
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

        // 正文里的强调色直接写在字符串里：#1D6E5F 标操作要领与关键规则，#B03A2E 标代价和容易吃亏的地方

        private static readonly string[] PAGE_TITLES =
        {
            "怎么操作",
            "一局流程",
            "下潜收线",
            "鱼和价钱",
            "商店背包",
            "装备图鉴"
        };

        private static readonly string[][] PAGE_SECTIONS =
        {
            // 怎么操作
            new[]
            {
                "电脑上\n鼠标左键管钩子：<color=#1D6E5F>按住往下放，松手就往回收</color>。鱼竿左右挪用 <color=#1D6E5F>A 和 D</color>，方向键也认，哪套顺手用哪套。放钩收线这一路都能横着找鱼，不用停下来等。半截不想钓了按 <color=#1D6E5F>Esc</color>，退出这局回主菜单。",
                "手机上\n手指<color=#1D6E5F>按着屏幕就是放钩，抬指就收线</color>。按着不放左右拖，钩子跟着手指走，比鼠标好控制。手机上没有别的按键要记。",
                "出界了怎么办\n河道就这么窄一条，钩子贴到两边的壁<color=#B03A2E>就再也挪不动</color>。这时候别硬拖，<color=#1D6E5F>松手收线重新放一竿</color>就是，鱼还在那儿等着你。手机上手指拖到屏幕边上也是同理，不会卡死。",
                "右上角那两个\n齿轮是设置，音量和静音都在里面调；问号就是这本教程，忘了什么随时点开来看。"
            },

            // 一局流程
            new[]
            {
                "先逛商店再下水\n开局手里那点钱，先去换装备和布。<color=#B03A2E>只有摆进背包格子的才算穿上</color>，搁在仓库里的不生效，别看它躺在包里就以为起作用了。",
                "一回合三竿\n每回合只给你<color=#1D6E5F>三次下潜</color>的机会，用完就结算走人。<color=#B03A2E>空手回来照样扣一次</color>，别为了省机会在水面上干耗着。",
                "达标就往下走\n这一回合捞的鱼值多少钱，跟屏幕上写着的数目比。<color=#1D6E5F>够数就进下一回</color>，下一回要得更多；不够就到此为止，这局算钓完了。",
                "钱是两本账\n鱼的价值记的是回合目标，真正能花的钱<color=#B03A2E>还要再折一道</color>才进钱包。看着钓得挺多，口袋涨得却慢，这不是错觉，是两本账没记在一块儿。"
            },

            // 下潜收线
            new[]
            {
                "放钩下去\n按住之后钩子不紧不慢匀速往下走。这一路碰上的鱼，只要不是你想要的，<color=#B03A2E>蹭一下就把它惊散了</color>，想钓就得提前绕开。放到鱼线尽头<color=#1D6E5F>会自己转成收线</color>，不用掐着秒表算。",
                "卷线才上鱼\n<color=#1D6E5F>真正挂住鱼是在往回卷的时候</color>，贴着鱼身蹭过去就算挂上了。一趟能带几条回水面，看的是竿子的载量，名额满了再蹭也不进钩。",
                "河豚\n圆鼓鼓那位不算鱼，是个麻烦。<color=#B03A2E>撞上会缠住钩子好一阵子</color>，这期间什么都干不了，钩上已经挂住的鱼还有可能被它甩掉。看到鼓肚子的就绕，别去赌。",
                "出水结算\n三次机会用完才结算，中途自己浮上去不算数。鱼一露头就稳稳记进这一回合的渔获，<color=#1D6E5F>之后掉不下去</color>，可以放心。"
            },

            // 鱼和价钱
            new[]
            {
                "越深越值钱\n从水面往下，鱼的身价一路往上涨。浅处的小杂鱼不值钱，深处的稀罕货一条能顶你捞半天。前提是<color=#1D6E5F>你够得着</color>，线不够长，再贵也只能看着眼馋。",
                "看星级\n名字下面<color=#1D6E5F>标的星越多越难得，也越经钓</color>。河豚不标星，它只添乱。",
                "价钱怎么抬上去\n同一条鱼，<color=#1D6E5F>挂住的深度越深就越值钱</color>。一趟收线里多带几条，还有连带的好处。想赚就两条路：奔深处，再想办法多带。",
                "鱼的脾气\n有的鱼会躲，钩子一靠近就溜，<color=#1D6E5F>得靠横移追上去</color>；有的随水漂，别停在它跟前等它撞上来，主动送过去更稳。"
            },

            // 商店背包
            new[]
            {
                "货架\n每回合换一批货。前面几件随便看，后面的格子要掏钱才摊开，<color=#1D6E5F>开过的以后一直给你留着</color>。不满意可以刷新，<color=#B03A2E>刷新费一次比一次贵</color>，越刷越不划算，能将就就将就。",
                "什么时候出好货\n打到中后期，白送的格子里也会冒出顶级装备，越往后越值得多逛几轮。所以前期<color=#1D6E5F>别把钱一口气花光</color>。",
                "背包\n格子就那么些，装备和布都得摆进去才算数，怎么摆挺费心思。塞不下就先放仓库，<color=#B03A2E>仓库只是暂存，一点属性都不加</color>。",
                "布\n布看占几个格子和料子档次定价。同一块料有好几种形状，<color=#1D6E5F>省格子的那种往往更划算</color>。最便宜的空白布没有属性，纯粹拿来填缝。",
                "合成\n两件一模一样的能拼成更高一档，<color=#1D6E5F>不花钱，只占格子</color>。看中哪件就攒够两件再升，前期东一件西一件最容易浪费。"
            },

            // 装备图鉴
            new[]
            {
                "想下得更深更快\n卷线手柄收得利落，配重铅坠压得下去，延长鱼线是把线直接放长。深水那些贵鱼够不着，<color=#1D6E5F>就先在这几样上想办法</color>。",
                "追得上还得挂得住\n流线钩身横移利索，溜得快的鱼也追得上；转环轴承手感稳，钩子跟手不飘；宽钩门挂上的鱼不容易跑。<color=#1D6E5F>光追上不挂住，等于白跑一趟</color>。",
                "少出岔子\n防咬手套管的是被咬住脱钩，快解刀管缠住的东西，<color=#B03A2E>缠得越久损失越大</color>。活水舱和教学鱼竿是一类用处，但它更省格子。",
                "引鱼和卖钱\n发光拟饵和强力鱼饵都是把鱼往钩边引，后者更猛，<color=#B03A2E>代价是占地方</color>。老主顾和提秤管卖钱那一头，同样一篓鱼能多换些金币。",
                "教学鱼竿\n开局给的那根，竿身结实，<color=#1D6E5F>一趟能多带几条回来</color>，早期也没别的东西管载量。它占格子大，却是这段日子最实在的一条，别急着换掉。",
                "布的加成\n下潜和横移看深潜、快潜、迅捷；收线和手感看轻收、稳手；带几条看重载；引鱼和钩子看诱鱼、广钩；钱和出岔子那几摊看财运、防脱、快解。<color=#1D6E5F>碰到少见的整块好布，先抢下来再说</color>。"
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
