using FairyGUI;

/// <summary>
/// FairyGUI 弹窗逻辑基类：包装导出的弹窗组件根节点，便于与 <see cref="FairyUIViewBase"/> 区分类型并扩展共用行为。
/// </summary>
public abstract class FairyUIPopupBase
{
    protected FairyUIPopupBase(GComponent root)
    {
        Root = root ?? throw new System.ArgumentNullException(nameof(root));
    }

    public GComponent Root { get; }
}

/// <summary>
/// 运行时由 <see cref="UIPackage.CreateObject"/> 创建的弹窗实例的默认宿主。
/// </summary>
public sealed class FairyUIPopupHost : FairyUIPopupBase
{
    public FairyUIPopupHost(GComponent root)
        : base(root)
    {
    }
}
