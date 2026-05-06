using System.Collections.Generic;
using FairyGUI;
using UnityEngine;
using VitaMj.MatchGame;

/// <summary>
/// 将 <see cref="IPairMatchGame"/>（程序化 <see cref="LayeredPairMatchGame"/> 或表驱动 <see cref="ConfiguredPairMatchGame"/>）
/// 与 FairyGUI <c>Package1/card</c> 绑定；玩法由 <see cref="MatchPlayStyle"/> 决定：双点配对或收纳栏入队消除。
/// </summary>
public sealed class LayeredMatchBoardBinder
{
    const int DefaultRows = 4;
    const int DefaultCols = 5;
    const int DefaultLayers = 2;

    const string TrSelect = "select";
    const string TrSucc = "succ";
    const string TrFail = "fail";
    const string TrHelp = "help";

    readonly Dictionary<int, GComponent> _cards = new Dictionary<int, GComponent>();
    IPairMatchGame _model;
    GComponent _holder;
    EventCallback1 _clickHandler;
    string _packageName;
    string _cardComponentName;
    float _gap;

    /// <summary>配对成功/失败动效播放期间禁止操作棋盘。</summary>
    bool _boardLocked;

    int? _hintIdA;
    int? _hintIdB;

    System.Action _onBoardComplete;
    System.Action _onMatchBarOverflow;

    GComponent _gameViewRoot;
    GComponent _matchBarDock;

    float _cardW;
    float _cardH;
    float _layoutOx;
    float _layoutOy;
    float _gridW0;
    float _gridH0;
    float _stepX;
    float _stepY;
    float _layerShiftX;
    float _layerShiftY;
    int _rowCount;
    int _colCount;

    float _barFlyDuration = 0.35f;
    const int BoardCardSortingOrder = 0;
    const int BarCardSortingOrderBase = 50;

    readonly HashSet<int> _flightCellIds = new HashSet<int>();
    int _activeBarFlights;

    GButton _btnRevert;
    EventCallback _revertClickHandler;

    bool _cardEntranceActive;
    int _cardEntranceTweensRemain;
    System.Action _onBoardEntranceComplete;
    float _cardEntranceMoveDuration = 0.42f;
    float _cardEntranceStagger = 0.05f;

    public IPairMatchGame Model => _model;

    bool BlockGameplayInput =>
        _boardLocked ||
        (_model != null && _model.MatchBarCapacity > 0 && _activeBarFlights > 0) ||
        _cardEntranceActive;

    /// <summary>对局结束（如超时）时锁定全部卡片点击，不改变消除状态。</summary>
    public void SetBoardInteractionLocked(bool locked)
    {
        _boardLocked = locked;
        RefreshAllCards();
    }

    /// <summary>FairyGUI 里用于摆放卡片的容器；建议在 game_view 中建名为 card_holder 的空组件。</summary>
    public const string HolderChildName = "card_holder";

