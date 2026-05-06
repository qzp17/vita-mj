using FairyGUI;
using UnityEngine;

/// <summary>
/// 从 <see cref="Resources"/> 实例化碎屑 FX 根物体（UI 层）；预制体缺失时用同名运行时物体兜底。
/// </summary>
public static class ShardFxPrefabSpawn
{
    /// <param name="resourcesPrefabPath">Resources 下路径，不含扩展名（如 <c>VitaMJ/Prefabs/MahjongBaoshiShardsFx</c>）。</param>
    /// <param name="fallbackRootName">未找到预制体时新建的 <see cref="GameObject"/> 名称。</param>
    public static GameObject InstantiateUiFx(string resourcesPrefabPath, Vector3 worldCenter, string fallbackRootName)
    {
        GameObject prefab = Resources.Load<GameObject>(resourcesPrefabPath);
        GameObject root = prefab != null
            ? Object.Instantiate(prefab, worldCenter, Quaternion.identity)
            : new GameObject(string.IsNullOrEmpty(fallbackRootName) ? "ShardFx" : fallbackRootName);

        root.transform.SetPositionAndRotation(worldCenter, Quaternion.identity);

        int uiLayer = LayerMask.NameToLayer(StageCamera.LayerName);
        if (uiLayer >= 0)
            root.layer = uiLayer;

        return root;
    }
}
