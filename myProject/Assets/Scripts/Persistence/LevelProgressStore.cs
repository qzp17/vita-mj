namespace VitaMj.Persistence
{
    /// <summary>
    /// 与选关列表 <see cref="GameUIManager"/> 中 Level 表行序（0 起）对齐的进度。
    /// 规则：仅当完成第 i 关后解锁第 i+1 关；第 0 关默认可玩；已通关可重复挑战。
    /// </summary>
    public static class LevelProgressStore
    {
        const string KeyMaxClearedIndex = "progress.level.max_cleared_index";

        /// <summary>
        /// 已通关的最高列表下标；-1 表示尚未通关任一关。
        /// </summary>
        public static int GetMaxClearedLevelIndex()
        {
            return LocalKeyValueStore.GetInt(KeyMaxClearedIndex, -1);
        }

        /// <summary>
        /// 记录「列表第 <paramref name="levelListIndex" /> 关」已通关；仅向前推进进度。
        /// </summary>
        public static void RegisterLevelCleared(int levelListIndex)
        {
            if (levelListIndex < 0)
                return;

            int cur = GetMaxClearedLevelIndex();
            if (levelListIndex <= cur)
                return;

            LocalKeyValueStore.SetInt(KeyMaxClearedIndex, levelListIndex);
        }

        /// <summary>是否已达到可游玩条件（锁定关不可点击）。</summary>
        public static bool IsLevelUnlocked(int levelListIndex)
        {
            return levelListIndex <= GetMaxClearedLevelIndex() + 1;
        }

        /// <summary>是否已经通关（用于显示已完成态）。</summary>
        public static bool IsLevelCleared(int levelListIndex)
        {
            return levelListIndex <= GetMaxClearedLevelIndex();
        }

        /// <summary>
        /// FairyGUI 列表格子控制器：<c>0=open</c>，<c>1=finish</c>，<c>2=lock</c>。
        /// </summary>
        public static LevelCellVisualState GetCellVisualState(int levelListIndex)
        {
            if (!IsLevelUnlocked(levelListIndex))
                return LevelCellVisualState.Lock;
            if (IsLevelCleared(levelListIndex))
                return LevelCellVisualState.Finish;
            return LevelCellVisualState.Open;
        }
    }

    public enum LevelCellVisualState
    {
        Open = 0,
        Finish = 1,
        Lock = 2,
    }
}
