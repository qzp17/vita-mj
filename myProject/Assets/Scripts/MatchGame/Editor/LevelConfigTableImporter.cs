using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using VitaMj.MatchGame;

/// <summary>
/// LevelConfig 一键导入：使用 .xlsx（首个工作表）。列为 A=tag(string)，B=level(number)，C=JSON。
/// </summary>
public static class LevelConfigTableImporter
{
    const string MenuPath = "Assets/VitaMJ/关卡配置表导入";

    [MenuItem(MenuPath, true)]
    static bool ValidateImportMenu()
    {
        return Selection.activeObject is LevelConfig;
    }

    [MenuItem(MenuPath, false)]
    static void ImportMenu()
    {
        if (Selection.activeObject is LevelConfig cfg)
            ImportInto(cfg);
    }

    public static void ImportInto(LevelConfig config)
    {
        if (config == null)
            return;

        string path = EditorUtility.OpenFilePanel("选择关卡表（.xlsx）", Application.dataPath, "xlsx");
        if (string.IsNullOrEmpty(path))
            return;

        if (!Path.GetExtension(path).Equals(".xlsx", System.StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("关卡配置导入", "请选择扩展名为 .xlsx 的 Excel 文件。", "确定");
            return;
        }

        try
        {
            List<LevelConfigRow> imported = XlsxLevelConfigParser.Parse(path);
            if (imported.Count == 0)
            {
                EditorUtility.DisplayDialog("关卡配置导入", "未解析到任何有效数据行。", "确定");
                return;
            }

            Undo.RecordObject(config, "Import Level Config Table");
            config.rows.Clear();
            config.rows.AddRange(imported);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("关卡配置导入", $"已从 xlsx 导入 {imported.Count} 行。", "确定");
        }
        catch (System.Exception ex)
        {
            EditorUtility.DisplayDialog("关卡配置导入失败", ex.Message, "确定");
            Debug.LogException(ex);
        }
    }
}

[CustomEditor(typeof(LevelConfig))]
public sealed class LevelConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("一键导入：Excel .xlsx，首个工作表。A 列 tag，B 列 level，C 列 JSON。", MessageType.Info);
        if (GUILayout.Button("从 .xlsx 一键导入…"))
            LevelConfigTableImporter.ImportInto((LevelConfig)target);
    }
}
