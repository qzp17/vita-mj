using System;
using System.Collections.Generic;
using System.Globalization;
using FairyGUI;
using UnityEngine;
using VitaMj.Config;
using VitaMj.MatchGame;
using VitaMj.Persistence;

using ConfigLevelRow = VitaMj.Config.LevelConfigRow;

/// <summary>
/// 全局 FairyGUI 界面逻辑：主界面由场景里的 UIPanel（main_view）承担；
/// 选关与全屏 View（level_view / game_view）挂在 <see cref="GRoot"/> 上。
/// <para>主界面根节点由 <see cref="MainUIView"/> 包装；选关 / 对局由 <see cref="LevelUIView"/>、<see cref="GameUIView"/> 包装，
/// 均继承 <see cref="FairyUIViewBase"/> 并维护弹窗栈。弹窗由 <see cref="FairyUIPopupBase"/> 体系表示。</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class GameUIManager : MonoBehaviour
{
    enum LevelFinishKind
    {
        Win,
        TimeUp,
        QueueFull,
    }

    public static GameUIManager Instance { get; private set; }

    [Tooltip("与 UIPanel.packagePath 一致，用于运行时 EnsurePackage")]
    [SerializeField]
    string packagePath = "res/Package1";

    [SerializeField]
    string packageName = "Package1";

    [SerializeField]
    string levelViewComponentName = "level_view";

    [SerializeField]
    string gameViewComponentName = "game_view";

    [SerializeField]
    string finishPopupComponentName = "finish_popup";

    [SerializeField]
    string settingPopupComponentName = "setting_popup";

    [Tooltip("finish_popup 内返回选关的按钮名；留空则依次尝试 btn_back、btn_confirm、btn_ok、n0")]
    [SerializeField]
    string finishPopupBackButtonName = "";

    [Tooltip("finish_popup「再玩一局」按钮名；留空则尝试 btn_retry、btn_again、retry、replay 等")]
    [SerializeField]
    string finishPopupRetryButtonName = "";

    [Tooltip("finish_popup「下一关」按钮名；留空则尝试 btn_next、next、btn_continue 等")]
    [SerializeField]
    string finishPopupNextButtonName = "";

    [Tooltip("finish_popup 上可选控制器名：0=胜利，1=失败/超时（无此控制器可留空）")]
    [SerializeField]
    string finishPopupStatusControllerName = "status";

    [Tooltip("棋盘每张卡片对应的 FairyGUI 组件名（Package1 内）")]
    [SerializeField]
    string cardComponentName = "card";

    [SerializeField]
    float cardCellGap = 8f;

    [Tooltip("是否允许玩家在选关页的 level_view（优先）或对局页的 game_view（回退）选择「双点消除」或「收纳栏」；找不到 UI 则用 fallback")]
    [SerializeField]
    bool offerMatchPlayStyleChoice = true;

    [Tooltip("关卡未显示玩法选择或未配置按钮时使用")]
    [SerializeField]
    MatchPlayStyle fallbackMatchPlayStyle = MatchPlayStyle.ClassicPairClick;

    [Tooltip("level_view 下玩法切换面板：存在且含两个玩法按钮时在选关页切换，开局不再盖住棋盘")]
    [SerializeField]
    string levelViewPlayPickPanelName = "play_mode_pick";

    [Tooltip("game_view 下用于放置两个选项按钮的组件名（无有效 level_view 玩法面板时使用）；留空则不使用内嵌面板")]
    [SerializeField]
    string gameViewPlayPickPanelName = "play_mode_pick";

    [Tooltip("game_view 无内嵌面板时尝试从 FairyGUI 包创建该组件名作为弹窗；留空跳过")]
    [SerializeField]
    string playStylePickFallbackPopupName = "";

    [Tooltip("Classic 玩法按钮自定义名优先；为空则依次尝试预设名")]
    [SerializeField]
    string pickClassicPlayButtonPreferredName = "";

    [Tooltip("收纳栏玩法按钮自定义名优先；为空则依次尝试预设名")]
    [SerializeField]
    string pickQueuePlayButtonPreferredName = "";

    [Tooltip("程序化棋盘 + 收纳栏模式时的栏位长度（关卡 JSON 中 queueMaxSlots 仍优先生效）")]
    [SerializeField]
    [Min(2)]
    int proceduralQueueMaxSlotsFallback = ConfiguredPairMatchGame.DefaultQueueSlotsWhenConfigZero;

    [Tooltip("收纳栏首张牌左上角与 game_view 该子面板对齐（与棋盘同在 card_holder 坐标系下折算）；留空则排在棋盘下方")]
    [SerializeField]
    string matchBarDockPanelName = "match_bar_panel";

    [SerializeField]
    [Min(0.05f)]
    float matchBarFlyDurationSeconds = 0.35f;

    [Tooltip("开局卡牌从屏幕左右水平飞入棋盘，错落启动")]
    [SerializeField]
    bool enableCardBoardEntrance = true;

    [SerializeField]
    [Min(0.05f)]
    float cardEntranceMoveSeconds = 0.42f;

    [SerializeField]
    [Min(0f)]
    float cardEntranceStaggerSeconds = 0.055f;

    [Tooltip("选关列表：ConfigReader 表名，与 Assets/Config 下 xlsx 文件名一致（不含扩展名），如 Level → Resources/ExportConfig/Level.bytes")]
    [SerializeField]
    string levelConfigTableKey = "Level";

    [Tooltip("留空则打开游戏界面时使用默认 4×5×2 程序化棋盘")]
    [SerializeField]
    MatchLevelDefinition matchLevel;

    [Tooltip("主界面 UIPanel（main_view）；为空则在同物体上 GetComponent<UIPanel>")]
    [SerializeField]
    UIPanel mainUIPanel;

    [SerializeField]
    bool playStartupBackgroundMusic = true;

    [SerializeField]
    string startupBackgroundMusicTag = "main_bgm";

    [Tooltip("选关条目组件上控制器名：0=open，1=finish，2=lock")]
    [SerializeField]
    string levelCellStateControllerName = "state";

    MainUIView _mainUIView;
    LevelUIView _levelUIView;
    GameUIView _gameUIView;
    LayeredMatchBoardBinder _boardBinder;

    readonly List<(GButton Button, EventCallback1 Handler)> _playPickHandlerRegs = new List<(GButton Button, EventCallback1 Handler)>();

    readonly List<(GButton Button, EventCallback1 Handler)> _levelPlayPickHandlerRegs =
        new List<(GButton Button, EventCallback1 Handler)>();

    bool _levelViewPlayPickReady;
    MatchPlayStyle _matchPlayStyleForNextBoard;

    GObject _floatingPlayPickRoot;

    /// <summary>由选关 JSON 生成的临时关卡，关闭游戏时需 Destroy。</summary>
    MatchLevelDefinition _ownedMatchLevelInstance;

    EventCallback1 _levelListClickHandler;
    EventCallback1 _finishPopupBackClickHandler;
    EventCallback1 _gameHelpClickHandler;
    EventCallback1 _gameQuitClickHandler;
    EventCallback1 _finishRetryClickHandler;
    EventCallback1 _finishNextClickHandler;
    EventCallback1 _settingPopupCloseClickHandler;

    bool _gameEnded;

    bool _levelCountdownActive;
    float _countdownEndsAtUnscaled;
    int _lastRenderedCountdownSec = -1;
    int? _effectiveTimeLimitSeconds;
    int? _replayTimeLimitSeconds;
    string _replayLevelContentJson;
    string _replayLevelTagDisplay;
    MatchLevelDefinition _replayScriptableLevel;

    /// <summary>true 表示当前对局为程序化默认棋盘，可再开一局同规则。</summary>
    bool _replayProceduralDefault;

    bool _finishHadTimeLimit;
    int? _finishRemainSecondsSnapshot;

    /// <summary>当前 Level 表中行的顺序（与导出 Excel 行序一致），列表项索引与之对应。</summary>
    readonly System.Collections.Generic.List<string> _levelRowTagsOrdered = new System.Collections.Generic.List<string>();

    /// <summary>从选关列表进入对局时为列表下标；程序化/无主线路径为 -1。</summary>
    int _campaignLevelListIndex = -1;

    public bool IsLevelViewOpen => _levelUIView != null && !_levelUIView.Root.isDisposed;

    public bool IsGameViewOpen => _gameUIView != null && !_gameUIView.Root.isDisposed;

    void Awake()
    {
        Instance = this;
        _levelListClickHandler = OnLevelListItemClick;
        _finishPopupBackClickHandler = OnFinishPopupBackClicked;
        _gameHelpClickHandler = OnGameHelpClicked;
        _gameQuitClickHandler = OnGameQuitClicked;
        _finishRetryClickHandler = OnFinishRetryClicked;
        _finishNextClickHandler = OnFinishNextClicked;
        _settingPopupCloseClickHandler = OnSettingPopupCloseClicked;
        if (mainUIPanel == null)
            mainUIPanel = GetComponent<UIPanel>();
        EnsureMainUIView();
    }

    void Start()
    {
        if (playStartupBackgroundMusic &&
            !string.IsNullOrEmpty(startupBackgroundMusicTag))
            AudioManager.Instance.PlayMusicByTag(startupBackgroundMusicTag, true);

        AudioManager.Instance.RefreshVolumesFromPersistedSettings();
    }

    /// <summary>
    /// 在主界面弹出设置（依赖 Package1/<see cref="settingPopupComponentName"/>）。
    /// </summary>
    public void OpenSettingPopup()
    {
        EnsureMainUIView();
        if (_mainUIView == null || _mainUIView.Root.isDisposed)
        {
            Debug.LogError("[GameUIManager] 主界面为空，无法打开设置。");
            return;
        }

        EnsurePackageLoaded();

        GObject obj = UIPackage.CreateObject(packageName, settingPopupComponentName);
        var root = obj as GComponent;
        if (root == null)
        {
            Debug.LogError($"[GameUIManager] 创建失败：{packageName}/{settingPopupComponentName} 不存在或不是组件。");
            obj?.Dispose();
            return;
        }

        BindSettingPopupComponents(root);

        var host = new FairyUIPopupHost(root);
        _mainUIView.PushPopup(host);
    }

    void BindSettingPopupComponents(GComponent root)
    {
        GButton close = root.GetChild("btn_close")?.asButton;
        close?.onClick.Add(_settingPopupCloseClickHandler);

        GButton audioBtn = root.GetChild("btn_audio")?.asButton;
        GSlider sliderSound = root.GetChild("slider_sound")?.asSlider;

        bool musicOn = AudioSettingsStore.GetMusicEnabled();
        if (audioBtn != null)
        {
            audioBtn.selected = musicOn;
            audioBtn.onChanged.Add(OnSettingMusicToggleChanged);
        }

        if (sliderSound != null)
        {
            SetSliderNormalized(sliderSound, AudioSettingsStore.GetMasterVolume01());
            sliderSound.onChanged.Add(OnSettingVolumeSliderChanged);
        }
    }

    void OnSettingPopupCloseClicked(EventContext _) =>
        _mainUIView?.CloseTopPopup();

    void OnSettingMusicToggleChanged(EventContext ctx)
    {
        if (ctx.sender is not GButton btn)
            return;
        AudioSettingsStore.SetMusicEnabled(btn.selected);
        AudioManager.Instance.RefreshVolumesFromPersistedSettings();
    }

    void OnSettingVolumeSliderChanged(EventContext ctx)
    {
        if (ctx.sender is not GSlider sl)
            return;
        AudioSettingsStore.SetMasterVolume01(SliderToNormalized(sl));
        AudioManager.Instance.RefreshVolumesFromPersistedSettings();
    }

    static float SliderToNormalized(GSlider sl)
    {
        double denom = sl.max - sl.min;
        if (denom <= 0.0001d)
            return 1f;
        return Mathf.Clamp01((float)((sl.value - sl.min) / denom));
    }

    static void SetSliderNormalized(GSlider sl, float n01)
    {
        n01 = Mathf.Clamp01(n01);
        sl.value = sl.min + (sl.max - sl.min) * n01;
    }

    /// <summary>
    /// 使用 <see cref="MainUIView"/> 包装 UIPanel 根节点；面板尚未生成时返回 null。
    /// </summary>
    public MainUIView EnsureMainUIView()
    {
        if (_mainUIView != null && !_mainUIView.Root.isDisposed)
            return _mainUIView;

        GComponent ui = mainUIPanel != null ? mainUIPanel.ui : null;
        if (ui == null)
            return null;

        _mainUIView = new MainUIView(ui);
        return _mainUIView;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        _mainUIView?.ClearPopups();
        _mainUIView = null;

        CloseLevelView();
        CloseGameView();
    }

    void EnsurePackageLoaded()
    {
        if (UIPackage.GetByName(packageName) != null)
            return;
        if (string.IsNullOrEmpty(packagePath))
        {
            Debug.LogError("[GameUIManager] packagePath 为空。");
            return;
        }

        UIPackage.AddPackage(packagePath);
    }

    /// <summary>
    /// 打开选关界面：根据 <see cref="ConfigReader"/> 中 Level 表填充名为 level 的 GList；若无表或为空则直接进入默认游戏。
    /// </summary>
    public void OpenLevelView()
    {
        EnsurePackageLoaded();

        if (!RefreshLevelRowTags())
        {
            Debug.LogWarning("[GameUIManager] Level 配置表不可用或无数据（请先导出 Assets/Config/*.xlsx），跳过选关界面。");
            OpenGameView(null, false);
            return;
        }

        if (_levelUIView != null && !_levelUIView.Root.isDisposed)
        {
            RefreshLevelList();
            _levelUIView.Root.visible = true;
            GRoot.inst.SetChildIndex(_levelUIView.Root, GRoot.inst.numChildren - 1);
            RefreshUnderlayVisibility();
            return;
        }

        GObject obj = UIPackage.CreateObject(packageName, levelViewComponentName);
        var com = obj as GComponent;
        if (com == null)
        {
            Debug.LogError($"[GameUIManager] 创建失败：{packageName}/{levelViewComponentName} 不存在或不是组件。");
            obj?.Dispose();
            return;
        }

        _levelUIView = new LevelUIView(com);
        GRoot.inst.AddChild(_levelUIView.Root);
        _levelUIView.Root.SetXY(0, 0);
        _levelUIView.Root.MakeFullScreen();
        _levelUIView.Root.AddRelation(GRoot.inst, RelationType.Size);

        BindLevelList(_levelUIView);
        BindLevelPlayPickIfPresent(_levelUIView);
        RefreshUnderlayVisibility();
    }

    bool RefreshLevelRowTags()
    {
        _levelRowTagsOrdered.Clear();
        if (string.IsNullOrEmpty(levelConfigTableKey))
            return false;
        if (!ConfigReader.TryGetOrderedTags(levelConfigTableKey, out var tags))
            return false;
        _levelRowTagsOrdered.AddRange(tags);
        return _levelRowTagsOrdered.Count > 0;
    }

    void BindLevelList(LevelUIView view)
    {
        GList list = view.LevelList;
        if (list == null)
        {
            Debug.LogError("[GameUIManager] level_view 下未找到名为 \"level\" 的 GList。");
            return;
        }

        list.RemoveChildrenToPool();
        list.itemRenderer = RenderLevelListCell;
        list.onClickItem.Add(_levelListClickHandler);
        list.numItems = _levelRowTagsOrdered.Count;
        RefreshLevelCellsVisuals(list);
    }

    void BindLevelPlayPickIfPresent(LevelUIView view)
    {
        UnbindLevelPlayPickHandlers();
        _levelViewPlayPickReady = false;

        if (view == null || view.Root.isDisposed)
            return;

        if (!string.IsNullOrEmpty(levelViewPlayPickPanelName))
        {
            GComponent panel = view.Root.GetChild(levelViewPlayPickPanelName)?.asCom;
            if (panel != null && !offerMatchPlayStyleChoice)
                panel.visible = false;
        }

        if (!offerMatchPlayStyleChoice)
            return;

        if (string.IsNullOrEmpty(levelViewPlayPickPanelName))
            return;

        GComponent anchor = view.Root.GetChild(levelViewPlayPickPanelName)?.asCom;
        if (anchor == null)
            return;

        GButton classic = ResolvePlayPickButton(
            anchor,
            pickClassicPlayButtonPreferredName,
            new[] { "btn_classic", "btn_double", "btn_pair", "mode_classic", "btn_mode_classic" });
        GButton queue = ResolvePlayPickButton(
            anchor,
            pickQueuePlayButtonPreferredName,
            new[] { "btn_queue", "btn_bar", "btn_matchbar", "mode_queue", "btn_mode_queue" });

        if (classic == null || queue == null)
        {
            Debug.LogWarning("[GameUIManager] level_view 玩法选择缺少按钮，将尝试 game_view 内嵌面板或默认玩法。");
            anchor.visible = false;
            return;
        }

        anchor.visible = true;
        _matchPlayStyleForNextBoard = fallbackMatchPlayStyle;

        RegisterLevelPlayPickClick(classic, _ => SetLevelViewPlayPickStyle(MatchPlayStyle.ClassicPairClick));
        RegisterLevelPlayPickClick(queue, _ => SetLevelViewPlayPickStyle(MatchPlayStyle.MatchBarQueue));
        _levelViewPlayPickReady = true;
    }

    void SetLevelViewPlayPickStyle(MatchPlayStyle style)
    {
        _matchPlayStyleForNextBoard = style;
    }

    void RegisterLevelPlayPickClick(GButton btn, EventCallback1 handler)
    {
        btn.onClick.Add(handler);
        _levelPlayPickHandlerRegs.Add((btn, handler));
    }

    void UnbindLevelPlayPickHandlers()
    {
        for (int i = 0; i < _levelPlayPickHandlerRegs.Count; i++)
        {
            (GButton button, EventCallback1 handler) = _levelPlayPickHandlerRegs[i];
            button?.onClick.Remove(handler);
        }

        _levelPlayPickHandlerRegs.Clear();
    }

    /// <summary>非虚拟列表在 <see cref="GList.numItems"/> 不变时不会重绑 itemRenderer，通关后须显式刷新。</summary>
    void RefreshLevelCellsVisuals(GList list)
    {
        if (list == null || list.itemRenderer == null)
            return;

        if (list.isVirtual)
        {
            list.RefreshVirtualList();
            return;
        }

        int n = _levelRowTagsOrdered.Count;
        int count = Mathf.Min(n, list.numChildren);
        for (int i = 0; i < count; i++)
            list.itemRenderer(i, list.GetChildAt(i));
    }

    void RefreshLevelList()
    {
        if (_levelUIView == null || _levelUIView.Root.isDisposed)
            return;
        if (!RefreshLevelRowTags())
            return;
        GList list = _levelUIView.LevelList;
        if (list == null)
            return;
        list.numItems = _levelRowTagsOrdered.Count;
        RefreshLevelCellsVisuals(list);
    }

    void RenderLevelListCell(int index, GObject obj)
    {
        if (index < 0 || index >= _levelRowTagsOrdered.Count)
            return;

        string tagKey = _levelRowTagsOrdered[index];
        if (!ConfigReader.TryGetRow<ConfigLevelRow>(levelConfigTableKey, tagKey, out ConfigLevelRow cfg))
            return;

        obj.data = tagKey;

        GComponent item = obj.asCom;
        if (item == null)
            return;

        LevelCellVisualState cellState = LevelProgressStore.GetCellVisualState(index);
        Controller st = item.GetController(levelCellStateControllerName);
        if (st != null)
            st.selectedIndex = (int)cellState;
        item.touchable = cellState != LevelCellVisualState.Lock;

        GTextField lv = item.GetChild("lv")?.asTextField;
        if (lv != null)
            lv.text = $"{cfg.tag} · {cfg.level}";
    }

    void OnLevelListItemClick(EventContext context)
    {
        if (!(context.data is GObject item) || !(item.data is string tagKey))
            return;

        int listIndex = _levelRowTagsOrdered.IndexOf(tagKey);
        if (listIndex < 0)
            return;

        _ = TryStartCampaignLevelAtListIndex(listIndex, logFailures: true);
    }

    /// <summary>
    /// 按选关列表下标载入 JSON 关卡并进入对局（与列表点击相同校验）。
    /// </summary>
    /// <returns>是否已成功调用 <see cref="OpenGameView"/>。</returns>
    bool TryStartCampaignLevelAtListIndex(int listIndex, bool logFailures)
    {
        if (listIndex < 0 || listIndex >= _levelRowTagsOrdered.Count)
            return false;

        if (LevelProgressStore.GetCellVisualState(listIndex) == LevelCellVisualState.Lock)
            return false;

        string tagKey = _levelRowTagsOrdered[listIndex];
        if (!ConfigReader.TryGetRow<ConfigLevelRow>(levelConfigTableKey, tagKey, out ConfigLevelRow cfg))
            return false;

        if (string.IsNullOrWhiteSpace(cfg.content))
        {
            if (logFailures)
                Debug.LogError($"[GameUIManager] 关卡 content 为空（tag={tagKey}）。");
            return false;
        }

        if (!TryParseLevelCell(cfg.level, out int levelNum))
        {
            if (logFailures)
                Debug.LogError($"[GameUIManager] 无法解析 level 列：「{cfg.level}」（tag={tagKey}）");
            return false;
        }

        MatchLevelDefinition def = null;
        try
        {
            def = MatchLevelDefinitionFromJson.Create(cfg.content);
            _ = new ConfiguredPairMatchGame(def, MatchPlayStyle.ClassicPairClick);
        }
        catch (Exception ex)
        {
            if (def != null)
                Destroy(def);

            if (logFailures)
                Debug.LogError($"[GameUIManager] 关卡加载失败（{cfg.tag}/{levelNum}）：{ex.Message}");
            return false;
        }

        int jsonSec = def.timeLimitSeconds > 0 ? def.timeLimitSeconds : 0;
        _effectiveTimeLimitSeconds = jsonSec > 0 ? jsonSec : (int?)null;
        _replayTimeLimitSeconds = _effectiveTimeLimitSeconds;
        _replayLevelContentJson = cfg.content;
        _replayLevelTagDisplay = cfg.tag;

        _campaignLevelListIndex = listIndex;
        HideLevelViewForOverlay();
        OpenGameView(def, true);
        return true;
    }

    bool CanGoToNextCampaignLevel()
    {
        if (_campaignLevelListIndex < 0)
            return false;
        int next = _campaignLevelListIndex + 1;
        if (next >= _levelRowTagsOrdered.Count)
            return false;
        return LevelProgressStore.GetCellVisualState(next) != LevelCellVisualState.Lock;
    }

    void OnFinishNextClicked(EventContext _)
    {
        if (_gameUIView == null || _gameUIView.Root.isDisposed || !CanGoToNextCampaignLevel())
            return;

        int nextIndex = _campaignLevelListIndex + 1;
        _gameUIView.ClearPopups();

        if (!TryStartCampaignLevelAtListIndex(nextIndex, logFailures: true))
            Debug.LogWarning($"[GameUIManager] 无法进入下一关（index={nextIndex}）。");
    }

    static bool TryParseLevelCell(string levelStr, out int level)
    {
        level = 0;
        if (string.IsNullOrWhiteSpace(levelStr))
            return false;

        if (int.TryParse(levelStr.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out level))
            return true;

        if (double.TryParse(levelStr.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
        {
            level = (int)Math.Round(d);
            return true;
        }

        return false;
    }

    /// <summary>进入游戏时保留选关界面实例，仅隐藏，便于关闭游戏后回到选关。</summary>
    void HideLevelViewForOverlay()
    {
        if (_levelUIView != null && !_levelUIView.Root.isDisposed)
            _levelUIView.Root.visible = false;
    }

    void BindGameToolbarIfPresent()
    {
        if (_gameUIView == null || _gameUIView.Root.isDisposed)
            return;

        UnbindGameToolbarIfPresent();

        GButton help = _gameUIView.BtnHelp;
        if (help != null)
            help.onClick.Add(_gameHelpClickHandler);

        GButton quit = _gameUIView.BtnQuit;
        if (quit != null)
            quit.onClick.Add(_gameQuitClickHandler);
    }

    void UnbindGameToolbarIfPresent()
    {
        if (_gameUIView == null || _gameUIView.Root.isDisposed)
            return;

        GButton help = _gameUIView.BtnHelp;
        if (help != null)
            help.onClick.Remove(_gameHelpClickHandler);

        GButton quit = _gameUIView.BtnQuit;
        if (quit != null)
            quit.onClick.Remove(_gameQuitClickHandler);
    }

    void OnGameHelpClicked(EventContext _)
    {
        _boardBinder?.StartHintPairPulse();
    }

    void OnGameQuitClicked(EventContext _)
    {
        CloseGameView();
        if (_levelUIView == null || _levelUIView.Root.isDisposed)
            OpenLevelView();
    }

    /// <summary>是否存在遮住主界面的全屏层（选关可见，或游戏界面存在）。</summary>
    bool HasFullscreenOverlay()
    {
        if (_gameUIView != null && !_gameUIView.Root.isDisposed)
            return true;
        if (_levelUIView != null && !_levelUIView.Root.isDisposed && _levelUIView.Root.visible)
            return true;
        return false;
    }

    void RefreshUnderlayVisibility()
    {
        EnsureMainUIView();
        GComponent mainRoot = _mainUIView != null ? _mainUIView.Root : mainUIPanel?.ui;
        if (mainRoot != null && !mainRoot.isDisposed)
            mainRoot.visible = !HasFullscreenOverlay();
    }

    void BringLevelViewToFrontOnGRoot()
    {
        if (_levelUIView == null || _levelUIView.Root.isDisposed)
            return;
        _levelUIView.Root.visible = true;
        GRoot.inst.SetChildIndex(_levelUIView.Root, GRoot.inst.numChildren - 1);
    }

    /// <summary>
    /// 关闭选关界面。
    /// </summary>
    public void CloseLevelView()
    {
        if (_levelUIView == null || _levelUIView.Root.isDisposed)
        {
            _levelUIView = null;
            return;
        }

        _levelUIView.ClearPopups();

        GList list = _levelUIView.LevelList;
        if (list != null)
        {
            list.onClickItem.Remove(_levelListClickHandler);
            list.itemRenderer = null;
            list.numItems = 0;
        }

        UnbindLevelPlayPickHandlers();
        _levelViewPlayPickReady = false;

        _levelUIView.Root.Dispose();
        _levelUIView = null;
        RefreshUnderlayVisibility();
    }

    void DisposeOwnedMatchLevel()
    {
        if (_ownedMatchLevelInstance == null)
            return;
        Destroy(_ownedMatchLevelInstance);
        _ownedMatchLevelInstance = null;
    }

    /// <summary>
    /// 在 GRoot 上创建并显示游戏界面。
    /// </summary>
    /// <param name="levelOverride">非空则使用该关卡；为空则用 Inspector 中的 <see cref="matchLevel"/>（仍可为空走程序化棋盘）。</param>
    /// <param name="destroyLevelWhenClosed">为 true 时须在关闭游戏时 Destroy（适用于 JSON 生成的运行时实例）；勿对资源资产设为 true。</param>
    public void OpenGameView(MatchLevelDefinition levelOverride = null, bool destroyLevelWhenClosed = false)
    {
        _replayScriptableLevel = null;
        _replayProceduralDefault = false;

        if (!destroyLevelWhenClosed || levelOverride == null)
        {
            _campaignLevelListIndex = -1;
            _replayLevelContentJson = null;
            _replayLevelTagDisplay = null;
            _replayTimeLimitSeconds = null;
            _effectiveTimeLimitSeconds = null;
        }

        EnsurePackageLoaded();

        MatchLevelDefinition def = levelOverride != null ? levelOverride : matchLevel;

        if (def != null &&
            def.timeLimitSeconds > 0 &&
            (!_effectiveTimeLimitSeconds.HasValue || _effectiveTimeLimitSeconds.Value <= 0))
            _effectiveTimeLimitSeconds = def.timeLimitSeconds;

        // 结算「再玩一局」：JSON 关卡在 OnLevelListItemClick 已写入 *_replay*
        if (string.IsNullOrEmpty(_replayLevelContentJson) &&
            def != null &&
            matchLevel != null &&
            ReferenceEquals(def, matchLevel))
        {
            _replayScriptableLevel = matchLevel;
            if (def.timeLimitSeconds > 0)
                _replayTimeLimitSeconds = def.timeLimitSeconds;
        }
        else if (string.IsNullOrEmpty(_replayLevelContentJson) && def == null)
            _replayProceduralDefault = true;

        if (_gameUIView != null && !_gameUIView.Root.isDisposed)
        {
            DisposeOwnedMatchLevel();
            TearDownBoard();
            ResetPlayPickUiState();

            _gameUIView.ClearPopups();

            _ownedMatchLevelInstance = destroyLevelWhenClosed ? def : null;

            BeginPlayStyleChoiceOrBindBoard(def);
            GRoot.inst.SetChildIndex(_gameUIView.Root, GRoot.inst.numChildren - 1);
            RefreshUnderlayVisibility();
            return;
        }

        TearDownBoard();
        ResetPlayPickUiState();
        DisposeOwnedMatchLevel();
        _ownedMatchLevelInstance = destroyLevelWhenClosed ? def : null;

        GObject obj = UIPackage.CreateObject(packageName, gameViewComponentName);
        var com = obj as GComponent;
        if (com == null)
        {
            Debug.LogError($"[GameUIManager] 创建失败：{packageName}/{gameViewComponentName} 不存在或不是组件。");
            obj?.Dispose();
            DisposeOwnedMatchLevel();
            ClearGameSessionSchedulingState(resetCampaignToo: true);
            BringLevelViewToFrontOnGRoot();
            RefreshUnderlayVisibility();
            return;
        }

        _gameUIView = new GameUIView(com);
        GRoot.inst.AddChild(_gameUIView.Root);
        _gameUIView.Root.SetXY(0, 0);
        _gameUIView.Root.MakeFullScreen();
        _gameUIView.Root.AddRelation(GRoot.inst, RelationType.Size);

        BeginPlayStyleChoiceOrBindBoard(def);
        RefreshUnderlayVisibility();
    }

    void BeginPlayStyleChoiceOrBindBoard(MatchLevelDefinition def)
    {
        if (_gameUIView == null || _gameUIView.Root.isDisposed)
            return;

        if (!offerMatchPlayStyleChoice)
        {
            CompleteMatchBoardSetup(def, fallbackMatchPlayStyle);
            return;
        }

        if (_levelViewPlayPickReady)
        {
            CompleteMatchBoardSetup(def, _matchPlayStyleForNextBoard);
            return;
        }

        if (!TryAttachPlayStylePick(def))
            CompleteMatchBoardSetup(def, fallbackMatchPlayStyle);
    }

    void CompleteMatchBoardSetup(MatchLevelDefinition def, MatchPlayStyle style)
    {
        if (_gameUIView == null || _gameUIView.Root.isDisposed)
            return;

        TearDownBoard();
        _boardBinder = new LayeredMatchBoardBinder();
        _boardBinder.Bind(
            _gameUIView.Root,
            packageName,
            cardComponentName,
            cardCellGap,
            def,
            OnMatchBoardCompleted,
            OnMatchBarOverflow,
            style,
            proceduralQueueMaxSlotsFallback,
            matchBarDockPanelName,
            matchBarFlyDurationSeconds,
            enableCardBoardEntrance,
            cardEntranceMoveSeconds,
            cardEntranceStaggerSeconds,
            StartLevelSessionAfterBind);
        BindGameToolbarIfPresent();
    }

    void ResetPlayPickUiState()
    {
        UnregisterPlayPickHandlers();
        DisposeFloatingPlayPick();
        if (_gameUIView == null || _gameUIView.Root.isDisposed)
            return;
        if (!string.IsNullOrEmpty(gameViewPlayPickPanelName))
        {
            GComponent embedded = _gameUIView.Root.GetChild(gameViewPlayPickPanelName)?.asCom;
            if (embedded != null)
                embedded.visible = false;
        }

        SetCardHolderVisible(true);
    }

    bool TryAttachPlayStylePick(MatchLevelDefinition def)
    {
        if (_gameUIView == null || _gameUIView.Root.isDisposed)
            return false;

        UnregisterPlayPickHandlers();
        DisposeFloatingPlayPick();

        GComponent anchor = TryResolvePlayPickAnchor();
        if (anchor == null)
            return false;

        GButton classic = ResolvePlayPickButton(
            anchor,
            pickClassicPlayButtonPreferredName,
            new[] { "btn_classic", "btn_double", "btn_pair", "mode_classic", "btn_mode_classic" });
        GButton queue = ResolvePlayPickButton(
            anchor,
            pickQueuePlayButtonPreferredName,
            new[] { "btn_queue", "btn_bar", "btn_matchbar", "mode_queue", "btn_mode_queue" });

        if (classic == null || queue == null)
        {
            Debug.LogWarning("[GameUIManager] 玩法选择 UI 缺少按钮，已退回默认玩法。");
            anchor.visible = false;
            SetCardHolderVisible(true);
            DisposeFloatingPlayPick();
            return false;
        }

        SetCardHolderVisible(false);
        anchor.visible = true;

        void Pick(MatchPlayStyle style)
        {
            UnregisterPlayPickHandlers();
            anchor.visible = false;
            SetCardHolderVisible(true);
            DisposeFloatingPlayPick();
            CompleteMatchBoardSetup(def, style);
        }

        RegisterPlayPickClick(classic, _ => Pick(MatchPlayStyle.ClassicPairClick));
        RegisterPlayPickClick(queue, _ => Pick(MatchPlayStyle.MatchBarQueue));
        return true;
    }

    GComponent TryResolvePlayPickAnchor()
    {
        if (!string.IsNullOrEmpty(gameViewPlayPickPanelName))
        {
            GComponent embedded = _gameUIView.Root.GetChild(gameViewPlayPickPanelName)?.asCom;
            if (embedded != null)
                return embedded;
        }

        if (string.IsNullOrEmpty(playStylePickFallbackPopupName))
            return null;

        GObject obj = UIPackage.CreateObject(packageName, playStylePickFallbackPopupName);
        var pop = obj as GComponent;
        if (pop == null)
        {
            obj?.Dispose();
            Debug.LogWarning($"[GameUIManager] 无法创建玩法弹窗 {packageName}/{playStylePickFallbackPopupName}");
            return null;
        }

        _gameUIView.Root.AddChild(pop);
        pop.SetXY(
            Mathf.Max(0f, (_gameUIView.Root.width - pop.width) * 0.5f),
            Mathf.Max(0f, (_gameUIView.Root.height - pop.height) * 0.5f));
        _floatingPlayPickRoot = pop;
        return pop;
    }

    static GButton ResolvePlayPickButton(GComponent root, string preferred, string[] fallback)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            GObject g = root.GetChild(preferred.Trim());
            if (g != null && g.asButton != null)
                return g.asButton;
        }

        foreach (string name in fallback)
        {
            GObject g = root.GetChild(name);
            if (g != null && g.asButton != null)
                return g.asButton;
        }

        return null;
    }

    void RegisterPlayPickClick(GButton btn, EventCallback1 handler)
    {
        btn.onClick.Add(handler);
        _playPickHandlerRegs.Add((btn, handler));
    }

    void UnregisterPlayPickHandlers()
    {
        for (int i = 0; i < _playPickHandlerRegs.Count; i++)
        {
            (GButton button, EventCallback1 handler) = _playPickHandlerRegs[i];
            button?.onClick.Remove(handler);
        }

        _playPickHandlerRegs.Clear();
    }

    void DisposeFloatingPlayPick()
    {
        if (_floatingPlayPickRoot != null && !_floatingPlayPickRoot.isDisposed)
        {
            _floatingPlayPickRoot.Dispose();
            _floatingPlayPickRoot = null;
        }
    }

    void SetCardHolderVisible(bool visible)
    {
        if (_gameUIView == null || _gameUIView.Root.isDisposed)
            return;
        GObject holder = _gameUIView.Root.GetChild(LayeredMatchBoardBinder.HolderChildName);
        if (holder != null)
            holder.visible = visible;
    }

    void OnMatchBoardCompleted()
    {
        if (_gameEnded)
            return;

        _gameEnded = true;
        _finishHadTimeLimit = _effectiveTimeLimitSeconds.HasValue && _effectiveTimeLimitSeconds.Value > 0;

        _finishRemainSecondsSnapshot = !_finishHadTimeLimit
            ? null
            : Mathf.Max(0,
                Mathf.CeilToInt(_countdownEndsAtUnscaled - Time.unscaledTime));
        StopLevelCountdownState();

        if (_campaignLevelListIndex >= 0)
            LevelProgressStore.RegisterLevelCleared(_campaignLevelListIndex);

        if (_levelUIView?.LevelList != null)
            RefreshLevelCellsVisuals(_levelUIView.LevelList);

        ShowFinishPopup(LevelFinishKind.Win);
    }

    void ShowFinishPopup(LevelFinishKind kind)
    {
        EnsurePackageLoaded();
        if (_gameUIView == null || _gameUIView.Root.isDisposed)
            return;

        GObject obj = UIPackage.CreateObject(packageName, finishPopupComponentName);
        var popupRoot = obj as GComponent;
        if (popupRoot == null)
        {
            Debug.LogError($"[GameUIManager] 创建失败：{packageName}/{finishPopupComponentName} 不存在或不是组件。");
            obj?.Dispose();
            return;
        }

        ApplyFinishPopupLayout(popupRoot, kind);

        var finishPopup = new FairyUIPopupHost(popupRoot);
        _gameUIView.PushPopup(finishPopup);

        GButton back = ResolveFinishPopupBackButton(finishPopup.Root);
        if (back != null)
            back.onClick.Add(_finishPopupBackClickHandler);
        else
            Debug.LogWarning("[GameUIManager] finish_popup 未找到返回选关按钮（可设置 finishPopupBackButtonName 或使用 btn_back 等预设名）。");

        GButton retry = ResolveFinishRetryButton(popupRoot);
        if (retry != null)
        {
            bool canRetry = CanRetryCurrentLevel();
            retry.visible = canRetry;
            if (canRetry)
                retry.onClick.Add(_finishRetryClickHandler);
        }
        else
            Debug.LogWarning("[GameUIManager] finish_popup 未找到「再玩一局」按钮（可设置 finishPopupRetryButtonName 或使用 btn_retry 等预设名）。");

        GButton nextBtn = ResolveFinishNextButton(popupRoot);
        if (nextBtn != null)
        {
            bool showNext = kind == LevelFinishKind.Win && CanGoToNextCampaignLevel();
            nextBtn.visible = showNext;
            if (showNext)
                nextBtn.onClick.Add(_finishNextClickHandler);
        }
    }

    bool CanRetryCurrentLevel() =>
        !string.IsNullOrEmpty(_replayLevelContentJson) ||
        _replayScriptableLevel != null ||
        _replayProceduralDefault;

    void ClearGameSessionSchedulingState(bool resetCampaignToo)
    {
        StopLevelCountdownState();
        _gameEnded = false;
        if (resetCampaignToo)
            _campaignLevelListIndex = -1;
        _effectiveTimeLimitSeconds = null;
        _replayTimeLimitSeconds = null;
        _replayLevelContentJson = null;
        _replayLevelTagDisplay = null;
        _replayScriptableLevel = null;
        _replayProceduralDefault = false;
    }

    void StartLevelSessionAfterBind()
    {
        _gameEnded = false;
        StartOrResetLevelCountdown();
    }

    void StartOrResetLevelCountdown()
    {
        StopLevelCountdownState();
        _lastRenderedCountdownSec = -1;

        if (!_effectiveTimeLimitSeconds.HasValue || _effectiveTimeLimitSeconds.Value <= 0)
        {
            SetGameTimeDisplayText("--:--");
            return;
        }

        _levelCountdownActive = true;
        _countdownEndsAtUnscaled = Time.unscaledTime + _effectiveTimeLimitSeconds.Value;
        _lastRenderedCountdownSec = _effectiveTimeLimitSeconds.Value;
        SetGameTimeDisplayText(FormatMmSs(_lastRenderedCountdownSec));
    }

    void StopLevelCountdownState()
    {
        _levelCountdownActive = false;
    }

    void SetGameTimeDisplayText(string s)
    {
        GTextField tf = _gameUIView?.TxtTime;
        if (tf != null && !tf.isDisposed)
            tf.text = s ?? string.Empty;
    }

    static string FormatMmSs(int totalSeconds)
    {
        totalSeconds = Mathf.Max(0, totalSeconds);
        int m = totalSeconds / 60;
        int sec = totalSeconds % 60;
        return $"{m}:{sec:00}";
    }

    void Update()
    {
        if (!_levelCountdownActive || _gameEnded)
            return;
        if (_gameUIView == null || _gameUIView.Root.isDisposed)
            return;

        float remain = _countdownEndsAtUnscaled - Time.unscaledTime;
        if (remain <= 0f)
        {
            SetGameTimeDisplayText("0:00");
            OnLevelTimeUp();
            return;
        }

        int ceil = Mathf.CeilToInt(remain);
        if (ceil != _lastRenderedCountdownSec)
        {
            _lastRenderedCountdownSec = ceil;
            SetGameTimeDisplayText(FormatMmSs(ceil));
        }
    }

    void OnLevelTimeUp()
    {
        if (!_levelCountdownActive || _gameEnded)
            return;

        _gameEnded = true;
        _levelCountdownActive = false;

        _finishHadTimeLimit = true;
        _finishRemainSecondsSnapshot = 0;

        _boardBinder?.SetBoardInteractionLocked(true);
        ShowFinishPopup(LevelFinishKind.TimeUp);
    }

    void OnMatchBarOverflow()
    {
        if (_gameEnded)
            return;

        _gameEnded = true;
        _finishHadTimeLimit = _effectiveTimeLimitSeconds.HasValue && _effectiveTimeLimitSeconds.Value > 0;
        _finishRemainSecondsSnapshot = !_finishHadTimeLimit
            ? null
            : Mathf.Max(0, Mathf.CeilToInt(_countdownEndsAtUnscaled - Time.unscaledTime));
        StopLevelCountdownState();

        _boardBinder?.SetBoardInteractionLocked(true);
        ShowFinishPopup(LevelFinishKind.QueueFull);
    }

    void ApplyFinishPopupLayout(GComponent root, LevelFinishKind kind)
    {
        if (!string.IsNullOrEmpty(finishPopupStatusControllerName))
        {
            Controller ctrl = root.GetController(finishPopupStatusControllerName);
            if (ctrl != null)
                ctrl.selectedIndex = kind == LevelFinishKind.Win ? 0 : 1;
        }

        if (kind == LevelFinishKind.Win)
        {
            TrySetFinishText(root, "txt_title", "闯关成功");
            TrySetFinishText(root, "txt_subtitle", "你已消除全部卡牌。");
            if (_finishHadTimeLimit && _finishRemainSecondsSnapshot.HasValue)
            {
                TrySetFinishText(root, "txt_stats", $"剩余时间 {FormatMmSs(_finishRemainSecondsSnapshot.Value)}");
                TrySetFinishText(root, "txt_hint", "可返回选关或再玩一局刷新成绩。");
            }
            else
            {
                TrySetFinishText(root, "txt_stats", "本关未开启限时");
                TrySetFinishText(root, "txt_hint", "可在关卡 content JSON 中设置 time 或 timeLimitSeconds（秒）开启倒计时。");
            }
        }
        else if (kind == LevelFinishKind.TimeUp)
        {
            TrySetFinishText(root, "txt_title", "挑战结束 · 超时");
            TrySetFinishText(root, "txt_subtitle", "本关倒计时已用尽。");
            int lim = _effectiveTimeLimitSeconds ?? _replayTimeLimitSeconds ?? 0;
            TrySetFinishText(root, "txt_stats", $"本关限时 {FormatMmSs(lim)}");
            TrySetFinishText(root, "txt_hint", "使用「提示」找可消对子，或重开本关。");
        }
        else
        {
            TrySetFinishText(root, "txt_title", "挑战失败 · 栏位已满");
            TrySetFinishText(root, "txt_subtitle", "收纳栏已达上限，无法再移入卡牌。");
            TrySetFinishText(root, "txt_stats", "请合理规划入栏顺序，或重开本关。");
            TrySetFinishText(root, "txt_hint", "可在关卡 content JSON 中设置 queueMaxSlots 或 queueMax 调整栏位长度。");
        }

        TrySetFinishText(root, "txt_level",
            string.IsNullOrEmpty(_replayLevelTagDisplay) ? "" : $"当前：{_replayLevelTagDisplay}");
    }

    static void TrySetFinishText(GComponent root, string childName, string text)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return;
        GTextField tf = root.GetChild(childName)?.asTextField;
        if (tf != null && !tf.isDisposed)
            tf.text = text ?? string.Empty;
    }

    void OnFinishRetryClicked(EventContext _)
    {
        if (_gameUIView == null || _gameUIView.Root.isDisposed || !CanRetryCurrentLevel())
            return;

        _gameUIView.ClearPopups();

        try
        {
            _gameEnded = false;

            if (!string.IsNullOrEmpty(_replayLevelContentJson))
            {
                MatchLevelDefinition def = MatchLevelDefinitionFromJson.Create(_replayLevelContentJson);
                Destroy(_ownedMatchLevelInstance);
                _ownedMatchLevelInstance = def;

                _boardBinder?.Dispose();
                _boardBinder = null;
                ApplyEffectiveLimitAfterRetry(def);
                ResetPlayPickUiState();
                BeginPlayStyleChoiceOrBindBoard(def);
            }
            else if (_replayScriptableLevel != null)
            {
                DisposeOwnedMatchLevel();
                MatchLevelDefinition def = _replayScriptableLevel;

                _boardBinder?.Dispose();
                _boardBinder = null;
                ApplyEffectiveLimitAfterRetry(def);
                ResetPlayPickUiState();
                BeginPlayStyleChoiceOrBindBoard(def);
            }
            else if (_replayProceduralDefault)
            {
                DisposeOwnedMatchLevel();

                _boardBinder?.Dispose();
                _boardBinder = null;
                _effectiveTimeLimitSeconds = _replayTimeLimitSeconds;
                ResetPlayPickUiState();
                BeginPlayStyleChoiceOrBindBoard(null);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameUIManager] 再玩一次加载失败：{ex.Message}");
        }
    }

    void ApplyEffectiveLimitAfterRetry(MatchLevelDefinition def)
    {
        _effectiveTimeLimitSeconds = _replayTimeLimitSeconds;
        if (def != null &&
            def.timeLimitSeconds > 0 &&
            (!_effectiveTimeLimitSeconds.HasValue || _effectiveTimeLimitSeconds.Value <= 0))
            _effectiveTimeLimitSeconds = def.timeLimitSeconds;
    }

    static GButton ResolveFinishRetryButton(GComponent popup, string preferredName)
    {
        if (!string.IsNullOrEmpty(preferredName))
        {
            GObject g = popup.GetChild(preferredName);
            if (g != null && g.asButton != null)
                return g.asButton;
        }

        foreach (string name in new[] { "btn_retry", "btn_again", "retry", "btn_replay", "replay" })
        {
            GObject g = popup.GetChild(name);
            if (g != null && g.asButton != null)
                return g.asButton;
        }

        return null;
    }

    GButton ResolveFinishRetryButton(GComponent popup) =>
        ResolveFinishRetryButton(popup, finishPopupRetryButtonName);

    static GButton ResolveFinishNextButton(GComponent popup, string preferredName)
    {
        if (!string.IsNullOrEmpty(preferredName))
        {
            GObject g = popup.GetChild(preferredName);
            if (g != null && g.asButton != null)
                return g.asButton;
        }

        foreach (string name in new[] { "btn_next", "next", "btn_continue", "continue", "forward" })
        {
            GObject g = popup.GetChild(name);
            if (g != null && g.asButton != null)
                return g.asButton;
        }

        return null;
    }

    GButton ResolveFinishNextButton(GComponent popup) =>
        ResolveFinishNextButton(popup, finishPopupNextButtonName);

    static GButton ResolveFinishPopupBackButton(GComponent popup, string preferredName)
    {
        if (!string.IsNullOrEmpty(preferredName))
        {
            GObject g = popup.GetChild(preferredName);
            if (g != null && g.asButton != null)
                return g.asButton;
        }

        foreach (string name in new[] { "btn_back", "btn_confirm", "btn_ok", "n0" })
        {
            GObject g = popup.GetChild(name);
            if (g != null && g.asButton != null)
                return g.asButton;
        }

        return null;
    }

    GButton ResolveFinishPopupBackButton(GComponent popup) =>
        ResolveFinishPopupBackButton(popup, finishPopupBackButtonName);

    void OnFinishPopupBackClicked(EventContext _)
    {
        CloseGameView();
    }

    void TearDownBoard()
    {
        _boardBinder?.Dispose();
        _boardBinder = null;
    }

    /// <summary>
    /// 关闭游戏界面。
    /// </summary>
    public void CloseGameView()
    {
        StopLevelCountdownState();
        _gameEnded = false;

        TearDownBoard();
        DisposeOwnedMatchLevel();

        if (_gameUIView == null || _gameUIView.Root.isDisposed)
        {
            _gameUIView = null;
            RefreshUnderlayVisibility();
            return;
        }

        UnbindGameToolbarIfPresent();

        UnregisterPlayPickHandlers();
        DisposeFloatingPlayPick();

        _gameUIView.ClearPopups();
        _gameUIView.Root.Dispose();
        _gameUIView = null;

        _campaignLevelListIndex = -1;
        _replayLevelContentJson = null;
        _replayLevelTagDisplay = null;
        _replayTimeLimitSeconds = null;
        _replayScriptableLevel = null;
        _replayProceduralDefault = false;

        BringLevelViewToFrontOnGRoot();
        if (_levelUIView != null && !_levelUIView.Root.isDisposed && _levelUIView.LevelList != null &&
            RefreshLevelRowTags())
        {
            GList lvList = _levelUIView.LevelList;
            lvList.numItems = _levelRowTagsOrdered.Count;
            RefreshLevelCellsVisuals(lvList);
        }

        RefreshUnderlayVisibility();
    }

    /// <summary>
    /// 关闭游戏 View 栈顶的弹窗（若有）；用于多层弹窗时仅关掉最上一层。
    /// </summary>
    public bool CloseTopGamePopup()
    {
        return _gameUIView != null && _gameUIView.CloseTopPopup();
    }
}
