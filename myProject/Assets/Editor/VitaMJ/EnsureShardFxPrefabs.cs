#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 在工程中生成卡牌碎屑 FX 预制体（含 ParticleSystem + ShardParticleCollectDriver），供运行时 Resources 加载。
/// 首次导入或脚本编译后 DelayCall 执行一次；已存在则跳过。
/// </summary>
static class EnsureShardFxPrefabs
{
    const string PrefabFolder = "Assets/Resources/VitaMJ/Prefabs";
    const string MahjongPrefabPath = PrefabFolder + "/MahjongBaoshiShardsFx.prefab";
    const string BillboardPrefabPath = PrefabFolder + "/CardShatterBillboardFx.prefab";

    static EnsureShardFxPrefabs()
    {
        EditorApplication.delayCall += TryCreateMissingPrefabs;
    }

    static void TryCreateMissingPrefabs()
    {
        if (!Directory.Exists(PrefabFolder))
            Directory.CreateDirectory(PrefabFolder);

        if (AssetDatabase.LoadAssetAtPath<GameObject>(MahjongPrefabPath) == null)
            SavePrefab(BuildMahjongShell(), MahjongPrefabPath);

        if (AssetDatabase.LoadAssetAtPath<GameObject>(BillboardPrefabPath) == null)
            SavePrefab(BuildBillboardShell(), BillboardPrefabPath);
    }

    static GameObject BuildMahjongShell()
    {
        var go = new GameObject("MahjongBaoshiShardsFx");
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = false;

        go.AddComponent<ShardParticleCollectDriver>();

        int ui = LayerMask.NameToLayer("UI");
        if (ui >= 0)
            go.layer = ui;

        return go;
    }

    static GameObject BuildBillboardShell()
    {
        var go = new GameObject("CardShatterBillboardFx");
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = false;

        go.AddComponent<ShardParticleCollectDriver>();

        int ui = LayerMask.NameToLayer("UI");
        if (ui >= 0)
            go.layer = ui;

        return go;
    }

    static void SavePrefab(GameObject tempInstance, string assetPath)
    {
        try
        {
            PrefabUtility.SaveAsPrefabAsset(tempInstance, assetPath);
        }
        finally
        {
            Object.DestroyImmediate(tempInstance);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("VitaMJ/Effects/强制重建碎屑 FX 预制体")]
    static void MenuRebuildShardFxPrefabs()
    {
        if (!Directory.Exists(PrefabFolder))
            Directory.CreateDirectory(PrefabFolder);

        if (AssetDatabase.LoadAssetAtPath<Object>(MahjongPrefabPath) != null)
            AssetDatabase.DeleteAsset(MahjongPrefabPath);
        if (AssetDatabase.LoadAssetAtPath<Object>(BillboardPrefabPath) != null)
            AssetDatabase.DeleteAsset(BillboardPrefabPath);
        AssetDatabase.Refresh();

        SavePrefab(BuildMahjongShell(), MahjongPrefabPath);
        SavePrefab(BuildBillboardShell(), BillboardPrefabPath);
        EditorUtility.DisplayDialog("VitaMJ", "已重建预制体：\n" + MahjongPrefabPath + "\n" + BillboardPrefabPath, "确定");
    }
}
#endif
