using System;
using System.Collections.Generic;
using FairyGUI;
using UnityEngine;

/// <summary>
/// game_arrow_view 的网格箭头线段玩法：
/// 点击任意线段，整条线按头部方向前进一步；头部出界即整条消除。
/// </summary>
public sealed class ArrowEliminationBoardBinder : IDisposable
{
    enum ArrowDirection
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3,
    }

    sealed class ArrowLine
    {
        public int Id;
        public ArrowDirection Direction;
        public List<Vector2Int> Segments = new List<Vector2Int>(); // 0 为头部
    }

    const float DefaultCellSize = 10f;
    const int MinRows = 16;
    const int MinCols = 24;

    ArrowGameUIView _view;
    GComponent _panel;
    GComponent _holder;
    GTextField _hintText;
    string _packageName;
    string _arrowComponentName;
    float _cellSize = DefaultCellSize;
    Action _onAllCleared;

    readonly List<ArrowLine> _lines = new List<ArrowLine>();
    readonly List<ArrowLine> _initialLines = new List<ArrowLine>();
    readonly List<GObject> _segmentViews = new List<GObject>();
    readonly System.Random _rng = new System.Random();

    int _cols;
    int _rows;
    bool _disposed;
    bool _interactionLocked;

    public void Bind(
        ArrowGameUIView view,
        string packageName,
        string arrowComponentName,
        Action onAllCleared)
    {
        Dispose();

        _view = view;
        _panel = view?.BoardPanel;
        _packageName = packageName;
        _arrowComponentName = string.IsNullOrWhiteSpace(arrowComponentName) ? "arrow" : arrowComponentName.Trim();
        _onAllCleared = onAllCleared;
        _hintText = view?.Root?.GetChild("txt_hint")?.asTextField;
        _disposed = false;
        _interactionLocked = false;

        if (_panel == null || _panel.isDisposed)
        {
            Debug.LogError("[ArrowEliminationBoardBinder] game_arrow_view 未找到 panel/board_panel/arrow_panel。");
            return;
        }

        _holder = new GComponent();
        _holder.name = "arrow_holder_runtime";
        _holder.touchable = true;
        _holder.SetSize(_panel.width, _panel.height);
        _panel.AddChild(_holder);

        _cellSize = DefaultCellSize;
        int w = Mathf.Max(1, Mathf.FloorToInt(_panel.width / _cellSize));
        int h = Mathf.Max(1, Mathf.FloorToInt(_panel.height / _cellSize));
        _cols = Mathf.Max(MinCols, w);
        _rows = Mathf.Max(MinRows, h);

        GenerateRandomLevel();
        SaveInitialSnapshot();
        RefreshBoardVisual();
        SetHint("点击任意线段，让整条线按箭头方向移动。");
    }

    public void Dispose()
    {
        _interactionLocked = true;
        ClearSegmentViews();
        _lines.Clear();
        _initialLines.Clear();

        if (_holder != null && !_holder.isDisposed)
            _holder.Dispose();
        _holder = null;

        _panel = null;
        _view = null;
        _hintText = null;
        _onAllCleared = null;
        _disposed = true;
    }

    void GenerateRandomLevel()
    {
        _lines.Clear();

        var occupied = new HashSet<Vector2Int>();
        int nextId = 1;
        int lineCount = Mathf.Clamp((_cols * _rows) / 350, 5, 9);

        ArrowLine guaranteed = CreateGuaranteedExitLine(nextId++, occupied);
        if (guaranteed != null)
            _lines.Add(guaranteed);

        int maxAttempts = 240;
        while (_lines.Count < lineCount && maxAttempts-- > 0)
        {
            ArrowLine line = TryCreateRandomLine(nextId++, occupied);
            if (line == null)
                continue;
            _lines.Add(line);
        }
    }

    void SaveInitialSnapshot()
    {
        _initialLines.Clear();
        for (int i = 0; i < _lines.Count; i++)
            _initialLines.Add(CloneLine(_lines[i]));
    }

    public void ResetToInitial()
    {
        if (_disposed)
            return;
        _interactionLocked = false;
        _lines.Clear();
        for (int i = 0; i < _initialLines.Count; i++)
            _lines.Add(CloneLine(_initialLines[i]));
        RefreshBoardVisual();
        SetHint("已重开：恢复到本关初始状态。");
    }

    public void RandomizeNextLevel()
    {
        if (_disposed)
            return;
        _interactionLocked = false;
        GenerateRandomLevel();
        SaveInitialSnapshot();
        RefreshBoardVisual();
        SetHint("下一关：已生成新的随机布局。");
    }

    ArrowLine CreateGuaranteedExitLine(int id, HashSet<Vector2Int> occupied)
    {
        ArrowDirection dir = (ArrowDirection)_rng.Next(0, 4);
        int len = _rng.Next(4, 8);
        Vector2Int head;

        switch (dir)
        {
            case ArrowDirection.Left:
                head = new Vector2Int(0, _rng.Next(2, Mathf.Max(3, _rows - 2)));
                break;
            case ArrowDirection.Right:
                head = new Vector2Int(_cols - 1, _rng.Next(2, Mathf.Max(3, _rows - 2)));
                break;
            case ArrowDirection.Up:
                head = new Vector2Int(_rng.Next(2, Mathf.Max(3, _cols - 2)), 0);
                break;
            default:
                head = new Vector2Int(_rng.Next(2, Mathf.Max(3, _cols - 2)), _rows - 1);
                break;
        }

        List<Vector2Int> segs = BuildSegments(head, dir, len);
        if (!AllInside(segs))
            return null;
        if (!Reserve(occupied, segs))
            return null;

        return new ArrowLine { Id = id, Direction = dir, Segments = segs };
    }

    ArrowLine TryCreateRandomLine(int id, HashSet<Vector2Int> occupied)
    {
        int len = _rng.Next(3, 9);
        ArrowDirection dir = (ArrowDirection)_rng.Next(0, 4);

        for (int i = 0; i < 36; i++)
        {
            Vector2Int head = new Vector2Int(
                _rng.Next(1, Mathf.Max(2, _cols - 1)),
                _rng.Next(1, Mathf.Max(2, _rows - 1)));

            List<Vector2Int> segs = BuildSegments(head, dir, len);
            if (!AllInside(segs))
                continue;
            if (!Reserve(occupied, segs))
                continue;

            return new ArrowLine { Id = id, Direction = dir, Segments = segs };
        }

        return null;
    }

    static List<Vector2Int> BuildSegments(Vector2Int head, ArrowDirection dir, int len)
    {
        Vector2Int dv = DirectionToDelta(dir);
        var segs = new List<Vector2Int>(len);
        for (int i = 0; i < len; i++)
            segs.Add(head - dv * i);
        return segs;
    }

    static bool Reserve(HashSet<Vector2Int> occupied, List<Vector2Int> cells)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (occupied.Contains(cells[i]))
                return false;
        }

        for (int i = 0; i < cells.Count; i++)
            occupied.Add(cells[i]);
        return true;
    }

    bool AllInside(List<Vector2Int> cells)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (!IsInside(cells[i]))
                return false;
        }

        return true;
    }

    bool IsInside(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < _cols && p.y < _rows;

    void RefreshBoardVisual()
    {
        if (_holder == null || _holder.isDisposed)
            return;

        ClearSegmentViews();

        for (int i = 0; i < _lines.Count; i++)
        {
            ArrowLine line = _lines[i];
            for (int s = 0; s < line.Segments.Count; s++)
            {
                GObject obj = UIPackage.CreateObject(_packageName, _arrowComponentName);
                var com = obj as GComponent;
                if (com == null)
                {
                    obj?.Dispose();
                    continue;
                }

                com.SetSize(_cellSize, _cellSize);
                Vector2Int gp = line.Segments[s];
                com.SetXY(gp.x * _cellSize, gp.y * _cellSize);
                ApplyArrowState(com, line.Direction, s == 0);

                int lineId = line.Id;
                com.onClick.Add(_ => OnSegmentClicked(lineId));

                _holder.AddChild(com);
                _segmentViews.Add(com);
            }
        }
    }

    void ClearSegmentViews()
    {
        for (int i = 0; i < _segmentViews.Count; i++)
        {
            GObject obj = _segmentViews[i];
            if (obj != null && !obj.isDisposed)
                obj.Dispose();
        }

        _segmentViews.Clear();
    }

    void OnSegmentClicked(int lineId)
    {
        if (_disposed || _interactionLocked)
            return;

        ArrowLine line = _lines.Find(l => l.Id == lineId);
        if (line == null)
            return;

        List<Vector2Int> snapshot = new List<Vector2Int>(line.Segments);
        StepFollowMove(line);

        if (!IsInside(line.Segments[0]))
        {
            _lines.Remove(line);
            RefreshBoardVisual();
            if (_lines.Count == 0)
            {
                _interactionLocked = true;
                SetHint("已消除全部线段，通关！");
                _onAllCleared?.Invoke();
            }
            else
            {
                SetHint("成功消除一条线段。");
            }

            return;
        }

        if (IsBlocked(line))
        {
            line.Segments = snapshot;
            SetHint("被阻挡，已回退到原位置。");
            return;
        }

        RefreshBoardVisual();
    }

    static void StepFollowMove(ArrowLine line)
    {
        Vector2Int oldHead = line.Segments[0];
        for (int i = line.Segments.Count - 1; i >= 1; i--)
            line.Segments[i] = line.Segments[i - 1];
        line.Segments[0] = oldHead + DirectionToDelta(line.Direction);
    }

    static ArrowLine CloneLine(ArrowLine src)
    {
        var dst = new ArrowLine
        {
            Id = src.Id,
            Direction = src.Direction,
            Segments = new List<Vector2Int>(src.Segments.Count),
        };

        for (int i = 0; i < src.Segments.Count; i++)
            dst.Segments.Add(src.Segments[i]);
        return dst;
    }

    bool IsBlocked(ArrowLine movingLine)
    {
        var selfSet = new HashSet<Vector2Int>();
        for (int i = 0; i < movingLine.Segments.Count; i++)
        {
            Vector2Int p = movingLine.Segments[i];
            if (!IsInside(p))
                return true;
            if (!selfSet.Add(p))
                return true; // 碰自身
        }

        var occupiedByOthers = new HashSet<Vector2Int>();
        for (int i = 0; i < _lines.Count; i++)
        {
            ArrowLine line = _lines[i];
            if (line.Id == movingLine.Id)
                continue;
            for (int s = 0; s < line.Segments.Count; s++)
                occupiedByOthers.Add(line.Segments[s]);
        }

        for (int i = 0; i < movingLine.Segments.Count; i++)
        {
            if (occupiedByOthers.Contains(movingLine.Segments[i]))
                return true;
        }

        return false;
    }

    static Vector2Int DirectionToDelta(ArrowDirection dir)
    {
        return dir switch
        {
            ArrowDirection.Up => new Vector2Int(0, -1),
            ArrowDirection.Down => new Vector2Int(0, 1),
            ArrowDirection.Left => new Vector2Int(-1, 0),
            _ => new Vector2Int(1, 0),
        };
    }

    static void ApplyArrowState(GComponent com, ArrowDirection dir, bool isHead)
    {
        Controller dirCtrl = com.GetController("direction");
        if (dirCtrl != null)
        {
            string page = dir switch
            {
                ArrowDirection.Up => "up",
                ArrowDirection.Down => "down",
                ArrowDirection.Left => "left",
                _ => "right",
            };
            TrySetControllerPage(dirCtrl, page, (int)dir);
        }

        Controller headCtrl = com.GetController("head");
        if (headCtrl != null)
        {
            if (isHead)
                TrySetControllerPage(headCtrl, "head", 1);
            else
                TrySetControllerPage(headCtrl, "body", 0);
        }
    }

    static void TrySetControllerPage(Controller ctrl, string pageName, int fallbackIndex)
    {
        if (ctrl == null)
            return;

        for (int i = 0; i < ctrl.pageCount; i++)
        {
            string n = ctrl.GetPageName(i);
            if (string.Equals(n, pageName, StringComparison.OrdinalIgnoreCase))
            {
                ctrl.selectedIndex = i;
                return;
            }
        }

        if (ctrl.pageCount <= 0)
            return;
        ctrl.selectedIndex = Mathf.Clamp(fallbackIndex, 0, ctrl.pageCount - 1);
    }

    void SetHint(string text)
    {
        if (_hintText != null && !_hintText.isDisposed)
            _hintText.text = text ?? string.Empty;
    }
}
