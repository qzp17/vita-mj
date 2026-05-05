using System.Collections.Generic;

namespace VitaMj.MatchGame
{
    /// <summary>配对消除点击规则（与棋盘构造方式无关）。</summary>
    internal static class PairMatchRules
    {
        public static bool CanClick(IReadOnlyList<LayeredGridCell> cells, int cellId)
        {
            if ((uint)cellId >= (uint)cells.Count)
                return false;
            var cell = cells[cellId];
            if (cell.Eliminated)
                return false;

            for (int i = 0; i < cell.BlockerIds.Length; i++)
            {
                var b = cells[cell.BlockerIds[i]];
                if (!b.Eliminated)
                    return false;
            }

            return true;
        }

        public static LayeredMatchClickResult TryClick(IReadOnlyList<LayeredGridCell> cells, ref int? selectedId, int cellId)
        {
            if ((uint)cellId >= (uint)cells.Count)
                return LayeredMatchClickResult.Invalid;

            if (!CanClick(cells, cellId))
                return LayeredMatchClickResult.Invalid;

            if (!selectedId.HasValue)
            {
                selectedId = cellId;
                return LayeredMatchClickResult.SelectedFirst;
            }

            if (selectedId.Value == cellId)
            {
                selectedId = null;
                return LayeredMatchClickResult.Deselected;
            }

            var first = cells[selectedId.Value];
            selectedId = null;
            var cell = cells[cellId];

            if (first.Value == cell.Value)
            {
                first.Eliminated = true;
                cell.Eliminated = true;
                return LayeredMatchClickResult.MatchedPair;
            }

            return LayeredMatchClickResult.MismatchedPair;
        }

        /// <summary>
        /// 寻找一对当面可点且牌面数值相同、仍可消除的格子（用于提示）。
        /// </summary>
        public static bool TryFindClickableMatchingPair(IPairMatchGame game, out int cellIdA, out int cellIdB)
        {
            cellIdA = cellIdB = -1;
            if (game == null)
                return false;

            IReadOnlyList<LayeredGridCell> cells = game.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                LayeredGridCell a = cells[i];
                if (a.Eliminated || !game.CanClick(a.Id))
                    continue;

                for (int j = i + 1; j < cells.Count; j++)
                {
                    LayeredGridCell b = cells[j];
                    if (b.Eliminated || !game.CanClick(b.Id))
                        continue;
                    if (a.Value != b.Value)
                        continue;

                    cellIdA = a.Id;
                    cellIdB = b.Id;
                    return true;
                }
            }

            return false;
        }
    }
}
