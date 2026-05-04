using System;
using System.Globalization;
using FairyGUI;
using UnityEngine;
using VitaMj.Config;
using VitaMj.MatchGame;

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

    [Tooltip("finish_popup 内返回选关的按钮名；留空则依次尝试 btn_back、btn_confirm、btn_ok、n0")]
    [SerializeField]
    string finishPopupBackButtonName = "";

    [Tooltip("棋盘每张卡片对应的 FairyGUI 组件名（Package1 内）")]
    [SerializeField]
    string cardComponentName = "card";

    [SerializeField]
    float cardCellGap = 8f;

    [Tooltip("选关列表：ConfigReader 表名，与 Assets/Config 下 xlsx 文件名一致（不含扩展名），如 Level → Resources/ExportConfig/Level.bytes")]
    [SerializeField]
    string levelConfigTableKey = "Level";

    [Tooltip("留空则打开游戏界面时使用默认 4×5×2 程序化棋盘")]
    [SerializeField]
    MatchLevelDefinition matchLevel;

    [Tooltip("主界面 UIPanel（main_view）；为空则在同物体上 GetComponent<UIPanel>")]
    [SerializeField]
    UIPanel mainUIPanel;

    MainUIView _mainUIView;
    LevelUIView _levelUIView;
    GameUIView _gameUIView;
    LayeredMatchBoardBinder _boardBinder;

    /// <summary>由选关 JSON 生成的临时关卡，关闭游戏时需 Destroy。</summary>
    MatchLevelDefinition _ownedMatchLevelInstance;

    EventCallback1 _levelListClickHandler;
    EventCallback1 _finishPopupBackClickHandler;

    /// <summary>当前 Level 表中行的顺序（与导出 Excel 行序一致），列表项索引与之对应。</summary>
    readonly System.Collections.Generic.List<string> _levelRowTagsOrdered = new System.Collections.Generic.List<string>();

    public bool IsLevelViewOpen => _levelUIView != null && !_levelUIView.Root.isDisposed;

    public bool IsGameViewOpen => _gameUIView != null && !_gameUIView.Root.isDisposed;

    void Awake()
    {
        Instance = this;
        _levelListClickHandler = OnLevelListItemClick;
        _finishPopupBackClickHandler = OnFinishPopupBackClicked;
        if (mainUIPanel == null)
            mainUIPanel = GetComponent<UIPanel>();
        EnsureMainUIView();
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

        GTextField lv = item.GetChild("lv")?.asTextField;
        if (lv != null)
            lv.text = $"{cfg.tag} · {cfg.level}";
    }

    void OnLevelListItemClick(EventContext context)
    {
        if (!(context.data is GObject item) || !(item.data is string tagKey))
            return;

        if (!ConfigReader.TryGetRow<ConfigLevelRow>(levelConfigTableKey, tagKey, out ConfigLevelRow cfg))
            return;

        if (string.IsNullOrWhiteSpace(cfg.content))
        {
            Debug.LogError($"[GameUIManager] 关卡 content 为空（tag={tagKey}）。");
            return;
        }

        if (!TryParseLevelCell(cfg.level, out int levelNum))
        {
            Debug.LogError($"[GameUIManager] 无法解析 level 列：「{cfg.level}」（tag={tagKey}）");
            return;
        }

        MatchLevelDefinition def = null;
        try
        {
            def = MatchLevelDefinitionFromJson.Create(cfg.content);
            _ = new ConfiguredPairMatchGame(def);
        }
        catch (Exception ex)
        {
            if (def != null)
                Destroy(def);

            Debug.LogError($"[GameUIManager] 关卡加载失败（{cfg.tag}/{levelNum}）：{ex.Message}");
            return;
        }

        HideLevelViewForOverlay();
        OpenGameView(def, true);
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
        EnsurePackageLoaded();

        MatchLevelDefinition def = levelOverride != null ? levelOverride : matchLevel;

        if (_gameUIView != null && !_gameUIView.Root.isDisposed)
        {
            DisposeOwnedMatchLevel();
            TearDownBoard();

            _gameUIView.ClearPopups();

            _ownedMatchLevelInstance = destroyLevelWhenClosed ? def : null;

            _boardBinder = new LayeredMatchBoardBinder();
            _boardBinder.Bind(_gameUIView.Root, packageName, cardComponentName, cardCellGap, def, OnMatchBoardCompleted);
            GRoot.inst.SetChildIndex(_gameUIView.Root, GRoot.inst.numChildren - 1);
            RefreshUnderlayVisibility();
            return;
        }

        TearDownBoard();
        DisposeOwnedMatchLevel();
        _ownedMatchLevelInstance = destroyLevelWhenClosed ? def : null;

        GObject obj = UIPackage.CreateObject(packageName, gameViewComponentName);
        var com = obj as GComponent;
        if (com == null)
        {
            Debug.LogError($"[GameUIManager] 创建失败：{packageName}/{gameViewComponentName} 不存在或不是组件。");
            obj?.Dispose();
            DisposeOwnedMatchLevel();
            BringLevelViewToFrontOnGRoot();
            RefreshUnderlayVisibility();
            return;
        }

        _gameUIView = new GameUIView(com);
        GRoot.inst.AddChild(_gameUIView.Root);
        _gameUIView.Root.SetXY(0, 0);
        _gameUIView.Root.MakeFullScreen();
        _gameUIView.Root.AddRelation(GRoot.inst, RelationType.Size);

        _boardBinder = new LayeredMatchBoardBinder();
        _boardBinder.Bind(_gameUIView.Root, packageName, cardComponentName, cardCellGap, def, OnMatchBoardCompleted);
        RefreshUnderlayVisibility();
    }

    void OnMatchBoardCompleted()
    {
        ShowFinishPopup();
    }

    void ShowFinishPopup()
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

        var finishPopup = new FairyUIPopupHost(popupRoot);
        _gameUIView.PushPopup(finishPopup);

        GButton btn = ResolveFinishPopupBackButton(finishPopup.Root);
        if (btn != null)
            btn.onClick.Add(_finishPopupBackClickHandler);
        else
            Debug.LogWarning("[GameUIManager] finish_popup 未找到返回按钮（可在 Inspector 填写 finishPopupBackButtonName）。");
    }

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
        TearDownBoard();
        DisposeOwnedMatchLevel();

        if (_gameUIView == null || _gameUIView.Root.isDisposed)
        {
            _gameUIView = null;
            RefreshUnderlayVisibility();
            return;
        }

        _gameUIView.ClearPopups();
        _gameUIView.Root.Dispose();
        _gameUIView = null;

        BringLevelViewToFrontOnGRoot();
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
