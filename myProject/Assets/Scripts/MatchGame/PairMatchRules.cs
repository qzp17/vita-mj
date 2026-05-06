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

        /// <summary>
        /// 收纳栏：可点时入栏并离开棋盘；若栏尾与前一张牌面相同则两者从栏中移除（可连环）；栏已满时再点判定失败。
        /// </summary>
        public static LayeredMatchClickResult TryMatchBarClick(
            IReadOnlyList<LayeredGridCell> cells,
            List<int> barCellIds,
            int barCapacity,
            int cellId)
        {
            if ((uint)cellId >= (uint)cells.Count)
                return LayeredMatchClickResult.Invalid;

            if (!CanClick(cells, cellId))
                return LayeredMatchClickResult.Invalid;

            if (barCellIds.Count >= barCapacity)
                return LayeredMatchClickResult.MatchBarFullGameOver;

            barCellIds.Add(cellId);
            cells[cellId].Eliminated = true;

            bool merged = false;
            while (barCellIds.Count >= 2)
            {
                int a = barCellIds[barCellIds.Count - 2];
                int b = barCellIds[barCellIds.Count - 1];
                if (cells[a].Value != cells[b].Value)
                    break;

                barCellIds.RemoveAt(barCellIds.Count - 1);
                barCellIds.RemoveAt(barCellIds.Count - 1);
                merged = true;
            }

        public static LayeredMatchClickResult TryMatchBarClick(
            IReadOnlyList<LayeredGridCell> cells,
            List<int> barCellIds,
            int barCapacity,
            int cellId)
        {
            if ((uint)cellId >= (uint)cells.Count)
                return LayeredMatchClickResult.Invalid;

            if (!CanClick(cells, cellId))
                return LayeredMatchClickResult.Invalid;

            if (barCellIds.Count >= barCapacity)
                return LayeredMatchClickResult.MatchBarFullGameOver;

            barCellIds.Add(cellId);
            cells[cellId].Eliminated = true;

            bool merged = false;
            while (barCellIds.Count >= 2)
            {
                int a = barCellIds[barCellIds.Count - 2];
                int b = barCellIds[barCellIds.Count - 1];
                if (cells[a].Value != cells[b].Value)
                    break;

                barCellIds.RemoveAt(barCellIds.Count - 1);
                barCellIds.RemoveAt(barCellIds.Count - 1);
                merged = true;
            }

            return merged
                ? LayeredMatchClickResult.MatchBarMerged
                : LayeredMatchClickResult.MatchBarEnqueued;
        }

        /// <summary>
        /// 收纳栏反悔：取下队尾的一张牌放回棋盘。
        /// </summary>
        public static bool TryMatchBarRevert(IReadOnlyList<LayeredGridCell> cells, List<int> barCellIds)
        {
            if (barCellIds == null || barCellIds.Count == 0 || cells == null)
                return false;
            int id = barCellIds[barCellIds.Count - 1];
            barCellIds.RemoveAt(barCellIds.Count - 1);
            if ((uint)id >= (uint)cells.Count)
                return false;
            cells[id].Eliminated = false;
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

            if (game.MatchBarCapacity > 0)
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
