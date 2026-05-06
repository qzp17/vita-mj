using System;
using System.Collections.Generic;

namespace VitaMj.MatchGame
{
    /// <summary>
    /// 由 <see cref="MatchLevelDefinition"/> 构建的配对局：牌数量、层、逻辑格点、坐标与遮挡均可不规则。
    /// </summary>
    public sealed class ConfiguredPairMatchGame : IPairMatchGame
    {
        readonly List<LayeredGridCell> _cells = new List<LayeredGridCell>();
        readonly Dictionary<(int layer, int row, int col), LayeredGridCell> _cellAt =
            new Dictionary<(int layer, int row, int col), LayeredGridCell>();
        readonly Dictionary<string, int> _designerIdToRuntimeId = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly Random _rng;
        readonly List<int> _matchBar = new List<int>();
        readonly List<int> _lastMatchBarMergedAway = new List<int>();
        readonly MatchPlayStyle _playStyle;

        static readonly int[] EmptyCellIdList = Array.Empty<int>();

        int _configuredQueueSlots;
        int _matchBarCapacity;
        int? _selectedId;

        internal const int DefaultQueueSlotsWhenConfigZero = 7;

        public IReadOnlyList<LayeredGridCell> Cells => _cells;
        public int? SelectedCellId => _selectedId;

        public int MatchBarCapacity => _matchBarCapacity;

        public IReadOnlyList<int> MatchBarCellIds => _matchBar;

        public IReadOnlyList<int> LastMatchBarMergedAwayCellIds =>
            _matchBarCapacity > 0 ? _lastMatchBarMergedAway : EmptyCellIdList;

        public bool IsComplete
        {
            get
            {
                for (int i = 0; i < _cells.Count; i++)
                {
                    if (!_cells[i].Eliminated)
                        return false;
                }

                return true;
            }
        }

        public ConfiguredPairMatchGame(
            MatchLevelDefinition definition,
            MatchPlayStyle playStyle = MatchPlayStyle.ClassicPairClick,
            int? seedOverride = null)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            _playStyle = playStyle;

            int seed = seedOverride ?? (definition.randomSeed != 0 ? definition.randomSeed : Environment.TickCount);
            _rng = new Random(seed);

            Build(definition);
        }

        void Build(MatchLevelDefinition def)
        {
            _cells.Clear();
            _cellAt.Clear();
            _designerIdToRuntimeId.Clear();
            _selectedId = null;
            _matchBar.Clear();
            _configuredQueueSlots = def.queueMaxSlots < 0 ? 0 : def.queueMaxSlots;

            var rows = def.cards;
            if (rows == null || rows.Count == 0)
                throw new InvalidOperationException("MatchLevelDefinition.cards 为空。");

            if ((rows.Count & 1) == 1)
                throw new InvalidOperationException($"关卡牌数必须为偶数（当前 {rows.Count}）。");

            for (int i = 0; i < rows.Count; i++)
            {
                MatchLevelCardRow row = rows[i];
                if (string.IsNullOrWhiteSpace(row.cardId))
                    throw new InvalidOperationException($"第 {i} 行 cardId 为空。");

                if (_designerIdToRuntimeId.ContainsKey(row.cardId))
                    throw new InvalidOperationException($"重复的 cardId：{row.cardId}");

                var key = (row.layer, row.logicRow, row.logicCol);
                if (_cellAt.ContainsKey(key))
                    throw new InvalidOperationException($"重复的(layer,logicRow,logicCol)=({key.layer},{key.logicRow},{key.logicCol})，cardId={row.cardId}");

                int id = _cells.Count;
                var cell = new LayeredGridCell(id, row.layer, row.logicRow, row.logicCol, row.cardId.Trim())
                {
                    DisplayX = row.displayX,
                    DisplayY = row.displayY,
                    UseDesignerCoordinates = row.useAbsolutePosition,
                };
                _cells.Add(cell);
                _cellAt[key] = cell;
                _designerIdToRuntimeId[row.cardId.Trim()] = id;
            }

            WireBlockers(def);
            AssignFaces(def);
            ApplyMatchPlayStyle();
        }

