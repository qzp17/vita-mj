using FairyGUI;

/// <summary>
/// FairyGUI 全屏 View 基类：持有根 <see cref="GComponent"/>，并在其上维护 <see cref="ViewPopupStack"/>。
/// 关闭顶层弹窗后下层自动露出；关闭 View 根节点前应调用 <see cref="ClearPopups"/>。
/// </summary>
public abstract class FairyUIViewBase
{
    readonly ViewPopupStack _popupStack;

    protected FairyUIViewBase(GComponent root)
    {
        Root = root ?? throw new System.ArgumentNullException(nameof(root));
        _popupStack = new ViewPopupStack(root);
    }

    public GComponent Root { get; }

    /// <summary>当前 View 上弹窗栈深度。</summary>
    public int PopupDepth => _popupStack.Count;

    /// <summary>将弹窗压入本 View 栈顶并显示在最上层。</summary>
    public void PushPopup(GComponent popup) => _popupStack.Push(popup);

    /// <summary>将封装后的弹窗压栈。</summary>
    public void PushPopup(FairyUIPopupBase popup)
    {
        if (popup == null)
            return;
        _popupStack.Push(popup.Root);
    }

    /// <summary>关闭栈顶一层弹窗。</summary>
    public bool CloseTopPopup() => _popupStack.CloseTop();

    /// <summary>清除本 View 上全部弹窗（不销毁 View 根节点）。</summary>
    public void ClearPopups() => _popupStack.Clear();
}
