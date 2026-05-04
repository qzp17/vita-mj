using System.Collections.Generic;
using FairyGUI;
using UnityEngine;
using VitaMj.MatchGame;

/// <summary>
/// 将 <see cref="IPairMatchGame"/>（程序化 <see cref="LayeredPairMatchGame"/> 或表驱动 <see cref="ConfiguredPairMatchGame"/>）
/// 与 FairyGUI <c>Package1/card</c> 绑定；下层被上层挡住时不可点（灰显）；先后点两枚数字相同则消除。
/// </summary>
public sealed class LayeredMatchBoardBinder
{
    const int DefaultRows = 4;
    const int DefaultCols = 5;
    const int DefaultLayers = 2;

    const string TrSelect = "select";
    const string TrSucc = "succ";
    const string TrFail = "fail";

    readonly Dictionary<int, GComponent> _cards = new Dictionary<int, GComponent>();
    IPairMatchGame _model;
    GComponent _holder;
    EventCallback1 _clickHandler;
    string _packageName;
    string _cardComponentName;
    float _gap;

    /// <summary>配对成功/失败动效播放期间禁止操作棋盘。</summary>
    bool _boardLocked;

    System.Action _onBoardComplete;

    public IPairMatchGame Model => _model;

    /// <summary>FairyGUI 里用于摆放卡片的容器；建议在 game_view 中建名为 card_holder 的空组件。</summary>
    public const string HolderChildName = "card_holder";

    /// <param name="level">非空则按 <see cref="ConfiguredPairMatchGame"/> 布局；为空则使用默认 4×5×2 程序化棋盘。</param>
    /// <param name="onBoardComplete">全部消除且结算动效结束后调用（仅触发一次）。</param>
    public void Bind(GComponent gameViewRoot, string packageName, string cardComponentName, float cellGap, MatchLevelDefinition level = null, System.Action onBoardComplete = null)
    {
        Dispose();
        _packageName = packageName;
        _cardComponentName = cardComponentName;
        _gap = cellGap;
        _onBoardComplete = onBoardComplete;

        GObject holderObj = gameViewRoot.GetChild(HolderChildName);
        _holder = holderObj != null ? holderObj.asCom : gameViewRoot;

        if (level != null)
            _model = new ConfiguredPairMatchGame(level);
        else
        {
            var proc = new LayeredPairMatchGame(Random.Range(int.MinValue, int.MaxValue));
            proc.BuildGrid(DefaultRows, DefaultCols, DefaultLayers);
            int pairs = LayeredPairMatchGame.GetMaxPairCount(DefaultRows, DefaultCols, DefaultLayers);
            proc.DealPairs(pairs);
            _model = proc;
        }

        GObject templateProbe = UIPackage.CreateObject(packageName, cardComponentName);
        if (templateProbe == null || templateProbe is not GComponent)
        {
            Debug.LogError($"[LayeredMatchBoardBinder] 无法创建 {packageName}/{cardComponentName}，请确认 FairyGUI 已导出该组件。");
            templateProbe?.Dispose();
            _model = null;
            return;
        }

        float cellW = templateProbe.sourceWidth > 1 ? templateProbe.sourceWidth : templateProbe.width;
        float cellH = templateProbe.sourceHeight > 1 ? templateProbe.sourceHeight : templateProbe.height;
        templateProbe.Dispose();

        float stepX = cellW + _gap;
        float stepY = cellH + _gap;
        float layerShiftX = stepX * 0.5f;
        float layerShiftY = stepY * 0.5f;

        ComputeGridFootprint(_model, out int rowCount, out int colCount);
        float gridW0 = colCount * cellW + (colCount - 1) * _gap;
        float gridH0 = rowCount * cellH + (rowCount - 1) * _gap;
        float ox = Mathf.Max(0f, (_holder.width - gridW0) * 0.5f);
        float oy = Mathf.Max(0f, (_holder.height - gridH0) * 0.5f);

        _clickHandler = OnCardClick;

        var sortedCells = new List<LayeredGridCell>(_model.Cells.Count);
        foreach (var cell in _model.Cells)
            sortedCells.Add(cell);
        sortedCells.Sort((a, b) =>
        {
            int lr = a.Layer.CompareTo(b.Layer);
            if (lr != 0) return lr;
            int rr = a.Row.CompareTo(b.Row);
            return rr != 0 ? rr : a.Col.CompareTo(b.Col);
        });

        foreach (var cell in sortedCells)
        {
            GObject obj = UIPackage.CreateObject(_packageName, _cardComponentName);
            var card = obj as GComponent;
            if (card == null)
            {
                obj?.Dispose();
                continue;
            }

            card.data = cell.Id;
            if (cell.UseDesignerCoordinates)
                card.SetXY(cell.DisplayX, cell.DisplayY);
            else
            {
                float baseX = ox + cell.Col * stepX;
                float baseY = oy + cell.Row * stepY;
                if (cell.Layer > 0)
                {
                    baseX += layerShiftX * cell.Layer;
                    baseY += layerShiftY * cell.Layer;
                }

                card.SetXY(baseX, baseY);
            }
            _holder.AddChild(card);
            _cards[cell.Id] = card;

            ApplyCardFace(card, cell.Value);

            card.onClick.Add(_clickHandler);
        }

        RefreshAllCards();

        var tip = gameViewRoot.GetChild("txt_tip")?.asTextField;
        if (tip != null)
            tip.text = "翻开两张相同数字即可消除（上层未消尽时下层锁住）";
    }

