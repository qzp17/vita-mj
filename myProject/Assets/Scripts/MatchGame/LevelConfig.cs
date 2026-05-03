using System;
using System.Collections.Generic;
using UnityEngine;

namespace VitaMj.MatchGame
{
    /// <summary>
    /// 关卡配置表中的一行：关卡 tag、关卡编号、关卡详情 JSON（结构与 <see cref="MatchLevelDefinitionJson"/> 一致）。
    /// </summary>
    [Serializable]
    public sealed class LevelConfigRow
    {
        [Tooltip("关卡分组 / 标签，供逻辑检索")]
        public string tag;

        [Tooltip("关卡序号（同 tag 下建议唯一）")]
        public int level;

        [Tooltip("MatchLevelDefinitionJson 序列化文本")]
        [TextArea(2, 24)]
        public string contentJson;
    }

    /// <summary>
    /// 关卡配置总表：可由编辑器从 Excel（.xlsx）一键导入。
    /// </summary>
    [CreateAssetMenu(menuName = "VitaMJ/Level Config", fileName = "LevelConfig")]
    public sealed class LevelConfig : ScriptableObject
    {
        public List<LevelConfigRow> rows = new List<LevelConfigRow>();

        /// <summary>按 tag + level 查找原始 JSON 行。</summary>
        public bool TryGetRow(string tag, int level, out LevelConfigRow row)
        {
            row = null;
            if (string.IsNullOrEmpty(tag))
                return false;

            for (int i = 0; i < rows.Count; i++)
            {
                LevelConfigRow r = rows[i];
                if (r != null && r.level == level && string.Equals(r.tag, tag, StringComparison.Ordinal))
                {
                    row = r;
                    return true;
                }
            }

            return false;
        }

        /// <summary>生成运行时关卡定义（校验逻辑与 <see cref="ConfiguredPairMatchGame"/> 一致）。</summary>
        public bool TryCreateMatchLevel(string tag, int level, out MatchLevelDefinition definition, out string error)
        {
            definition = null;
            error = null;

            if (!TryGetRow(tag, level, out LevelConfigRow row) || string.IsNullOrWhiteSpace(row.contentJson))
            {
                error = $"未找到 tag={tag}, level={level} 的配置或 contentJson 为空。";
                return false;
            }

            MatchLevelDefinition def = null;
            try
            {
                def = MatchLevelDefinitionFromJson.Create(row.contentJson);
                _ = new ConfiguredPairMatchGame(def);
                definition = def;
                return true;
            }
            catch (Exception ex)
            {
                if (def != null)
                    UnityEngine.Object.Destroy(def);

                definition = null;
                error = ex.Message;
                return false;
            }
        }
    }
}
