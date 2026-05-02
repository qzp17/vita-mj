using System;
using System.Collections.Generic;

namespace VitaMj.MatchGame
{
    public enum LayeredMatchClickResult
    {
        /// <summary>不可点（已消除、仍被上层挡住或与当前选中相同且取消）。</summary>
        Invalid,
        /// <summary>选中第一张，等待第二张。</summary>
        SelectedFirst,
        /// <summary>两次点击同一未消除格，取消选中。</summary>
        Deselected,
        /// <summary>两张数字相同，已消除。</summary>
        MatchedPair,
        /// <summary>两张数字不同，选中清空。</summary>
        MismatchedPair,
    }

    /// <summary>
    /// 分层矩形盘：底层 rows×cols，上一层 (rows-1)×(cols-1)，直至堆满（最多 min(rows,cols) 层，顶层可能为 1×k 或 k×1）。
    /// 上层每个格子对应下层一个 2×2 区域。下层格可点当且仅当压住它的上层格均已消除；先后点两格，数字相同则消除。
    /// </summary>
    public sealed class LayeredPairMatchGame
    {
        readonly List<LayeredGridCell> _cells = new List<LayeredGridCell>();
        readonly Dictionary<(int layer, int row, int col), LayeredGridCell> _cellAt = new Dictionary<(int layer, int row, int col), LayeredGridCell>();
        readonly Random _rng;

        int? _selectedId;

        public IReadOnlyList<LayeredGridCell> Cells => _cells;
        public int? SelectedCellId => _selectedId;
        /// <summary>最底层行数。</summary>
        public int RowCount { get; private set; }
        /// <summary>最底层列数。</summary>
        public int ColCount { get; private set; }
        /// <summary>实际堆叠层数（不超过 <see cref="MaxStackableLayers"/>）。</summary>
        public int StackedLayerCount { get; private set; }
        /// <summary>在行列同步减 1 的规则下，最多可堆几层（= min(RowCount, ColCount)）。</summary>
        public int MaxStackableLayers =>
            RowCount >= 1 && ColCount >= 1 ? Math.Min(RowCount, ColCount) : 0;

        public bool IsComplete
        {
            get
            {
                for (int i = 0; i < _cells.Count; i++)
                {
                    if (!_cells[i].Eliminated) return false;
                }
                return true;
            }
        }

