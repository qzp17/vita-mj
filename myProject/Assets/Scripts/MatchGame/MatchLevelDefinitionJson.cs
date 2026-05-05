using System;
using System.Collections.Generic;
using UnityEngine;

namespace VitaMj.MatchGame
{
    /// <summary>
    /// 与 <see cref="JsonUtility"/> 兼容的关卡 JSON 形态（数组字段使用数组，不用 List）。
    /// </summary>
    [Serializable]
    public sealed class MatchLevelDefinitionJson
    {
        public string levelId;

        /// <summary>0 = ExplicitPairs，1 = RandomPairsFromRange。</summary>
        public int faceMode;

        public int valueMin = 1;
        public int valueMax = 12;
        public int randomSeed;

        /// <summary>倒计时秒数；与 <see cref="time"/> 二选一即可（同时存在时优先本字段）。</summary>
        public int timeLimitSeconds;

        /// <summary>与 <c>timeLimitSeconds</c> 同义，便于在 content JSON 里写 <c>\"time\": 60</c>。</summary>
        public int time;

        public MatchLevelCardRowJson[] cards;
    }

    [Serializable]
    public sealed class MatchLevelCardRowJson
    {
        public string cardId;
        public int layer;
        public int logicRow;
        public int logicCol;
        public bool useAbsolutePosition;
        public float displayX;
        public float displayY;
        public int faceValue;
        public string[] blockedByCardIds;
    }

    /// <summary>从 JSON 生成运行时 <see cref="MatchLevelDefinition"/> 实例（无需资产文件）。</summary>
    public static class MatchLevelDefinitionFromJson
    {
        public static MatchLevelDefinition Create(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON 为空。", nameof(json));

            MatchLevelDefinitionJson dto = JsonUtility.FromJson<MatchLevelDefinitionJson>(json.Trim());
            if (dto == null)
                throw new InvalidOperationException("JSON 解析结果为 null。");

            var def = ScriptableObject.CreateInstance<MatchLevelDefinition>();
            Apply(dto, def);
            return def;
        }

        public static void Apply(MatchLevelDefinitionJson dto, MatchLevelDefinition target)
        {
            if (dto == null || target == null)
                throw new ArgumentNullException();

            target.levelId = dto.levelId ?? string.Empty;
            target.faceMode = dto.faceMode switch
            {
                (int)MatchLevelFaceMode.ExplicitPairs => MatchLevelFaceMode.ExplicitPairs,
                (int)MatchLevelFaceMode.RandomPairsFromRange => MatchLevelFaceMode.RandomPairsFromRange,
                _ => MatchLevelFaceMode.RandomPairsFromRange,
            };

            target.valueMin = dto.valueMin;
            target.valueMax = dto.valueMax;
            target.randomSeed = dto.randomSeed;
            target.timeLimitSeconds = ResolveTimeLimitSeconds(dto);

            target.cards ??= new List<MatchLevelCardRow>();
            target.cards.Clear();

            if (dto.cards == null)
                return;

            foreach (MatchLevelCardRowJson row in dto.cards)
            {
                if (row == null)
                    continue;

                var card = new MatchLevelCardRow
                {
                    cardId = row.cardId ?? string.Empty,
                    layer = row.layer,
                    logicRow = row.logicRow,
                    logicCol = row.logicCol,
                    useAbsolutePosition = row.useAbsolutePosition,
                    displayX = row.displayX,
                    displayY = row.displayY,
                    faceValue = row.faceValue,
                    blockedByCardIds = new List<string>(),
                };

                if (row.blockedByCardIds != null)
                {
                    foreach (string bid in row.blockedByCardIds)
                    {
                        if (!string.IsNullOrWhiteSpace(bid))
                            card.blockedByCardIds.Add(bid.Trim());
                    }
                }

                target.cards.Add(card);
            }
        }

        static int ResolveTimeLimitSeconds(MatchLevelDefinitionJson dto)
        {
            if (dto == null)
                return 0;
            if (dto.timeLimitSeconds > 0)
                return dto.timeLimitSeconds;
            return dto.time > 0 ? dto.time : 0;
        }
    }
}
