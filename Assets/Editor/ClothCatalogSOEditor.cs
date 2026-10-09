/*
* ┌──────────────────────────────────┐
* │  描    述: 布料总表 Inspector，把一块布画成一行表，BUFF 与异形就地展开编辑
* │  类    名: ClothCatalogSOEditor.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 默认的数组抽屉会把一块布摊成七八层折叠框，调参时看不出全貌，这里按「一行一布」重画
* 字段仍走 SerializedProperty，撤销、脏标记、ApplyModifiedProperties 一条不缺，只换排版
* 预览数字全部现算：占格数、上架基准价、在带加成布池里的抽中占比，改完当场看到结果
* 异形按位图画小样，玩家会买到什么形状不用再去形状库里逐个翻
*/

using UnityEditor;
using UnityEngine;
using Module.Backpack.Data;

[CustomEditor(typeof(ClothCatalogSO))]
public sealed class ClothCatalogSOEditor : Editor
{
    // ==================== 常量与静态字段 ====================
    private const int SHAPE_ROW_STRIDE = 8;
    private const int SHAPE_TOTAL_BITS = 64;
    private const float CELL_PX = 9f;

    // 新增行的默认值，与 ClothEntry / ClothRarityTier 字段上的默认值保持一致
    private const int DEFAULT_CELL_PRICE = 14;
    private const float DEFAULT_DRAW_WEIGHT = 10f;
    private const float DEFAULT_PRICE_SCALE = 1f;

    private const float W_ARROW = 14f;
    private const float W_COLOR = 34f;
    private const float W_LABEL = 84f;
    private const float W_ID = 104f;
    private const float W_RARITY = 74f;
    private const float W_PRICE = 40f;
    private const float W_TIER_ID = 78f;
    private const float W_TIER_LABEL = 56f;
    private const float W_TIER_NUM = 46f;
    private const float W_MOD = 122f;
    private const float W_NUM = 52f;
    private const float W_SHAPE = 128f;
    private const float W_BTN = 20f;
    private const float LINE_H = 18f;
    private const float INDENT = 14f;

    // BUFF 能力线枚举里「未选」的下标，新增条目时用它落到空白而不是误填第一条线
    private static readonly int m_modNoneIndex = System.Array.IndexOf(System.Enum.GetNames(typeof(BackpackStatMod)), nameof(BackpackStatMod.None));

    // ==================== 字段[私有] ====================
    private SerializedProperty m_rarityList;
    private SerializedProperty m_clothList;

    // 字段名对不上只报一次，免得每趟绘制都往 Console 灌一条
    private bool m_missingFieldReported;

    // 增删落地时写的提示，下一趟绘制显示一次就丢，不当常驻消息
    private string m_pendingNotice;
    private string m_shownNotice;

    // 增删要等整趟绘制收尾再做，数组一边画一边改会让下标错位
    private int m_rarityInsertAt = -1;
    private int m_rarityRemoveAt = -1;
    private bool m_addCloth;
    private int m_clothRemoveAt = -1;
    private int m_buffAddCloth = -1;
    private int m_buffRemoveCloth = -1;
    private int m_buffRemoveAt = -1;
    private int m_shapeAddCloth = -1;
    private int m_shapeRemoveCloth = -1;
    private int m_shapeRemoveAt = -1;

    // ==================== Public Function ====================
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 上一趟增删留下的提示在这趟显示，显示完即丢
        m_shownNotice = m_pendingNotice;
        m_pendingNotice = null;

        m_rarityList = serializedObject.FindProperty("RarityTierList");
        m_clothList = serializedObject.FindProperty("ClothList");
        ClothCatalogSO catalog = (ClothCatalogSO) target;

        drawOverview(catalog);
        drawRarityTable(catalog);
        drawClothTable(catalog);
        drawHint();

        applyPendingOps();