        public LayeredPairMatchGame(int? seed = null)
        {
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>底层 rows×cols，每层行、列各减 1；总层数默认 min(rows,cols)，可用 layerCount 截断。</summary>
        public static int GetTotalCellCount(int rows, int cols, int? layerCount = null)
        {
            if (rows < 1 || cols < 1) return 0;
            int maxLayers = Math.Min(rows, cols);
            int layers = layerCount ?? maxLayers;
            if (layers > maxLayers) layers = maxLayers;
            if (layers < 1) return 0;
            int sum = 0;
            for (int k = 0; k < layers; k++)
                sum += (rows - k) * (cols - k);
            return sum;
        }

        /// <summary>正方形底层 n×n 时的总格数。</summary>
        public static int GetTotalCellCountSquare(int baseSize, int? layerCount = null) =>
            GetTotalCellCount(baseSize, baseSize, layerCount);

        /// <summary>最多可发几对（总格数 / 2）。</summary>
        public static int GetMaxPairCount(int rows, int cols, int? layerCount = null) =>
            GetTotalCellCount(rows, cols, layerCount) / 2;

        /// <summary>正方形 n×n 时最多对数。</summary>
        public static int GetMaxPairCountSquare(int baseSize, int? layerCount = null) =>
            GetMaxPairCount(baseSize, baseSize, layerCount);

        /// <param name="stackedLayers">堆叠层数，默认 min(rows, cols)。可减小以缩短高度。</param>
        /// <summary>重建矩形分层网格（不洗牌发牌）。</summary>
        public void BuildGrid(int rows, int cols, int? stackedLayers = null)
        {
            if (rows < 1)
                throw new ArgumentOutOfRangeException(nameof(rows));
            if (cols < 1)
                throw new ArgumentOutOfRangeException(nameof(cols));

            int maxLayers = Math.Min(rows, cols);
            int layers = stackedLayers ?? maxLayers;
            if (layers < 1 || layers > maxLayers)
                throw new ArgumentOutOfRangeException(nameof(stackedLayers), $"层数须在 1 与 min(rows,cols)={maxLayers} 之间。");

            RowCount = rows;
            ColCount = cols;
            StackedLayerCount = layers;
            _cells.Clear();
            _cellAt.Clear();
            _selectedId = null;

            int id = 0;
            for (int layer = 0; layer < layers; layer++)
            {
                int h = rows - layer;
                int w = cols - layer;
                for (int r = 0; r < h; r++)
                {
                    for (int c = 0; c < w; c++)
                    {
                        var cell = new LayeredGridCell(id++, layer, r, c);
                        _cells.Add(cell);
                        _cellAt[(layer, r, c)] = cell;
                    }
                }
            }

            WireBlockers();
        }

        /// <summary>正方形底层 n×n，等价于 <see cref="BuildGrid(int,int,int?)"/>。</summary>
        public void BuildSquareGrid(int baseSize, int? stackedLayers = null) =>
            BuildGrid(baseSize, baseSize, stackedLayers);

        void WireBlockers()
        {
            foreach (var cell in _cells)
            {
                if (cell.Layer >= StackedLayerCount - 1)
                {
                    cell.BlockerIds = Array.Empty<int>();
                    continue;
                }

                int upperLayer = cell.Layer + 1;
                int r = cell.Row;
                int c = cell.Col;
                var blockers = new List<int>(4);
                TryAddBlocker(upperLayer, r - 1, c - 1, blockers);
                TryAddBlocker(upperLayer, r - 1, c, blockers);
                TryAddBlocker(upperLayer, r, c - 1, blockers);
                TryAddBlocker(upperLayer, r, c, blockers);
                cell.BlockerIds = blockers.Count > 0 ? blockers.ToArray() : Array.Empty<int>();
            }
        }

        void TryAddBlocker(int layer, int row, int col, List<int> outIds)
        {
            if (_cellAt.TryGetValue((layer, row, col), out var blocker))
                outIds.Add(blocker.Id);
        }

        /// <summary>
        /// 将 1..pairCount 各两张随机填入格子；格子总数必须等于 2 * pairCount。
        /// </summary>
        public void DealPairs(int pairCount)
        {
            if (pairCount < 1)
                throw new ArgumentOutOfRangeException(nameof(pairCount));
            int need = pairCount * 2;
            if (_cells.Count != need)
                throw new InvalidOperationException(
                    $"格子数为 {_cells.Count}，但 {pairCount} 对需要 {need} 格。当前底层 {RowCount}×{ColCount}、层数={StackedLayerCount} 时最多 {GetMaxPairCount(RowCount, ColCount, StackedLayerCount)} 对。");

            var values = new List<int>(need);
            for (int v = 1; v <= pairCount; v++)
            {
                values.Add(v);
                values.Add(v);
            }
            Shuffle(values);

            for (int i = 0; i < _cells.Count; i++)
            {
                _cells[i].Eliminated = false;
                _cells[i].Value = values[i];
            }

            _selectedId = null;
        }

        void Shuffle(IList<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public LayeredGridCell GetCell(int id)
        {
            if ((uint)id >= (uint)_cells.Count)
                throw new ArgumentOutOfRangeException(nameof(id));
            return _cells[id];
        }

        public bool CanClick(int cellId)
        {
            if ((uint)cellId >= (uint)_cells.Count) return false;
            var cell = _cells[cellId];
            if (cell.Eliminated) return false;

            for (int i = 0; i < cell.BlockerIds.Length; i++)
            {
                var b = _cells[cell.BlockerIds[i]];
                if (!b.Eliminated) return false;
            }

            return true;
        }

        /// <summary>尝试点击一格：第一次选中，第二次比对数字。</summary>
        public LayeredMatchClickResult TryClick(int cellId)
        {
            if ((uint)cellId >= (uint)_cells.Count)
                return LayeredMatchClickResult.Invalid;

            var cell = _cells[cellId];
            if (!CanClick(cellId))
                return LayeredMatchClickResult.Invalid;

            if (!_selectedId.HasValue)
            {
                _selectedId = cellId;
                return LayeredMatchClickResult.SelectedFirst;
            }

            if (_selectedId.Value == cellId)
            {
                _selectedId = null;
                return LayeredMatchClickResult.Deselected;
            }

            var first = _cells[_selectedId.Value];
            _selectedId = null;

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