    static GTextField ResolveNumberLabel(GComponent card)
    {
        foreach (var name in new[] { "num", "title", "value", "number", "count" })
        {
            var child = card.GetChild(name);
            if (child is GTextField tf)
                return tf;
        }

        int n = card.numChildren;
        for (int i = 0; i < n; i++)
        {
            if (card.GetChildAt(i) is GTextField tf)
                return tf;
        }

        return null;
    }

    /// <summary>
    /// card 内需有名为 loader 的装载器；同包下图片资源命名为与牌面数字一致的字符串（如 "1"～"12"）。
    /// </summary>
    void ApplyCardFace(GComponent card, int faceValue)
    {
        GLoader loader = ResolveCardLoader(card);
        if (loader != null)
        {
            string url = UIPackage.GetItemURL(_packageName, faceValue.ToString());
            loader.url = url ?? string.Empty;
        }

        GTextField label = ResolveNumberLabel(card);
        if (label != null)
            label.text = faceValue.ToString();

        // Loader / 文本默认会拦截点击，导致点到子控件时父级 card.onClick 不触发，看起来像「仍不可点」。
        EnsureDecorationsNonTouchable(card);
    }

    static void EnsureDecorationsNonTouchable(GComponent card)
    {
        int n = card.numChildren;
        for (int i = 0; i < n; i++)
            DisableHitOnDecorationSubtree(card.GetChildAt(i), 0);
    }

    static void DisableHitOnDecorationSubtree(GObject o, int depth)
    {
        if (o == null || depth > 12)
            return;

        if (o is GLoader || o is GTextField || o is GRichTextField)
        {
            o.touchable = false;
            return;
        }

        if (o is GComponent com)
        {
            int n = com.numChildren;
            for (int i = 0; i < n; i++)
                DisableHitOnDecorationSubtree(com.GetChildAt(i), depth + 1);
        }
    }

    /// <summary>点击可能落在 loader / 文本上，沿 parent 找到挂了 cell.Id 的 card。</summary>
    static bool TryResolveCellId(GObject sender, out int cellId)
    {
        GObject go = sender;
        while (go != null)
        {
            if (go.data is int id)
            {
                cellId = id;
                return true;
            }

            go = go.parent;
        }

        cellId = default;
        return false;
    }

    static GLoader ResolveCardLoader(GComponent card)
    {
        foreach (var name in new[] { "loader", "icon", "img", "n0" })
        {
            GObject child = card.GetChild(name);
            if (child is GLoader ld)
                return ld;
        }

        int n = card.numChildren;
        for (int i = 0; i < n; i++)
        {
            if (card.GetChildAt(i) is GLoader ld)
                return ld;
        }

        return null;
    }

    void OnCardClick(EventContext context)
    {
        if (_model == null || context.sender == null || _boardLocked)
            return;

        if (!TryResolveCellId(context.sender as GObject, out int cellId))
        {
            RefreshAllCards();
            return;
        }

        int? selBefore = _model.SelectedCellId;

        var result = _model.TryClick(cellId);

        switch (result)
        {
            case LayeredMatchClickResult.Invalid:
                RefreshAllCards();
                break;

            case LayeredMatchClickResult.SelectedFirst:
                SafePlayTransition(GetCard(cellId), TrSelect, null);
                RefreshAllCards();
                break;

            case LayeredMatchClickResult.Deselected:
                GetCard(cellId)?.GetTransition(TrSelect)?.PlayReverse();
                RefreshAllCards();
                break;

            case LayeredMatchClickResult.MismatchedPair:
                if (selBefore.HasValue)
                    PlayPairTransitionThenRefresh(selBefore.Value, cellId, TrFail);
                else
                    RefreshAllCards();
                break;

            case LayeredMatchClickResult.MatchedPair:
                if (selBefore.HasValue)
                    PlayPairTransitionThenRefresh(selBefore.Value, cellId, TrSucc, AfterMatchedRefresh);
                else
                    RefreshAllCards();
                break;
        }
    }

