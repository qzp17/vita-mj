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
    }
}
