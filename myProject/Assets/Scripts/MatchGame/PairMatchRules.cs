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
        /// 收纳栏：仅入栏并离开棋盘（不在此处抵消）；栏已满时再点判定失败。
        /// </summary>
        public static LayeredMatchClickResult TryMatchBarEnqueueOnly(
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

            return LayeredMatchClickResult.MatchBarEnqueued;
        }

        /// <summary>收纳栏末尾两张面值是否相同（飞入落定后才会调用抵消）。</summary>
        public static bool MatchBarTailIsAdjacentSameFace(
            IReadOnlyList<LayeredGridCell> cells,
            IReadOnlyList<int> barCellIds)
        {
            if (barCellIds == null || barCellIds.Count < 2 || cells == null)
                return false;

            int a = barCellIds[barCellIds.Count - 2];
            int b = barCellIds[barCellIds.Count - 1];
            return cells[a].Value == cells[b].Value;
        }

        /// <summary>
        /// 从收纳栏末尾反复抵消相邻相同对，直至无法继续。
        /// </summary>
        /// <returns>是否发生过至少一次抵消。</returns>
        public static bool TryCollapseMatchBarMerges(
            IReadOnlyList<LayeredGridCell> cells,
            List<int> barCellIds,
            List<int> mergedAwayCollector)
        {
            mergedAwayCollector?.Clear();
            bool merged = false;

            while (barCellIds.Count >= 2)
            {
                int a = barCellIds[barCellIds.Count - 2];
                int b = barCellIds[barCellIds.Count - 1];
                if (cells[a].Value != cells[b].Value)
                    break;

                barCellIds.RemoveAt(barCellIds.Count - 1);
                barCellIds.RemoveAt(barCellIds.Count - 1);
                mergedAwayCollector?.Add(a);
                mergedAwayCollector?.Add(b);
                merged = true;
            }

            return merged;
        }

        /// <summary>
        /// 收纳栏：入栏后对栏尾立即做抵消判定（UI 可先 <see cref="TryMatchBarEnqueueOnly"/> + 动画，再在落定后调用 <see cref="TryCollapseMatchBarMerges"/>）。
        /// </summary>
        /// <param name="mergedAwayCollector">若非 null，会先 Clear，再在每次抵消时追加被移出收纳栏的一对 id（连环合并则可能多于 2 个）。</param>
        public static LayeredMatchClickResult TryMatchBarClick(
            IReadOnlyList<LayeredGridCell> cells,
            List<int> barCellIds,
            int barCapacity,
            int cellId,
            List<int> mergedAwayCollector = null)
        {
            LayeredMatchClickResult enqueue = TryMatchBarEnqueueOnly(cells, barCellIds, barCapacity, cellId);
            if (enqueue != LayeredMatchClickResult.MatchBarEnqueued)
                return enqueue;

            bool merged = TryCollapseMatchBarMerges(cells, barCellIds, mergedAwayCollector);
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
