using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace VitaMj.Config
{
    /// <summary>
    /// 从 Resources/ExportConfig 读取导出二进制（JSON 载荷），按表名 + tag 查询行。
    /// 静态入口：<see cref="TryGetRow{T}"/> / <see cref="GetRow{T}"/>；实例入口：<see cref="Instance"/>。
    /// </summary>
    public sealed class ConfigReader
    {
        static ConfigReader _instance;
        static readonly Dictionary<string, Type> TableTypes = new Dictionary<string, Type>(StringComparer.Ordinal);

        readonly Dictionary<string, Dictionary<string, JObject>> _rowCache =
            new Dictionary<string, Dictionary<string, JObject>>(StringComparer.Ordinal);

        /// <summary>与各表 JSON 中键顺序一致（与 Excel 导出行序一致）。</summary>
        readonly Dictionary<string, List<string>> _tagOrderCache =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);

        ConfigReader()
        {
        }

        /// <summary>全局单例。</summary>
        public static ConfigReader Instance => _instance ??= new ConfigReader();

        /// <summary>
        /// 由导出器生成的引导代码调用，注册「表名字符串 → 行类型」。
        /// </summary>
        public static void RegisterTable<T>(string tableKey) where T : class
        {
            if (string.IsNullOrEmpty(tableKey))
                return;
            TableTypes[tableKey] = typeof(T);
        }

        /// <summary>
        /// 清空注册信息与运行时缓存（域重载时由引导代码调用）。
        /// </summary>
        public static void ClearRegistry()
        {
            TableTypes.Clear();
            if (_instance != null)
            {
                _instance._rowCache.Clear();
                _instance._tagOrderCache.Clear();
            }
        }

        public static bool TryGetRow<T>(string tableKey, string tag, out T row) where T : class
        {
            return Instance.TryGet(tableKey, tag, out row);
        }

        public static T GetRow<T>(string tableKey, string tag) where T : class
        {
            return Instance.TryGet(tableKey, tag, out T row) ? row : null;
        }

        /// <summary>
        /// 获取某表全部 tag（与导出时 Excel 数据行顺序一致）；会先加载整表。
        /// </summary>
        public static bool TryGetOrderedTags(string tableKey, out IReadOnlyList<string> tags)
        {
            tags = null;
            ConfigReader reader = Instance;
            if (!reader.TryGetRows(tableKey, out _))
                return false;
            if (!reader._tagOrderCache.TryGetValue(tableKey, out List<string> list) || list.Count == 0)
                return false;
            tags = list;
            return true;
        }

        /// <summary>返回某表已加载后的行数（独立 tag 个数）；加载失败时为 0。</summary>
        public static int GetRowCount(string tableKey)
        {
            return Instance.TryGetRows(tableKey, out Dictionary<string, JObject> rows) && rows != null ? rows.Count : 0;
        }

        public bool TryGet<T>(string tableKey, string tag, out T row) where T : class
        {
            row = null;
            if (string.IsNullOrEmpty(tableKey) || string.IsNullOrEmpty(tag))
                return false;

            if (!TableTypes.TryGetValue(tableKey, out Type registered))
            {
                Debug.LogError($"[ConfigReader] 未注册的配置表：{tableKey}");
                return false;
            }

            if (registered != typeof(T))
            {
                Debug.LogError($"[ConfigReader] 类型不匹配：表「{tableKey}」注册为 {registered.Name}，请求 {typeof(T).Name}");
                return false;
            }

            if (!TryGetRows(tableKey, out Dictionary<string, JObject> rows))
                return false;

            if (!rows.TryGetValue(tag, out JObject jo))
                return false;

            row = jo.ToObject<T>();
            return row != null;
        }

        bool TryGetRows(string tableKey, out Dictionary<string, JObject> rows)
        {
            if (_rowCache.TryGetValue(tableKey, out rows))
                return true;

            TextAsset ta = Resources.Load<TextAsset>($"ExportConfig/{tableKey}");
            if (ta == null)
            {
                Debug.LogError($"[ConfigReader] 未找到 Resources 文件：ExportConfig/{tableKey}");
                rows = null;
                return false;
            }

            if (!TryDecodePayload(ta.bytes, out string json))
            {
                Debug.LogError($"[ConfigReader] 二进制格式非法：ExportConfig/{tableKey}");
                rows = null;
                return false;
            }

            try
            {
                JObject root = JObject.Parse(json);
                var dict = new Dictionary<string, JObject>(StringComparer.Ordinal);
                var order = new List<string>();
                foreach (JProperty prop in root.Properties())
                {
                    order.Add(prop.Name);
                    dict[prop.Name] = prop.Value as JObject ?? new JObject();
                }

                rows = dict;
                _tagOrderCache[tableKey] = order;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ConfigReader] JSON 解析失败（{tableKey}）：{ex.Message}");
                rows = null;
                return false;
            }

            _rowCache[tableKey] = rows;
            return true;
        }

        /// <summary>
        /// 是否包含指定 tag（会先加载整张表）。
        /// </summary>
        public bool ContainsTag(string tableKey, string tag)
        {
            return TryGetRows(tableKey, out Dictionary<string, JObject> rows) &&
                   rows != null &&
                   rows.ContainsKey(tag);
        }

        /// <summary>
        /// 预加载某张表（可选）。
        /// </summary>
        public void Preload(string tableKey)
        {
            TryGetRows(tableKey, out _);
        }

        static bool TryDecodePayload(byte[] raw, out string json)
        {
            json = null;
            if (raw == null || raw.Length < 8)
                return false;

            if (raw[0] != (byte)'V' || raw[1] != (byte)'M' || raw[2] != (byte)'J' || raw[3] != (byte)'1')
                return false;

            int len = BitConverter.ToInt32(raw, 4);
            if (len < 0 || 8 + len > raw.Length)
                return false;

            json = System.Text.Encoding.UTF8.GetString(raw, 8, len);
            return true;
        }
    }
}