    /// <param name="level">非空则按 <see cref="ConfiguredPairMatchGame"/> 布局；为空则使用默认 4×5×2 程序化棋盘。</param>
    /// <param name="onBoardComplete">全部消除且结算动效结束后调用（仅触发一次）。</param>
    /// <param name="onMatchBarOverflow">收纳栏已满时再点可行格时触发对局失败（仅关卡 queueMaxSlots &gt; 0 时需要）。</param>
    /// <param name="matchBarDockPanelName">收纳栏锚点 panel（game_view 子节点），第一张牌左上角与之对齐；留空则栏位排在棋盘下方。</param>
    /// <param name="matchBarFlySeconds">入栏飞行动画时长（秒）。</param>
    /// <param name="enableCardBoardEntrance">开局是否播放卡牌从左/右侧错落飞入棋盘。</param>
    /// <param name="cardEntranceMoveSeconds">单张卡牌水平飞入耗时（秒）。</param>
    /// <param name="cardEntranceStaggerSeconds">相邻启动的时间间隔基底（秒内加随机错峰）。</param>
    /// <param name="onBoardEntranceComplete">全部落定后可点击并接受倒计时；需在关内限时前调用。</param>
    public void Bind(
        GComponent gameViewRoot,
        string packageName,
        string cardComponentName,
        float cellGap,
        MatchLevelDefinition level = null,
        System.Action onBoardComplete = null,
        System.Action onMatchBarOverflow = null,
        MatchPlayStyle playStyle = MatchPlayStyle.ClassicPairClick,
        int proceduralQueueMaxSlots = ConfiguredPairMatchGame.DefaultQueueSlotsWhenConfigZero,
        string matchBarDockPanelName = null,
        float matchBarFlySeconds = 0.35f,
        bool enableCardBoardEntrance = true,
        float cardEntranceMoveSeconds = 0.42f,
        float cardEntranceStaggerSeconds = 0.05f,
        System.Action onBoardEntranceComplete = null)
    {
        Dispose();
        _gameViewRoot = gameViewRoot;
        _packageName = packageName;
        _cardComponentName = cardComponentName;
        _gap = cellGap;
        _onBoardComplete = onBoardComplete;
        _onMatchBarOverflow = onMatchBarOverflow;
        _onBoardEntranceComplete = onBoardEntranceComplete;
        _cardEntranceMoveDuration = Mathf.Max(0.05f, cardEntranceMoveSeconds);
        _cardEntranceStagger = Mathf.Max(0f, cardEntranceStaggerSeconds);
        _barFlyDuration = Mathf.Max(0.05f, matchBarFlySeconds);

        if (!string.IsNullOrEmpty(matchBarDockPanelName))
            _matchBarDock = gameViewRoot.GetChild(matchBarDockPanelName)?.asCom;

        GObject holderObj = gameViewRoot.GetChild(HolderChildName);
        _holder = holderObj != null ? holderObj.asCom : gameViewRoot;

        if (level != null)
            _model = new ConfiguredPairMatchGame(level, playStyle);
        else
        {
            var proc = new LayeredPairMatchGame(Random.Range(int.MinValue, int.MaxValue));
            proc.BuildGrid(DefaultRows, DefaultCols, DefaultLayers);
            int pairs = LayeredPairMatchGame.GetMaxPairCount(DefaultRows, DefaultCols, DefaultLayers);
            proc.DealPairs(pairs);

            proc.MatchBarCapacity = playStyle == MatchPlayStyle.MatchBarQueue ? Mathf.Max(1, proceduralQueueMaxSlots) : 0;

            _model = proc;
        }

        GObject templateProbe = UIPackage.CreateObject(packageName, cardComponentName);
        if (templateProbe == null || templateProbe is not GComponent)
        {
            Debug.LogError($"[LayeredMatchBoardBinder] 无法创建 {packageName}/{cardComponentName}，请确认 FairyGUI 已导出该组件。");
            templateProbe?.Dispose();
            _model = null;
            InvokeBoardEntranceCompleteCallback();
            return;
        }

        float cellW = templateProbe.sourceWidth > 1 ? templateProbe.sourceWidth : templateProbe.width;
        float cellH = templateProbe.sourceHeight > 1 ? templateProbe.sourceHeight : templateProbe.height;
        templateProbe.Dispose();

        _cardW = cellW;
        _cardH = cellH;
        float stepX = cellW + _gap;
        float stepY = cellH + _gap;
        float layerShiftX = stepX * 0.5f;
        float layerShiftY = stepY * 0.5f;
        _stepX = stepX;
        _stepY = stepY;
        _layerShiftX = layerShiftX;
        _layerShiftY = layerShiftY;

        ComputeGridFootprint(_model, out _rowCount, out _colCount);
        _gridW0 = _colCount * cellW + (_colCount - 1) * _gap;
        _gridH0 = _rowCount * cellH + (_rowCount - 1) * _gap;
        _layoutOx = Mathf.Max(0f, (_holder.width - _gridW0) * 0.5f);
        _layoutOy = Mathf.Max(0f, (_holder.height - _gridH0) * 0.5f);

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

            Vector2 settle = ComputeBoardSlotLocal(cell);
            if (!enableCardBoardEntrance || cardEntranceMoveSeconds <= 0f)
                ApplyInitialBoardLayout(card, cell);
            else
            {
                float rootW0 = ResolveStageWidth();
                Vector2 startLocal = ComputeCardEntranceStartLocal(settle, rootW0, rootW0 * 0.5f);
                card.SetXY(startLocal.x, startLocal.y);
            }

            _holder.AddChild(card);
            _cards[cell.Id] = card;

            ApplyCardFace(card, cell.Value);

            card.onClick.Add(_clickHandler);
        }

        BindRevertButton(gameViewRoot);