        void ApplyMatchPlayStyle()
        {
            _selectedId = null;
            _matchBar.Clear();
            switch (_playStyle)
            {
                case MatchPlayStyle.ClassicPairClick:
                    _matchBarCapacity = 0;
                    break;
                case MatchPlayStyle.MatchBarQueue:
                    _matchBarCapacity =
                        _configuredQueueSlots > 0 ? _configuredQueueSlots : DefaultQueueSlotsWhenConfigZero;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (_cells.Count > 0)
            {
                for (int i = 0; i < _cells.Count; i++)
                    _cells[i].Eliminated = false;
            }
        }

        void WireBlockers(MatchLevelDefinition def)
        {
            for (int i = 0; i < _cells.Count; i++)
            {
                LayeredGridCell cell = _cells[i];
                MatchLevelCardRow row = def.cards[i];

                if (row.blockedByCardIds != null && row.blockedByCardIds.Count > 0)
                {
                    var ids = new List<int>(row.blockedByCardIds.Count);
                    foreach (string bid in row.blockedByCardIds)
                    {
                        if (string.IsNullOrWhiteSpace(bid))
                            continue;
                        string k = bid.Trim();
                        if (!_designerIdToRuntimeId.TryGetValue(k, out int blockerId))
                            throw new InvalidOperationException($"cardId={row.cardId} 的 blockedBy 引用未知 id：{k}");

                        LayeredGridCell blockerCell = _cells[blockerId];
                        if (blockerCell.Layer != cell.Layer + 1)
                            throw new InvalidOperationException(
                                $"遮挡关系错误：{row.cardId} 声明由 {k} 挡住，但二者不满足「上层 layer = 下层 layer + 1」（{blockerCell.Layer} vs {cell.Layer}）。");

                        ids.Add(blockerId);
                    }

                    cell.BlockerIds = ids.Count > 0 ? ids.ToArray() : Array.Empty<int>();
                }
                else
                    WireBlockersGeometric(cell);
            }
        }

        void WireBlockersGeometric(LayeredGridCell cell)
        {
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

        void TryAddBlocker(int layer, int row, int col, List<int> outIds)
        {
            if (_cellAt.TryGetValue((layer, row, col), out var blocker))
                outIds.Add(blocker.Id);
        }

        void AssignFaces(MatchLevelDefinition def)
        {
            switch (def.faceMode)
            {
                case MatchLevelFaceMode.ExplicitPairs:
                    AssignExplicitPairs(def);
                    break;
                case MatchLevelFaceMode.RandomPairsFromRange:
                    AssignRandomPairs(def);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _selectedId = null;
            _matchBar.Clear();
            // Eliminated 与收纳栏重置由 Build 末尾 <see cref="ApplyMatchPlayStyle"/> 统一处理。
        }

        void AssignExplicitPairs(MatchLevelDefinition def)
        {
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < _cells.Count; i++)
            {
                int v = def.cards[i].faceValue;
                if (v < 1)
                    throw new InvalidOperationException($"ExplicitPairs 模式下 faceValue 必须 ≥1（cardId={def.cards[i].cardId}）。");

                _cells[i].Value = v;
                counts.TryGetValue(v, out int n);
                counts[v] = n + 1;
            }

            foreach (var kv in counts)
            {
                if ((kv.Value & 1) == 1)
                    throw new InvalidOperationException($"ExplicitPairs：数字 {kv.Key} 出现 {kv.Value} 次，无法两两配对。");
            }
        }

        void AssignRandomPairs(MatchLevelDefinition def)
        {
            int vmin = def.valueMin;
            int vmax = def.valueMax;
            if (vmin < 1 || vmax < vmin)
                throw new InvalidOperationException($"数值范围非法：valueMin={vmin}, valueMax={vmax}");

            int pairCount = _cells.Count / 2;
            var values = new List<int>(_cells.Count);
            for (int p = 0; p < pairCount; p++)
            {
                int v = _rng.Next(vmin, vmax + 1);
                values.Add(v);
                values.Add(v);
            }

            Shuffle(values);
            for (int i = 0; i < _cells.Count; i++)
                _cells[i].Value = values[i];
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

        public bool CanClick(int cellId) =>
            PairMatchRules.CanClick(_cells, cellId);

        public LayeredMatchClickResult TryClick(int cellId)
        {
            if (_matchBarCapacity > 0)
            {
                _selectedId = null;
                _lastMatchBarMergedAway.Clear();
                LayeredMatchClickResult r = PairMatchRules.TryMatchBarClick(
                    _cells, _matchBar, _matchBarCapacity, cellId, _lastMatchBarMergedAway);
                if (r != LayeredMatchClickResult.MatchBarMerged)
                    _lastMatchBarMergedAway.Clear();
                return r;
            }

            _lastMatchBarMergedAway.Clear();
            return PairMatchRules.TryClick(_cells, ref _selectedId, cellId);
        }

        public bool TryRevertLastMatchBarEntry()
        {
            if (_matchBarCapacity <= 0)
                return false;
            return PairMatchRules.TryMatchBarRevert(_cells, _matchBar);
        }
    }
}
