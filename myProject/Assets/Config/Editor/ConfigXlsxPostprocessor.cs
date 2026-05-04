using System.Linq;
using UnityEditor;

/// <summary>
/// Assets/Config 下 .xlsx 变更时自动重新导出。
/// </summary>
class ConfigXlsxPostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedToAssets,
        string[] movedFromAssets)
    {
        const string folder = "Assets/Config";

        bool touch = importedAssets.Any(p =>
                p.StartsWith(folder, System.StringComparison.OrdinalIgnoreCase) &&
                p.EndsWith(".xlsx", System.StringComparison.OrdinalIgnoreCase))
            || deletedAssets.Any(p =>
                p.StartsWith(folder, System.StringComparison.OrdinalIgnoreCase) &&
                p.EndsWith(".xlsx", System.StringComparison.OrdinalIgnoreCase))
            || movedToAssets.Any(p =>
                p.StartsWith(folder, System.StringComparison.OrdinalIgnoreCase) &&
                p.EndsWith(".xlsx", System.StringComparison.OrdinalIgnoreCase));

        if (touch)
            ExportAllConfigs.ExportAll(true);
    }
}
