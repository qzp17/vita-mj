using FairyGUI;

/// <summary>主界面 main_view（UIPanel 根节点）。</summary>
public sealed class MainUIView : FairyUIViewBase
{
    public MainUIView(GComponent root)
        : base(root)
    {
    }

    public GButton N0Button => Root.GetChild("n0")?.asButton;
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
}
