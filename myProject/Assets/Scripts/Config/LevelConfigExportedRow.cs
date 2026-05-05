using Newtonsoft.Json;

namespace VitaMj.Config
{
    /// <summary>
    /// 与 <c>Assets/Config</c> 中 Level 表导出列对应：tag | level | contentJson（首列 tag 作导出行键，须唯一）。
    /// </summary>
    [System.Serializable]
    public sealed class LevelConfigExportedRow
    {
        [JsonProperty("tag")]
        public string tag { get; set; }

        [JsonProperty("level")]
        public int level { get; set; }

        [JsonProperty("contentJson")]
        public string contentJson { get; set; }
    }
}