        bool playEntrance = enableCardBoardEntrance &&
                            _cardEntranceMoveDuration > 0f &&
                            _cards.Count > 0;
        if (playEntrance)
            StartBoardEntranceAnimations(sortedCells);
        else
        {
            foreach (var kv in _cards)
            {
                if (kv.Value == null || kv.Value.isDisposed)
                    continue;
                var bc = _model.GetCell(kv.Key);
                if (!bc.Eliminated)
                    ApplyInitialBoardLayout(kv.Value, bc);
            }
            _cardEntranceActive = false;
            RefreshAllCards();
            RefreshRevertButtonState();
            InvokeBoardEntranceCompleteCallback();
        }

        var tip = gameViewRoot.GetChild("txt_tip")?.asTextField;
        if (tip != null)
        {
            if (_model.MatchBarCapacity > 0)
                tip.text = $"收纳栏最多 {_model.MatchBarCapacity} 张：点击移入，栏尾相邻相同则抵消；栏满再点失败。";
            else
                tip.text = "翻开两张相同数字即可消除（上层未消尽时下层锁住）";
        }
    }

    /// <summary>查找一对当面可消的相同牌并在二者上循环播放 <c>help</c> 动效。</summary>
    public void StartHintPairPulse()
    {
        if (_model == null || _boardLocked || _cardEntranceActive)
            return;

        StopHintPulseInternal();

        if (!PairMatchRules.TryFindClickableMatchingPair(_model, out int a, out int b))
        {
            Debug.LogWarning("[LayeredMatchBoardBinder] 当前无可提示的消除对。");
            return;
        }

        _hintIdA = a;
        _hintIdB = b;
        StartHelpTransitionLoop(GetCard(a));
        StartHelpTransitionLoop(GetCard(b));
    }

    /// <summary>停止 help 提示动效。</summary>
    public void StopHintPairPulse()
    {
        StopHintPulseInternal();
    }

    void StopHintPulseInternal()
    {
        if (_hintIdA.HasValue)
            StopHelpTransition(GetCard(_hintIdA.Value));
        if (_hintIdB.HasValue)
            StopHelpTransition(GetCard(_hintIdB.Value));
        _hintIdA = null;
        _hintIdB = null;
    }

    static void StartHelpTransitionLoop(GComponent card)
    {
        if (card == null || card.isDisposed)
            return;

        Transition tr = card.GetTransition(TrHelp);
        if (tr == null)
        {
            Debug.LogWarning($"[LayeredMatchBoardBinder] card 上未定义「{TrHelp}」动效（FairyGUI 编辑器中为 card 添加 Transition）。");
            return;
        }

        tr.Play(-1, 0, null);
    }

    static void StopHelpTransition(GComponent card)
    {
        if (card == null || card.isDisposed)
            return;

        Transition tr = card.GetTransition(TrHelp);
        if (tr != null && tr.playing)
            tr.Stop();
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

    void ApplyInitialBoardLayout(GComponent card, LayeredGridCell cell)
    {
        Vector2 p = ComputeBoardSlotLocal(cell);
        card.SetXY(p.x, p.y);
    }

    Vector2 ComputeBoardSlotLocal(LayeredGridCell cell)
    {
        if (cell.UseDesignerCoordinates)
            return new Vector2(cell.DisplayX, cell.DisplayY);
        float baseX = _layoutOx + cell.Col * _stepX;
        float baseY = _layoutOy + cell.Row * _stepY;
        if (cell.Layer > 0)
        {
            baseX += _layerShiftX * cell.Layer;
            baseY += _layerShiftY * cell.Layer;
        }
        return new Vector2(baseX, baseY);
    }

    Vector2 DockOriginLocalInHolder()
    {
        if (_matchBarDock != null && !_matchBarDock.isDisposed && _holder != null && !_holder.isDisposed)
            return _holder.GlobalToLocal(_matchBarDock.LocalToGlobal(Vector2.zero));
        return new Vector2(_layoutOx, _layoutOy + _gridH0 + Mathf.Max(_gap, 8f));
    }

    Vector2 GetBarSlotLocal(int slotIndex)
    {
        Vector2 dockOrigin = DockOriginLocalInHolder();
        float dockH = _matchBarDock != null && !_matchBarDock.isDisposed ? _matchBarDock.height : _cardH;
        float y = dockOrigin.y + Mathf.Max(0f, (dockH - _cardH) * 0.5f);
        float x = dockOrigin.x + slotIndex * (_cardW + _gap);
        return new Vector2(x, y);
    }

    static void KillCardMovementTween(GComponent card)
    {
        if (card != null && !card.isDisposed)
            GTween.Kill(card, TweenPropType.XY, false);
    }

    float ResolveStageWidth()
    {
        if (GRoot.inst != null && GRoot.inst.width > 1f)
            return GRoot.inst.width;
        if (_gameViewRoot != null && !_gameViewRoot.isDisposed && _gameViewRoot.width > 1f)
            return _gameViewRoot.width;
        return Mathf.Max(Screen.width / Mathf.Max(UIContentScaler.scaleFactor, 0.01f), 1f);
    }

    Vector2 ComputeCardEntranceStartLocal(Vector2 settleLocal, float rootWidth, float halfWidth)
    {
        Vector2 global = _holder.LocalToGlobal(settleLocal);
        float margin = Mathf.Max(_cardW * 1.15f, 40f);
        bool fromLeft = global.x < halfWidth;
        float gx = fromLeft ? -margin : rootWidth + margin;
        Vector2 globalStart = new Vector2(gx, global.y);
        return _holder.GlobalToLocal(globalStart);
    }

    static void ShuffleEntranceOrder(IList<LayeredGridCell> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    void StartBoardEntranceAnimations(List<LayeredGridCell> sortedStable)
    {
        var order = new List<LayeredGridCell>(sortedStable);
        ShuffleEntranceOrder(order);

        float rootW = ResolveStageWidth();
        float half = rootW * 0.5f;

        _cardEntranceActive = true;
        _cardEntranceTweensRemain = order.Count;

        int k = 0;
        foreach (var cell in order)
        {
            GComponent card = GetCard(cell.Id);
            if (card == null || card.isDisposed)
            {
                OnEntranceTweenDone();
                continue;
            }

            KillCardMovementTween(card);

            Vector2 settle = ComputeBoardSlotLocal(cell);
            Vector2 start = ComputeCardEntranceStartLocal(settle, rootW, half);
            card.SetXY(start.x, start.y);
            card.visible = true;
            card.touchable = false;
            card.grayed = true;
            card.sortingOrder = BoardCardSortingOrder;

            float jitter = Mathf.Max(_cardEntranceStagger * 0.85f, 0.018f);
            float delay = k * _cardEntranceStagger + Random.Range(0f, jitter);
            k++;

            Vector2 settleCopy = settle;
            card.TweenMove(settleCopy, _cardEntranceMoveDuration)
                .SetDelay(delay)
                .SetEase(EaseType.CubicOut)
                .OnComplete(() => OnEntranceTweenDone());
        }

        RefreshRevertButtonState();
    }

    void OnEntranceTweenDone()
    {
        if (!_cardEntranceActive)
            return;
        _cardEntranceTweensRemain--;
        if (_cardEntranceTweensRemain <= 0)
            FinishBoardEntranceAnimations();
    }

    void FinishBoardEntranceAnimations()
    {
        if (!_cardEntranceActive)
            return;

        foreach (var kv in _cards)
        {
            if (kv.Value == null || kv.Value.isDisposed)
                continue;
            var bc = _model?.GetCell(kv.Key);
            if (bc != null && !bc.Eliminated && !_flightCellIds.Contains(kv.Key))
                KillCardMovementTween(kv.Value);
        }

        _cardEntranceActive = false;
        RefreshAllCards();
        RefreshRevertButtonState();
        InvokeBoardEntranceCompleteCallback();
    }

    void InvokeBoardEntranceCompleteCallback()
    {
        System.Action cb = _onBoardEntranceComplete;
        _onBoardEntranceComplete = null;
        cb?.Invoke();
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
        if (_model == null || context.sender == null || BlockGameplayInput)
            return;

        if (!TryResolveCellId(context.sender as GObject, out int cellId))
        {
            RefreshAllCards();
            RefreshRevertButtonState();
            return;
        }

        if (_hintIdA.HasValue &&
            _hintIdB.HasValue &&
            cellId != _hintIdA.Value &&
            cellId != _hintIdB.Value)
            StopHintPulseInternal();

        int? selBefore = _model.SelectedCellId;

        GComponent moveCard = GetCard(cellId);
        Vector2 preLocal = moveCard != null && !moveCard.isDisposed ? moveCard.xy : Vector2.zero;

        var result = _model.TryClick(cellId);

        switch (result)
        {
            case LayeredMatchClickResult.Invalid:
                RefreshAllCards();
                RefreshRevertButtonState();
                break;

            case LayeredMatchClickResult.SelectedFirst:
                SafePlayTransition(GetCard(cellId), TrSelect, null);
                RefreshAllCards();
                RefreshRevertButtonState();
                break;

            case LayeredMatchClickResult.Deselected:
                GetCard(cellId)?.GetTransition(TrSelect)?.PlayReverse();
                RefreshAllCards();
                RefreshRevertButtonState();
                break;

            case LayeredMatchClickResult.MismatchedPair:
                StopHintPulseInternal();
                if (selBefore.HasValue)
                    PlayPairTransitionThenRefresh(selBefore.Value, cellId, TrFail);
                else
                {
                    RefreshAllCards();
                    RefreshRevertButtonState();
                }
                break;

            case LayeredMatchClickResult.MatchedPair:
                StopHintPulseInternal();
                if (selBefore.HasValue)
                    PlayPairTransitionThenRefresh(selBefore.Value, cellId, TrSucc, AfterMatchedRefresh);
                else
                {
                    RefreshAllCards();
                    RefreshRevertButtonState();
                }
                break;

            case LayeredMatchClickResult.MatchBarEnqueued:
                StopHintPulseInternal();
                if (ShouldTweenMatchBarEnqueue())
                    StartMatchBarEnqueueFly(cellId, preLocal);
                else
                {
                    RefreshAllCards();
                    RefreshRevertButtonState();
                    if (_model != null && _model.IsComplete)
                        OnBoardComplete();
                }
                break;

            case LayeredMatchClickResult.MatchBarMerged:
                StopHintPulseInternal();
                RefreshAllCards();
                RefreshRevertButtonState();
                if (_model != null && _model.IsComplete)
                    OnBoardComplete();
                break;

            case LayeredMatchClickResult.MatchBarFullGameOver:
                StopHintPulseInternal();
                RefreshAllCards();
                RefreshRevertButtonState();
                _onMatchBarOverflow?.Invoke();
                break;
        }
    }

    bool ShouldTweenMatchBarEnqueue() =>
        _model != null &&
        _model.MatchBarCapacity > 0 &&
        _matchBarDock != null &&
        !_matchBarDock.isDisposed &&
        _barFlyDuration > 0f;

    void StartMatchBarEnqueueFly(int cellId, Vector2 fromLocal)
    {
        var card = GetCard(cellId);
        if (card == null || card.isDisposed)
        {
            RefreshAllCards();
            RefreshRevertButtonState();
            if (_model != null && _model.IsComplete)
                OnBoardComplete();
            return;
        }

        _activeBarFlights++;
        _flightCellIds.Add(cellId);
        KillCardMovementTween(card);

        card.visible = true;
        card.SetXY(fromLocal.x, fromLocal.y);
        card.sortingOrder = BarCardSortingOrderBase + 200;
        card.touchable = false;
        card.grayed = false;

        int slot = _model.MatchBarCellIds.Count - 1;
        Vector2 to = GetBarSlotLocal(slot);

        RefreshAllCards();
        RefreshRevertButtonState();

        card.TweenMove(to, _barFlyDuration)
            .SetEase(EaseType.QuadOut)
            .OnComplete(() =>
            {
                _flightCellIds.Remove(cellId);
                _activeBarFlights = Mathf.Max(0, _activeBarFlights - 1);
                if (card.isDisposed)
                    return;
                RefreshAllCards();
                RefreshRevertButtonState();
                if (_model != null && _model.IsComplete)
                    OnBoardComplete();
            });
    }

    void BindRevertButton(GComponent root)
    {
        _btnRevert = root.GetChild("btn_revert")?.asButton;
        if (_btnRevert == null)
            return;
        _revertClickHandler ??= OnRevertClicked;
        _btnRevert.onClick.Add(_revertClickHandler);
    }

    void OnRevertClicked(EventContext _)
    {
        if (_model == null || _boardLocked || _cardEntranceActive)
            return;
        if (_model.MatchBarCapacity <= 0)
            return;
        IReadOnlyList<int> bar = _model.MatchBarCellIds;
        if (bar.Count == 0)
            return;

        int lastId = bar[bar.Count - 1];
        if (_flightCellIds.Contains(lastId))
        {
            KillCardMovementTween(GetCard(lastId));
            _flightCellIds.Remove(lastId);
            _activeBarFlights = Mathf.Max(0, _activeBarFlights - 1);
        }

        if (!_model.TryRevertLastMatchBarEntry())
            return;

        RefreshAllCards();
        RefreshRevertButtonState();
    }

    void RefreshRevertButtonState()
    {
        if (_btnRevert == null || _btnRevert.isDisposed || _model == null)
            return;
        bool barMode = _model.MatchBarCapacity > 0;
        _btnRevert.visible = barMode;
        if (!barMode)
            return;

        int n = _model.MatchBarCellIds.Count;
        bool can = n > 0 && !_boardLocked && !_cardEntranceActive;
        _btnRevert.grayed = n == 0;
        _btnRevert.touchable = can;
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

        if (_cardEntranceActive)
            return;

        bool barMode = _model.MatchBarCapacity > 0;
        IReadOnlyList<int> barIds = barMode ? _model.MatchBarCellIds : null;
        var barSlotOf = barMode ? new Dictionary<int, int>() : null;
        if (barMode && barIds != null)
        {
            for (int i = 0; i < barIds.Count; i++)
                barSlotOf[barIds[i]] = i;
        }

        foreach (var kv in _cards)
        {
            int id = kv.Key;
            var card = kv.Value;
            if (card == null || card.isDisposed)
                continue;

            var cell = _model.GetCell(id);

            if (barMode)
            {
                if (!cell.Eliminated)
                {
                    Vector2 bp = ComputeBoardSlotLocal(cell);
                    card.visible = true;
                    if (!_flightCellIds.Contains(id))
                        card.SetXY(bp.x, bp.y);
                    card.sortingOrder = BoardCardSortingOrder;

                    ApplyCardFace(card, cell.Value);

                    bool can = _model.CanClick(id) && !_boardLocked && _activeBarFlights <= 0;
                    card.grayed = !can;
                    card.touchable = can;

                    continue;
                }

                if (barSlotOf != null && barSlotOf.TryGetValue(id, out int slotIdx))
                {
                    card.visible = true;

                    ApplyCardFace(card, cell.Value);

                    if (!_flightCellIds.Contains(id))
                    {
                        Vector2 p = GetBarSlotLocal(slotIdx);
                        card.SetXY(p.x, p.y);
                        card.sortingOrder = BarCardSortingOrderBase + slotIdx;
                    }
                    else
                        card.sortingOrder = BarCardSortingOrderBase + 200 + slotIdx;

                    card.grayed = false;
                    card.touchable = false;
                    continue;
                }

                KillCardMovementTween(card);
                card.visible = false;
                continue;
            }

            // 经典模式
            card.sortingOrder = BoardCardSortingOrder;

            if (cell.Eliminated)
            {
                card.visible = false;
                continue;
            }

            card.visible = true;

            ApplyCardFace(card, cell.Value);

            bool canClassic = _model.CanClick(id);
            card.grayed = !canClassic;
            card.touchable = canClassic && !_boardLocked;
        }

        RefreshRevertButtonState();
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
        _onBoardEntranceComplete = null;
        _cardEntranceActive = false;

        StopHintPulseInternal();

        if (_btnRevert != null && _revertClickHandler != null)
            _btnRevert.onClick.Remove(_revertClickHandler);
        _btnRevert = null;
        _revertClickHandler = null;

        if (_cards.Count > 0 && _clickHandler != null)
        {
            foreach (var kv in _cards)
            {
                var card = kv.Value;
                if (card != null && !card.isDisposed)
                {
                    KillCardMovementTween(card);
                    card.onClick.Remove(_clickHandler);
                    StopTransitionIfPlaying(card, TrSelect);
                    StopTransitionIfPlaying(card, TrSucc);
                    StopTransitionIfPlaying(card, TrFail);
                    StopTransitionIfPlaying(card, TrHelp);
                }
            }
        }

        _flightCellIds.Clear();
        _activeBarFlights = 0;

        _cards.Clear();
        _clickHandler = null;
        _holder = null;
        _gameViewRoot = null;
        _matchBarDock = null;
        _model = null;
        _onBoardComplete = null;
        _onMatchBarOverflow = null;
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
