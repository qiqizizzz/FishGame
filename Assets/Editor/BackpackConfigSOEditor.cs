/*
* ┌──────────────────────────────────┐
* │  描    述: 背包配置 Inspector，逐级解锁掩码用网格点选并校验包含关系
* │  类    名: BackpackConfigSOEditor.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 解锁阶段是一层层长出去的，后一级必须盖住前一级全部已解锁格，否则放好的物品会悬空
* 包含关系不合规就整次退回，不写脏值，配置里根本不存在「关掉一格」这种状态
* 掩码位图不暴露 long 输入框，点格子写回，视口尺寸就是容器尺寸，越界格根本点不到
* 价钱表比解锁级数少一项，开局那级不要钱，所以第 2 级起才跟着画价钱字段
*/

using UnityEditor;
using UnityEngine;
using Module.Backpack.Data;
using Module.Backpack.Model;

[CustomEditor(typeof(BackpackConfigSO))]
public sealed class BackpackConfigSOEditor : Editor
{
    // ==================== 常量与静态字段 ====================
    private const int DEFAULT_EXPANSION_COST = 100;

    // ==================== Public Function ====================
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        BackpackConfigSO config = (BackpackConfigSO)target;
        SerializedProperty columnsProperty = serializedObject.FindProperty("GridColumns");
        SerializedProperty rowsProperty = serializedObject.FindProperty("GridRows");
        SerializedProperty masksProperty = serializedObject.FindProperty("UnlockMaskList");
        SerializedProperty costsProperty = serializedObject.FindProperty("ExpansionCostList");

        EditorGUILayout.LabelField("容器网格", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(columnsProperty);
        EditorGUILayout.PropertyField(rowsProperty);

        int columns = Mathf.Clamp(columnsProperty.intValue, 2, ShapeMask.MAX_GRID_SIDE);
        int rows = Mathf.Clamp(rowsProperty.intValue, 2, ShapeMask.MAX_GRID_SIDE);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("逐级解锁", EditorStyles.boldLabel);
        drawStages(config, masksProperty, costsProperty, columns, rows);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("商品定价", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("TierPriceList"), new GUIContent("一/二/三级售价"), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("暂存架", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("StashCapacityLimit"));

        serializedObject.ApplyModifiedProperties();
    }

    // ==================== Private Function ====================
    // 一阶段一块画布，点亮的格就是这一阶段已开放的格
    private void drawStages(BackpackConfigSO config, SerializedProperty masks, SerializedProperty costs,
        int columns, int rows)
    {
        if (masks.arraySize == 0)
        {
            EditorGUILayout.HelpBox("还没有解锁阶段，容器无格可用。先建开局那一级。", MessageType.Warning);

            if (GUILayout.Button("新建开局级", GUILayout.Width(110f)))
            {
                masks.InsertArrayElementAtIndex(0);
                masks.GetArrayElementAtIndex(0).longValue = 0L;
            }

            return;
        }

        for (int stage = 0; stage < masks.arraySize; stage++)
        {
            SerializedProperty element = masks.GetArrayElementAtIndex(stage);
            long previous = stage == 0 ? 0L : masks.GetArrayElementAtIndex(stage - 1).longValue;

            drawStageToolbar(masks, stage, previous, columns, rows);

            long committed = element.longValue;
            long painted = MaskPainterControl.Paint(committed, columns, rows, out _);
            painted = keepContainment(painted, previous, committed);

            if (painted != committed)
            {
                element.longValue = painted;
                EditorUtility.SetDirty(config);
            }

            drawStageCost(costs, stage);
            EditorGUILayout.Space();
        }

        drawStageListToolbar(masks, costs, previousMask(masks));
    }

    // 本级工具条：继承上一级、清空、填满，都在读掩码之前执行，改完立刻反映到画布上
    private void drawStageToolbar(SerializedProperty masks, int stage, long previous, int columns, int rows)
    {
        SerializedProperty element = masks.GetArrayElementAtIndex(stage);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"第 {stage + 1} 级 · {MaskPainterControl.CountCells(element.longValue)} 格", GUILayout.Width(128f));

        if (stage > 0 && GUILayout.Button("继承上一级", GUILayout.Width(96f)))
            element.longValue = previous;

        if (GUILayout.Button("清空本级", GUILayout.Width(80f)))
            element.longValue = 0L;

        if (GUILayout.Button("填满本级", GUILayout.Width(80f)))
            element.longValue = MaskPainterControl.Fill(columns, rows);

        EditorGUILayout.EndHorizontal();
    }

    // 第 stage 级（从 0 数）对应的价钱是 costs[stage-1]，开局那级不收费
    private void drawStageCost(SerializedProperty costs, int stage)
    {
        int costIndex = stage - 1;

        if (costIndex < 0)
            return;

        if (costIndex >= costs.arraySize)
        {
            EditorGUILayout.HelpBox($"缺少「买到第 {stage + 1} 级」的扩容价，点下方加一级会自动补齐。", MessageType.Error);
            return;
        }

        EditorGUILayout.PropertyField(costs.GetArrayElementAtIndex(costIndex), new GUIContent($"买到第 {stage + 1} 级的扩容价"));
    }

    private void drawStageListToolbar(SerializedProperty masks, SerializedProperty costs, long lastMask)
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("加一级", GUILayout.Width(76f)))
        {
            masks.InsertArrayElementAtIndex(masks.arraySize);
            masks.GetArrayElementAtIndex(masks.arraySize - 1).longValue = lastMask;
            costs.InsertArrayElementAtIndex(costs.arraySize);
            costs.GetArrayElementAtIndex(costs.arraySize - 1).intValue = DEFAULT_EXPANSION_COST;
        }

        if (GUILayout.Button("删末级", GUILayout.Width(76f)))
        {
            if (masks.arraySize > 0)
                masks.DeleteArrayElementAtIndex(masks.arraySize - 1);

            if (costs.arraySize > masks.arraySize - 1)
                costs.DeleteArrayElementAtIndex(costs.arraySize - 1);
        }

        EditorGUILayout.EndHorizontal();
    }

    // 关掉上一级已解锁的格会让放好的物品悬空，这种改动直接退回
    private long keepContainment(long painted, long previous, long committed)
    {
        if ((previous & ~painted) == 0)
            return painted;

        EditorGUILayout.HelpBox("不能关掉上一级已解锁的格，后一级必须盖住前一级，本次改动已退回。", MessageType.Error);
        return committed;
    }

    private long previousMask(SerializedProperty masks)
    {
        if (masks.arraySize == 0)
            return 0L;

        return masks.GetArrayElementAtIndex(masks.arraySize - 1).longValue;
    }
}
