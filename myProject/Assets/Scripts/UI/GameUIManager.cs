using FairyGUI;
using UnityEngine;
using VitaMj.MatchGame;

/// <summary>
/// 全局 FairyGUI 界面逻辑：主界面由场景里的 UIPanel（main_view）承担；
/// 选关与游戏界面挂在 <see cref="GRoot"/> 上。打开上层时隐藏下层（含主界面）；关闭游戏后若有选关界面则重新显示并置于最前，否则恢复主界面。
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

    [Tooltip("棋盘每张卡片对应的 FairyGUI 组件名（Package1 内）")]
    [SerializeField]
    string cardComponentName = "card";

    [SerializeField]
    float cardCellGap = 8f;

    [Tooltip("关卡列表数据源；为空或无可导入行时，选关界面将直接进入默认游戏")]
    [SerializeField]
    LevelConfig levelConfig;

    [Tooltip("留空则打开游戏界面时使用默认 4×5×2 程序化棋盘")]
    [SerializeField]
    MatchLevelDefinition matchLevel;

    [Tooltip("主界面 UIPanel（main_view）；为空则在同物体上 GetComponent<UIPanel>")]
    [SerializeField]
    UIPanel mainUIPanel;

    GComponent _levelView;
    GComponent _gameView;
    LayeredMatchBoardBinder _boardBinder;

    /// <summary>由选关 JSON 生成的临时关卡，关闭游戏时需 Destroy。</summary>
    MatchLevelDefinition _ownedMatchLevelInstance;

    EventCallback1 _levelListClickHandler;

    public bool IsLevelViewOpen => _levelView != null && !_levelView.isDisposed;

    public bool IsGameViewOpen => _gameView != null && !_gameView.isDisposed;

    void Awake()
    {
        Instance = this;
        _levelListClickHandler = OnLevelListItemClick;
        if (mainUIPanel == null)
            mainUIPanel = GetComponent<UIPanel>();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

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
    /// 打开选关界面：根据 <see cref="LevelConfig"/> 填充名为 level 的 GList；若无配置则直接进入游戏。
    /// </summary>
    public void OpenLevelView()
    {
        EnsurePackageLoaded();

        if (levelConfig == null || levelConfig.rows == null || levelConfig.rows.Count == 0)
        {
            Debug.LogWarning("[GameUIManager] LevelConfig 为空或无行，跳过选关界面。");
            OpenGameView(null, false);
            return;
        }

        if (_levelView != null && !_levelView.isDisposed)
        {
            RefreshLevelList();
            _levelView.visible = true;
            GRoot.inst.SetChildIndex(_levelView, GRoot.inst.numChildren - 1);
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

        _levelView = com;
        GRoot.inst.AddChild(_levelView);
        _levelView.SetXY(0, 0);
        _levelView.MakeFullScreen();
        _levelView.AddRelation(GRoot.inst, RelationType.Size);

        BindLevelList(_levelView);
        RefreshUnderlayVisibility();
    }

    void BindLevelList(GComponent root)
    {
        GList list = root.GetChild("level")?.asList;
        if (list == null)
        {
            Debug.LogError("[GameUIManager] level_view 下未找到名为 \"level\" 的 GList。");
            return;
        }

        list.RemoveChildrenToPool();
        list.itemRenderer = RenderLevelListCell;
        list.onClickItem.Add(_levelListClickHandler);
        list.numItems = levelConfig.rows.Count;
    }

    void RefreshLevelList()
    {
        if (_levelView == null || _levelView.isDisposed || levelConfig == null)
            return;
        GList list = _levelView.GetChild("level")?.asList;
        if (list == null)
            return;
        list.numItems = levelConfig.rows.Count;
    }

    void RenderLevelListCell(int index, GObject obj)
    {
        if (levelConfig == null || index < 0 || index >= levelConfig.rows.Count)
            return;

        LevelConfigRow row = levelConfig.rows[index];
        obj.data = index;

        GComponent item = obj.asCom;
        if (item == null)
            return;

        GTextField lv = item.GetChild("lv")?.asTextField;
        if (lv != null)
            lv.text = $"{row.tag} · {row.level}";
    }

    void OnLevelListItemClick(EventContext context)
    {
        if (levelConfig == null || !(context.data is GObject item) || !(item.data is int idx))
            return;

        if (idx < 0 || idx >= levelConfig.rows.Count)
            return;

        LevelConfigRow row = levelConfig.rows[idx];
        if (!levelConfig.TryCreateMatchLevel(row.tag, row.level, out MatchLevelDefinition def, out string err))
        {
            Debug.LogError($"[GameUIManager] 关卡加载失败（{row.tag}/{row.level}）：{err}");
            return;
        }

        HideLevelViewForOverlay();
        OpenGameView(def, true);
    }

    /// <summary>进入游戏时保留选关界面实例，仅隐藏，便于关闭游戏后回到选关。</summary>
    void HideLevelViewForOverlay()
    {
        if (_levelView != null && !_levelView.isDisposed)
            _levelView.visible = false;
    }

    /// <summary>是否存在遮住主界面的全屏层（选关可见，或游戏界面存在）。</summary>
    bool HasFullscreenOverlay()
    {
        if (_gameView != null && !_gameView.isDisposed)
            return true;
        if (_levelView != null && !_levelView.isDisposed && _levelView.visible)
            return true;
        return false;
    }

    void RefreshUnderlayVisibility()
    {
        if (mainUIPanel?.ui != null)
            mainUIPanel.ui.visible = !HasFullscreenOverlay();
    }

    void BringLevelViewToFrontOnGRoot()
    {
        if (_levelView == null || _levelView.isDisposed)
            return;
        _levelView.visible = true;
        GRoot.inst.SetChildIndex(_levelView, GRoot.inst.numChildren - 1);
    }

    /// <summary>
    /// 关闭选关界面。
    /// </summary>
    public void CloseLevelView()
    {
        if (_levelView == null || _levelView.isDisposed)
        {
            _levelView = null;
            return;
        }

        GList list = _levelView.GetChild("level")?.asList;
        if (list != null)
        {
            list.onClickItem.Remove(_levelListClickHandler);
            list.itemRenderer = null;
            list.numItems = 0;
        }

        _levelView.Dispose();
        _levelView = null;
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

        if (_gameView != null && !_gameView.isDisposed)
        {
            DisposeOwnedMatchLevel();
            TearDownBoard();

            _ownedMatchLevelInstance = destroyLevelWhenClosed ? def : null;

            _boardBinder = new LayeredMatchBoardBinder();
            _boardBinder.Bind(_gameView, packageName, cardComponentName, cardCellGap, def);
            GRoot.inst.SetChildIndex(_gameView, GRoot.inst.numChildren - 1);
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

        _gameView = com;
        GRoot.inst.AddChild(_gameView);
        _gameView.SetXY(0, 0);
        _gameView.MakeFullScreen();
        _gameView.AddRelation(GRoot.inst, RelationType.Size);

        _boardBinder = new LayeredMatchBoardBinder();
        _boardBinder.Bind(_gameView, packageName, cardComponentName, cardCellGap, def);
        RefreshUnderlayVisibility();
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

        if (_gameView == null || _gameView.isDisposed)
        {
            _gameView = null;
            RefreshUnderlayVisibility();
            return;
        }

        _gameView.Dispose();
        _gameView = null;

        BringLevelViewToFrontOnGRoot();
        RefreshUnderlayVisibility();
    }
}
