using System;
using System.Collections.Generic;
using UnityEngine;

namespace VitaMj.MatchGame
{
    /// <summary>关卡牌面分配方式。</summary>
    public enum MatchLevelFaceMode
    {
        /// <summary>每行 <see cref="MatchLevelCardRow.faceValue"/> 必填（≥1），且每个数字出现偶数次。</summary>
        ExplicitPairs,
        /// <summary>在闭区间 [<see cref="MatchLevelDefinition.valueMin"/>, <see cref="MatchLevelDefinition.valueMax"/>] 内随机配对。</summary>
        RandomPairsFromRange,
    }

    /// <summary>关卡中单张 card 的配置行（策划表一行）。</summary>
    [Serializable]
    public sealed class MatchLevelCardRow
    {
        [Tooltip("关卡内唯一，用于 blockedByCardIds 引用")]
        public string cardId;

        [Tooltip("层级，0 为最底层；可不按金字塔递增")]
        public int layer;

        [Tooltip("逻辑格点：用于自动推算遮挡（上层四角压住下层）；不规则布局也可当作抽象索引")]
        public int logicRow;

        public int logicCol;

        [Tooltip("勾选后在 UI 上使用 displayX/displayY，否则由 Binder 按网格推算坐标")]
        public bool useAbsolutePosition;

        public float displayX;
        public float displayY;

        [Tooltip("ExplicitPairs：牌面数字。RandomPairsFromRange：填 0")]
        public int faceValue;

        [Tooltip("压住本牌的上层 cardId；留空则用 logicRow/Col 自动推算四角遮挡")]
        public List<string> blockedByCardIds = new List<string>();
    }

    /// <summary>
    /// 一关的卡牌布局与数值配置（ScriptableObject，可在 Inspector 填表，亦可由自定义 Excel 导入生成）。
    /// </summary>
    [CreateAssetMenu(menuName = "VitaMJ/Match Level Definition", fileName = "MatchLevel_")]
    public sealed class MatchLevelDefinition : ScriptableObject
    {
        [Tooltip("关卡标识，供存档/跳转")]
        public string levelId;

        public MatchLevelFaceMode faceMode = MatchLevelFaceMode.RandomPairsFromRange;

        [Tooltip("RandomPairsFromRange：随机牌面下界（含）")]
        public int valueMin = 1;

        [Tooltip("RandomPairsFromRange：随机牌面上界（含）")]
        public int valueMax = 12;

        [Tooltip("固定随机种子；0 表示每次开局用 TickCount")]
        public int randomSeed;

        [Tooltip("本关倒计时秒数；0 表示不限时（与关卡 content JSON 中 timeLimitSeconds / time 一致）")]
        public int timeLimitSeconds;

        [Tooltip("收纳栏上限；0 表示传统「点两次相同牌」配对。≥1 时点击入栏，栏尾相邻相同则抵消，栏满再点失败。")]
        public int queueMaxSlots;

        public List<MatchLevelCardRow> cards = new List<MatchLevelCardRow>();
    }
}