        if (serializedObject.ApplyModifiedProperties())
            catalog.InvalidatePieceCache();
    }

    // ==================== Private Function ====================
    // 表头摘要：几档几块布、能拼出多少种上架板块、其中几块是空白布
    private void drawOverview(ClothCatalogSO catalog)
    {
        EditorGUILayout.LabelField("布料总表", EditorStyles.boldLabel);

        int pieceCount = 0;
        int blankCount = 0;

        for (int i = 0; i < catalog.ClothCount; i++)
        {
            ClothEntry cloth = catalog.ClothAt(i);

            if (cloth == null)
                continue;

            pieceCount += cloth.ShapeCount;

            if (cloth.IsBlank)
                blankCount++;
        }

        EditorGUILayout.LabelField($"{catalog.RarityTierCount} 档 · {catalog.ClothCount} 块布 · {pieceCount} 种上架板块 · 空白布 {blankCount} 块");
    }

    // 稀有度档表：一档一行，权重与价格系数并排，右侧给出该档在售卖池里的合计占比
    private void drawRarityTable(ClothCatalogSO catalog)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("稀有度档", EditorStyles.boldLabel);

        if (m_rarityList.arraySize == 0)
            EditorGUILayout.HelpBox("还没有稀有度档，布料引用的下标无处落地，价钱也只能按系数 1 算", MessageType.Warning);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("标识", GUILayout.Width(W_TIER_ID));
        EditorGUILayout.LabelField("档名", GUILayout.Width(W_TIER_LABEL));
        EditorGUILayout.LabelField("权重", GUILayout.Width(W_TIER_NUM));
        EditorGUILayout.LabelField("价系数", GUILayout.Width(W_TIER_NUM));
        EditorGUILayout.LabelField("布料数与抽中占比");
        EditorGUILayout.LabelField(string.Empty, GUILayout.Width(W_BTN));
        EditorGUILayout.EndHorizontal();

        float pool = buffPoolWeight(catalog);

        for (int i = 0; i < m_rarityList.arraySize; i++)
        {
            SerializedProperty element = m_rarityList.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginHorizontal();
            drawFlatField(element.FindPropertyRelative("TierId"), W_TIER_ID);
            drawFlatField(element.FindPropertyRelative("TierLabel"), W_TIER_LABEL);
            drawFlatField(element.FindPropertyRelative("DrawWeight"), W_TIER_NUM);
            drawFlatField(element.FindPropertyRelative("PriceScale"), W_TIER_NUM);

            ClothRarityTier tier = catalog.RarityAt(i);
            int sellable = countSellableAt(catalog, i);
            string share = tier == null || pool <= 0f
                ? "—"
                : $"{sellable} 块 · {sellable * tier.DrawWeightValue / pool * 100f:F1}%";
            EditorGUILayout.LabelField(share);

            if (rowButton("+"))
                m_rarityInsertAt = i;
            if (rowButton("-"))
                m_rarityRemoveAt = i;
            EditorGUILayout.EndHorizontal();
        }
    }

    // 布料表：表头加逐行，每行末尾一个折叠箭头，展开才画 BUFF 与异形
    private void drawClothTable(ClothCatalogSO catalog)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("布料", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(string.Empty, GUILayout.Width(W_ARROW));
        EditorGUILayout.LabelField("颜色", GUILayout.Width(W_COLOR));
        EditorGUILayout.LabelField("布名", GUILayout.Width(W_LABEL));
        EditorGUILayout.LabelField("标识", GUILayout.Width(W_ID));
        EditorGUILayout.LabelField("稀有度", GUILayout.Width(W_RARITY));
        EditorGUILayout.LabelField("格价", GUILayout.Width(W_PRICE));
        EditorGUILayout.LabelField("上架预览");
        EditorGUILayout.LabelField(string.Empty, GUILayout.Width(W_BTN));
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < m_clothList.arraySize; i++)
            drawClothRow(i, catalog);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("新增一块布", GUILayout.Width(104f)))
            m_addCloth = true;
        EditorGUILayout.LabelField("定价 = 格价 × 占格数 × 价系数，商店再叠一次浮动", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    // 一行一块布：折叠箭头、底色、布名、标识、稀有度下拉、单格基准价，末尾给上架预览
    private void drawClothRow(int index, ClothCatalogSO catalog)
    {
        SerializedProperty element = m_clothList.GetArrayElementAtIndex(index);
        ClothEntry cloth = catalog.ClothAt(index);
        float pool = buffPoolWeight(catalog);

        EditorGUILayout.BeginHorizontal();

        Rect arrowRect = GUILayoutUtility.GetRect(W_ARROW, LINE_H);
        element.isExpanded = EditorGUI.Foldout(arrowRect, element.isExpanded, GUIContent.none, true);

        drawFlatField(element.FindPropertyRelative("ClothColor"), W_COLOR);
        drawFlatField(element.FindPropertyRelative("ClothLabel"), W_LABEL);
        drawFlatField(element.FindPropertyRelative("ClothId"), W_ID);

        SerializedProperty rarityProperty = element.FindPropertyRelative("RarityTierIndex");
        string[] tierNames = rarityNames();

        if (tierNames.Length > 0)
        {
            Rect popupRect = GUILayoutUtility.GetRect(W_RARITY, LINE_H);
            int current = Mathf.Clamp(rarityProperty.intValue, 0, tierNames.Length - 1);
            int picked = EditorGUI.Popup(popupRect, current, tierNames);

            if (picked != rarityProperty.intValue)
                rarityProperty.intValue = picked;
        }

        drawFlatField(element.FindPropertyRelative("CellBasePrice"), W_PRICE);
        EditorGUILayout.LabelField(cloth == null ? "空槽位" : summarizeCloth(cloth, catalog, pool));

        if (rowButton("×"))
            m_clothRemoveAt = index;

        EditorGUILayout.EndHorizontal();

        if (!element.isExpanded || cloth == null)
            return;

        drawBuffList(index, element, cloth);
        drawShapeList(index, element, cloth, catalog);
    }

    // 一行看懂一块布：几条 BUFF、几种异形、抽中占比、跨所有异形的基准价区间
    private string summarizeCloth(ClothEntry cloth, ClothCatalogSO catalog, float pool)
    {
        if (cloth.ShapeCount == 0)
            return "没配异形，永远刷不上货架";

        string buffText = cloth.IsBlank ? "空白布" : $"{cloth.BuffCount} 条 BUFF";
        string shareText = cloth.IsBlank
            ? "空白池独占"
            : pool > 0f
                ? $"占带BUFF池 {catalog.DrawWeightOf(cloth.RarityIndex) / pool * 100f:F1}%"
                : "带BUFF池为空";

        int min = int.MaxValue;
        int max = int.MinValue;
        float scale = catalog.PriceScaleOf(cloth.RarityIndex);

        for (int i = 0; i < cloth.ShapeCount; i++)
        {
            GridShapeSO shape = cloth.Shapes[i];

            if (shape == null)
                continue;

            int price = Mathf.RoundToInt(cloth.CellBasePriceValue * shape.CellCount * scale);
            min = Mathf.Min(min, price);
            max = Mathf.Max(max, price);
        }

        string priceText = max == int.MinValue ? "价—" : min == max ? $"价 {min}" : $"价 {min}~{max}";

        return $"{buffText} · {cloth.ShapeCount} 种异形 · {shareText} · {priceText}";
    }

    // 展开体上半：这块布挂的 BUFF，一行一条，右侧现算叠到封顶要吃几格
    private void drawBuffList(int clothIndex, SerializedProperty clothElement, ClothEntry cloth)
    {
        SerializedProperty buffs = clothElement.FindPropertyRelative("BuffList");

        for (int i = 0; i < buffs.arraySize; i++)
        {
            SerializedProperty row = buffs.GetArrayElementAtIndex(i);
            ClothBuffEntry buff = i < cloth.BuffCount ? cloth.Buffs[i] : null;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(INDENT);
            drawFlatField(row.FindPropertyRelative("ModType"), W_MOD);
            drawFlatField(row.FindPropertyRelative("PerCell"), W_NUM);
            drawFlatField(row.FindPropertyRelative("Cap"), W_NUM);
            EditorGUILayout.LabelField(buff == null ? "空槽位" : buffHint(buff));

            if (rowButton("×"))
            {
                m_buffRemoveCloth = clothIndex;
                m_buffRemoveAt = i;
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(INDENT);

        if (GUILayout.Button("加一条 BUFF", GUILayout.Width(104f)))
            m_buffAddCloth = clothIndex;

        EditorGUILayout.LabelField("一块布可挂多条，每格额度与封顶必须同号", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    // 每条 BUFF 的调参提示：到顶需要的格数决定该给这块布配多大的异形
    private string buffHint(ClothBuffEntry buff)
    {
        if (buff.Mod == BackpackStatMod.None)
            return "未选能力线";

        if (buff.PerCellValue == 0f)
            return "每格给零，等于没配";

        if (buff.CapValue == 0f)
            return "封顶填了 0，这条线被封死";

        if (Mathf.Sign(buff.PerCellValue) != Mathf.Sign(buff.CapValue))
            return "每格与封顶符号相反，会被夹成零";

        string direction = buff.PerCellValue < 0f ? "负向" : "正向";
        int cells = Mathf.CeilToInt(Mathf.Abs(buff.CapValue / buff.PerCellValue));

        return $"{direction} · {cells} 格到顶";
    }

    // 展开体下半：这块布能剪成的异形，右侧现算占格与基准价，末尾画格子小样
    private void drawShapeList(int clothIndex, SerializedProperty clothElement, ClothEntry cloth,
        ClothCatalogSO catalog)
    {
        SerializedProperty shapes = clothElement.FindPropertyRelative("ShapeList");
        float scale = catalog.PriceScaleOf(cloth.RarityIndex);

        for (int i = 0; i < shapes.arraySize; i++)
        {
            SerializedProperty row = shapes.GetArrayElementAtIndex(i);
            GridShapeSO shape = row.objectReferenceValue as GridShapeSO;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(INDENT);
            drawFlatField(row, W_SHAPE);

            string priceText = shape == null
                ? "形状槽位为空"
                : $"{shape.CellCount} 格 · 基准 {Mathf.RoundToInt(cloth.CellBasePriceValue * shape.CellCount * scale)}";
            EditorGUILayout.LabelField(priceText, GUILayout.Width(136f));

            if (shape != null)
                drawShapePreview(shape.Bits, cloth.Color);

            GUILayout.FlexibleSpace();

            if (rowButton("×"))
            {
                m_shapeRemoveCloth = clothIndex;
                m_shapeRemoveAt = i;
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(INDENT);

        if (GUILayout.Button("加一种异形", GUILayout.Width(104f)))
            m_shapeAddCloth = clothIndex;

        EditorGUILayout.LabelField("抽中这块布之后在列出的异形里等概率剪一个", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    // 异形小样：按位图把占到的格涂成布色，空格涂淡灰，一眼看出轮廓
    private void drawShapePreview(long bits, Color tint)
    {
        int minRow = SHAPE_ROW_STRIDE;
        int maxRow = -1;
        int minCol = SHAPE_ROW_STRIDE;
        int maxCol = -1;

        for (int bit = 0; bit < SHAPE_TOTAL_BITS; bit++)
        {
            if ((bits & (1L << bit)) == 0)
                continue;

            int row = bit / SHAPE_ROW_STRIDE;
            int col = bit % SHAPE_ROW_STRIDE;

            minRow = Mathf.Min(minRow, row);
            maxRow = Mathf.Max(maxRow, row);
            minCol = Mathf.Min(minCol, col);
            maxCol = Mathf.Max(maxCol, col);
        }

        if (maxRow < minRow)
            return;

        float width = (maxCol - minCol + 1) * CELL_PX;
        float height = (maxRow - minRow + 1) * CELL_PX;
        Rect box = GUILayoutUtility.GetRect(width, height);

        for (int row = minRow; row <= maxRow; row++)
        {
            for (int col = minCol; col <= maxCol; col++)
            {
                bool filled = (bits & (1L << (row * SHAPE_ROW_STRIDE + col))) != 0;
                Rect cell = new Rect(box.x + (col - minCol) * CELL_PX, box.y + (row - minRow) * CELL_PX,
                    CELL_PX - 1f, CELL_PX - 1f);

                EditorGUI.DrawRect(cell, filled ? tint : new Color(0.5f, 0.5f, 0.5f, 0.18f));
            }
        }
    }

    // 定宽画一个不带标签的字段，横向紧凑排版全靠它
    private void drawFlatField(SerializedProperty property, float width)
    {
        Rect rect = GUILayoutUtility.GetRect(width, LINE_H);

        if (property == null)
        {
            if (!m_missingFieldReported)
            {
                m_missingFieldReported = true;
                Debug.LogError($"[{nameof(ClothCatalogSOEditor)}] 总表字段名对不上，请核对 SerializedProperty 名与 ClothCatalogSO 的序列化名");
            }

            return;
        }

        EditorGUI.PropertyField(rect, property, GUIContent.none);
    }

    // 行尾的窄按钮，返回是否被点
    private bool rowButton(string text)
    {
        return GUILayout.Button(text, GUILayout.Width(W_BTN));
    }

    // 稀有度下拉的选项：优先档名，空了就退标识，再空就用行号顶上
    private string[] rarityNames()
    {
        string[] names = new string[m_rarityList.arraySize];

        for (int i = 0; i < m_rarityList.arraySize; i++)
        {
            SerializedProperty element = m_rarityList.GetArrayElementAtIndex(i);
            string label = element.FindPropertyRelative("TierLabel").stringValue;

            if (string.IsNullOrEmpty(label))
                label = element.FindPropertyRelative("TierId").stringValue;

            names[i] = string.IsNullOrEmpty(label) ? $"第 {i + 1} 档" : label;
        }

        return names;
    }

    // 带加成布池的合计权重，空白布自成一池不参与，占比算法与商店抽取保持一致
    private float buffPoolWeight(ClothCatalogSO catalog)
    {
        float total = 0f;

        for (int i = 0; i < catalog.ClothCount; i++)
        {
            ClothEntry cloth = catalog.ClothAt(i);

            if (cloth == null || cloth.IsBlank || cloth.ShapeCount == 0)
                continue;

            total += catalog.DrawWeightOf(cloth.RarityIndex);
        }

        return total;
    }

    // 数这一档里实际上架的布，空白布不计入，占比才不会虚高
    private int countSellableAt(ClothCatalogSO catalog, int tierIndex)
    {
        int total = 0;

        for (int i = 0; i < catalog.ClothCount; i++)
        {
            ClothEntry cloth = catalog.ClothAt(i);

            if (cloth == null || cloth.IsBlank || cloth.ShapeCount == 0)
                continue;

            if (cloth.RarityIndex == tierIndex)
                total++;
        }

        return total;
    }

    // 表尾提醒：重建菜单会整表覆盖手工改动，增删造成的档位对齐也在这里报一句
    private void drawHint()
    {
        if (!string.IsNullOrEmpty(m_shownNotice))
            EditorGUILayout.HelpBox(m_shownNotice, MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("手改前留意：菜单「Tools/钓鱼游戏/生成布料总表」会按脚本里的表整张重写本资产",
            EditorStyles.wordWrappedMiniLabel);
    }

    // 增删统一在绘制结束后落地，避免一边遍历一边改数组导致下标错位
    // 撤销交给 OnInspectorGUI 末尾的 ApplyModifiedProperties，这里不再 RecordObject，免得一次点击压两条撤销
    private void applyPendingOps()
    {
        if (m_rarityInsertAt >= 0)
        {
            string[] idsBefore = rarityIds();
            m_rarityList.InsertArrayElementAtIndex(m_rarityInsertAt);
            fillRarityRow(m_rarityList.GetArrayElementAtIndex(m_rarityInsertAt), m_rarityInsertAt);
            remapClothTiers(idsBefore, m_rarityRemoveAt < 0);
        }

        if (m_rarityRemoveAt >= 0 && m_rarityRemoveAt < m_rarityList.arraySize)
        {
            string[] idsBefore = rarityIds();
            m_rarityList.DeleteArrayElementAtIndex(m_rarityRemoveAt);
            remapClothTiers(idsBefore, false);
        }

        if (m_addCloth)
        {
            int index = m_clothList.arraySize;
            m_clothList.InsertArrayElementAtIndex(index);
            fillClothRow(m_clothList.GetArrayElementAtIndex(index), index);
        }

        if (m_clothRemoveAt >= 0 && m_clothRemoveAt < m_clothList.arraySize)
            m_clothList.DeleteArrayElementAtIndex(m_clothRemoveAt);

        if (m_buffAddCloth >= 0 && m_buffAddCloth < m_clothList.arraySize)
        {
            SerializedProperty buffs = subList(m_buffAddCloth, "BuffList");
            buffs.InsertArrayElementAtIndex(buffs.arraySize);

            SerializedProperty added = buffs.GetArrayElementAtIndex(buffs.arraySize - 1);
            added.FindPropertyRelative("ModType").enumValueIndex = m_modNoneIndex;
            added.FindPropertyRelative("PerCell").floatValue = 0f;
            added.FindPropertyRelative("Cap").floatValue = 0f;
        }

        if (m_buffRemoveCloth >= 0 && m_buffRemoveCloth < m_clothList.arraySize)
        {
            SerializedProperty buffs = subList(m_buffRemoveCloth, "BuffList");

            if (m_buffRemoveAt >= 0 && m_buffRemoveAt < buffs.arraySize)
                buffs.DeleteArrayElementAtIndex(m_buffRemoveAt);
        }

        if (m_shapeAddCloth >= 0 && m_shapeAddCloth < m_clothList.arraySize)
        {
            SerializedProperty shapes = subList(m_shapeAddCloth, "ShapeList");
            shapes.InsertArrayElementAtIndex(shapes.arraySize);
            shapes.GetArrayElementAtIndex(shapes.arraySize - 1).objectReferenceValue = null;
        }

        if (m_shapeRemoveCloth >= 0 && m_shapeRemoveCloth < m_clothList.arraySize)
        {
            SerializedProperty shapes = subList(m_shapeRemoveCloth, "ShapeList");

            if (m_shapeRemoveAt >= 0 && m_shapeRemoveAt < shapes.arraySize)
                shapes.DeleteArrayElementAtIndex(m_shapeRemoveAt);
        }

        m_rarityInsertAt = -1;
        m_rarityRemoveAt = -1;
        m_addCloth = false;
        m_clothRemoveAt = -1;
        m_buffAddCloth = -1;
        m_buffRemoveCloth = -1;
        m_buffRemoveAt = -1;
        m_shapeAddCloth = -1;
        m_shapeRemoveCloth = -1;
        m_shapeRemoveAt = -1;
    }

    // 取某块布下面的子数组，增删两处都要用
    private SerializedProperty subList(int clothIndex, string fieldName)
    {
        return m_clothList.GetArrayElementAtIndex(clothIndex).FindPropertyRelative(fieldName);
    }

    // 按插入前的档位标识，把每块布的 RarityTierIndex 对回原来的档
    // 布料引用的是数组下标，档位一增删所有下标整体错位，不对齐就会把「稀有」静默画成「空白新档」
    private void remapClothTiers(string[] idsBefore, bool inserted)
    {
        if (m_clothList == null || idsBefore == null || idsBefore.Length == 0)
            return;

        string[] idsAfter = rarityIds();

        if (idsAfter.Length == 0)
        {
            m_pendingNotice = "稀有度档已被清空，布料暂时指不到任何档，先补一档再说";
            return;
        }

        int movedCount = 0;
        int orphanCount = 0;

        for (int i = 0; i < m_clothList.arraySize; i++)
        {
            SerializedProperty rarity = m_clothList.GetArrayElementAtIndex(i).FindPropertyRelative("RarityTierIndex");
            int oldIndex = Mathf.Clamp(rarity.intValue, 0, idsBefore.Length - 1);
            int newIndex = System.Array.IndexOf(idsAfter, idsBefore[oldIndex]);

            // 这一档被删掉了，退到相邻的存活档，并把受影响的块数记下来
            if (newIndex < 0)
            {
                newIndex = Mathf.Clamp(oldIndex, 0, idsAfter.Length - 1);
                orphanCount++;
            }

            if (newIndex == rarity.intValue)
                continue;

            rarity.intValue = newIndex;
            movedCount++;
        }

        if (inserted)
            m_pendingNotice = $"插入档位后已按标识把 {movedCount} 块布对回原来的档";
        else if (orphanCount > 0)
            m_pendingNotice = $"删掉的档位原本挂着 {orphanCount} 块布，已退到相邻档，请逐块确认";
        else
            m_pendingNotice = $"删除档位后已按标识把 {movedCount} 块布对回原来的档";
    }

    // 读当前档位标识表，增删前后各读一次用来对账
    private string[] rarityIds()
    {
        if (m_rarityList == null)
            return new string[0];

        string[] ids = new string[m_rarityList.arraySize];

        for (int i = 0; i < m_rarityList.arraySize; i++)
            ids[i] = m_rarityList.GetArrayElementAtIndex(i).FindPropertyRelative("TierId").stringValue;

        return ids;
    }

    // 新档填默认值：插入是抄邻档的，标识重复会让对账失效，所以给一个没被占用的标识
    private void fillRarityRow(SerializedProperty element, int index)
    {
        element.FindPropertyRelative("TierId").stringValue = uniqueTierId();
        element.FindPropertyRelative("TierLabel").stringValue = $"新档 {index + 1}";
        element.FindPropertyRelative("DrawWeight").floatValue = DEFAULT_DRAW_WEIGHT;
        element.FindPropertyRelative("PriceScale").floatValue = DEFAULT_PRICE_SCALE;
    }

    // 新布填默认值：插入抄走了上一块布的 BUFF 与异形，不清空就等于凭空多出三条 BUFF
    private void fillClothRow(SerializedProperty element, int index)
    {
        element.FindPropertyRelative("ClothId").stringValue = uniqueClothId();
        element.FindPropertyRelative("ClothLabel").stringValue = $"新布 {index + 1}";
        element.FindPropertyRelative("RarityTierIndex").intValue = 0;
        element.FindPropertyRelative("CellBasePrice").intValue = DEFAULT_CELL_PRICE;
        element.FindPropertyRelative("ClothColor").colorValue = new Color32(0xED, 0xE0, 0xC8, 0xFF);
        element.FindPropertyRelative("BuffList").ClearArray();
        element.FindPropertyRelative("ShapeList").ClearArray();
        element.isExpanded = true;
    }

    // 造一个没被占用的档位标识
    private string uniqueTierId()
    {
        for (int n = 1; n < 999; n++)
        {
            string candidate = $"new_tier_{n}";

            if (System.Array.IndexOf(rarityIds(), candidate) < 0)
                return candidate;
        }

        return System.Guid.NewGuid().ToString("N").Substring(0, 8);
    }

    // 造一个没被占用的布料标识
    private string uniqueClothId()
    {
        for (int n = 1; n < 999; n++)
        {
            string candidate = $"cloth_new_{n}";
            bool taken = false;

            for (int i = 0; i < m_clothList.arraySize; i++)
            {
                if (m_clothList.GetArrayElementAtIndex(i).FindPropertyRelative("ClothId").stringValue == candidate)
                {
                    taken = true;
                    break;
                }
            }

            if (!taken)
                return candidate;
        }

        return System.Guid.NewGuid().ToString("N").Substring(0, 8);
    }
}