    void AfterMatchedRefresh()
    {
        if (_model != null && _model.IsComplete)
            OnBoardComplete();
    }

    void OnBoardComplete()
    {
        Debug.Log("[LayeredMatchBoardBinder] 全部消除，关卡完成");
        _onBoardComplete?.Invoke();
        _onBoardComplete = null;
    }

    void RefreshAllCards()
    {
        if (_model == null)
            return;

        foreach (var kv in _cards)
        {
            int id = kv.Key;
            var card = kv.Value;
            if (card == null || card.isDisposed)
                continue;

            var cell = _model.GetCell(id);
            if (cell.Eliminated)
            {
                card.visible = false;
                continue;
            }

            card.visible = true;

            ApplyCardFace(card, cell.Value);

            bool can = _model.CanClick(id);
            card.grayed = !can;
            card.touchable = can && !_boardLocked;

            // 选中态由 FairyGUI transition「select」表现，不再代码写缩放。
        }
    }

    GComponent GetCard(int cellId)
    {
        _cards.TryGetValue(cellId, out var card);
        return card;
    }

    /// <summary>两张牌同时播同一动效，均结束（或无该动效）后解锁并刷新棋盘。</summary>
    void PlayPairTransitionThenRefresh(int idA, int idB, string transitionName, System.Action afterRefresh = null)
    {
        var cA = GetCard(idA);
        var cB = GetCard(idB);

        StopTransitionIfPlaying(cA, TrSelect);
        StopTransitionIfPlaying(cB, TrSelect);

        _boardLocked = true;

        int remaining = 2;
        void OnOneEnd()
        {
            remaining--;
            if (remaining > 0)
                return;
            _boardLocked = false;
            RefreshAllCards();
            afterRefresh?.Invoke();
        }

        SafePlayTransition(cA, transitionName, OnOneEnd);
        SafePlayTransition(cB, transitionName, OnOneEnd);
    }

    static void SafePlayTransition(GComponent card, string transitionName, PlayCompleteCallback onComplete)
    {
        if (card == null || card.isDisposed)
        {
            onComplete?.Invoke();
            return;
        }

        Transition tr = card.GetTransition(transitionName);
        if (tr != null)
        {
            if (onComplete != null)
                tr.Play(onComplete);
            else
                tr.Play();
        }
        else
            onComplete?.Invoke();
    }

    static void StopTransitionIfPlaying(GComponent card, string transitionName)
    {
        if (card == null || card.isDisposed)
            return;
        Transition tr = card.GetTransition(transitionName);
        if (tr != null && tr.playing)
            tr.Stop();
    }

    public void Dispose()
    {
        _boardLocked = false;

        if (_cards.Count > 0 && _clickHandler != null)
        {
            foreach (var kv in _cards)
            {
                var card = kv.Value;
                if (card != null && !card.isDisposed)
                {
                    card.onClick.Remove(_clickHandler);
                    StopTransitionIfPlaying(card, TrSelect);
                    StopTransitionIfPlaying(card, TrSucc);
                    StopTransitionIfPlaying(card, TrFail);
                }
            }
        }

        _cards.Clear();
        _clickHandler = null;
        _holder = null;
        _model = null;
        _onBoardComplete = null;
    }

    /// <summary>仅统计未使用策划绝对坐标的牌，用于居中推算网格；若全部为绝对坐标则退化为 1×1。</summary>
    static void ComputeGridFootprint(IPairMatchGame model, out int rowCount, out int colCount)
    {
        int maxRow = 0;
        int maxCol = 0;
        bool any = false;
        foreach (LayeredGridCell cell in model.Cells)
        {
            if (cell.UseDesignerCoordinates)
                continue;
            any = true;
            if (cell.Row > maxRow)
                maxRow = cell.Row;
            if (cell.Col > maxCol)
                maxCol = cell.Col;
        }

        if (!any)
        {
            rowCount = 1;
            colCount = 1;
            return;
        }

        rowCount = maxRow + 1;
        colCount = maxCol + 1;
    }
}
