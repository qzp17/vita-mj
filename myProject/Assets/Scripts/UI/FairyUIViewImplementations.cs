using FairyGUI;

/// <summary>主界面 main_view（UIPanel 根节点）。</summary>
public sealed class MainUIView : FairyUIViewBase
{
    public MainUIView(GComponent root)
        : base(root)
    {
    }

    public GButton N0Button => Root.GetChild("n0")?.asButton;

    public GButton BtnSetting => Root.GetChild("btn_setting")?.asButton;

    public GButton BtnArrow => Root.GetChild("btn_arrow")?.asButton;
}

/// <summary>选关界面 level_view（GRoot 子节点）。</summary>
public sealed class LevelUIView : FairyUIViewBase
{
    public LevelUIView(GComponent root)
        : base(root)
    {
    }

    public GList LevelList => Root.GetChild("level")?.asList;
}

/// <summary>对局界面 game_view（GRoot 子节点）。</summary>
public sealed class GameUIView : FairyUIViewBase
{
    public GameUIView(GComponent root)
        : base(root)
    {
    }

    public GButton BtnHelp => Root.GetChild("btn_help")?.asButton;

    public GButton BtnQuit => Root.GetChild("btn_quit")?.asButton;

    public GTextField TxtTime => Root.GetChild("txt_time")?.asTextField;
}

/// <summary>箭头玩法界面 game_arrow_view（GRoot 子节点）。</summary>
public sealed class ArrowGameUIView : FairyUIViewBase
{
    public ArrowGameUIView(GComponent root)
        : base(root)
    {
    }

    public GComponent BoardPanel =>
        Root.GetChild("panel")?.asCom ??
        Root.GetChild("board_panel")?.asCom ??
        Root.GetChild("arrow_panel")?.asCom;

    public GButton BtnBack =>
        Root.GetChild("btn_back")?.asButton ??
        Root.GetChild("btn_quit")?.asButton;

    public GButton BtnRetry =>
        Root.GetChild("btn_retry")?.asButton ??
        Root.GetChild("btn_restart")?.asButton ??
        Root.GetChild("retry")?.asButton;

    public GButton BtnNext =>
        Root.GetChild("btn_next")?.asButton ??
        Root.GetChild("btn_continue")?.asButton ??
        Root.GetChild("next")?.asButton;
}
