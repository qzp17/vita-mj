using System.Collections.Generic;
using FairyGUI;

/// <summary>
/// 挂在某一 View 根节点上的弹窗栈：新弹窗叠在最上层；关闭顶层后下层自动露出；
/// View 整体关闭时应调用 <see cref="Clear"/>，避免残留引用。
/// </summary>
public sealed class ViewPopupStack
{
    readonly GComponent _viewRoot;
    readonly Stack<GComponent> _popups = new Stack<GComponent>();

    public ViewPopupStack(GComponent viewRoot)
    {
        _viewRoot = viewRoot;
    }

    /// <summary>当前栈深度（含顶层）。</summary>
    public int Count => _popups.Count;

    /// <summary>
    /// 将已创建的弹窗组件压栈并显示在 View 最上层。
    /// </summary>
    public void Push(GComponent popup)
    {
        if (popup == null || popup.isDisposed || _viewRoot == null || _viewRoot.isDisposed)
            return;

        _viewRoot.AddChild(popup);
        popup.SetXY(0, 0);
        popup.MakeFullScreen();
        popup.AddRelation(_viewRoot, RelationType.Size);
        _viewRoot.SetChildIndex(popup, _viewRoot.numChildren - 1);
        _popups.Push(popup);
    }

    /// <summary>
    /// 关闭栈顶弹窗；若有下层弹窗仍在层级中则会自动显示。
    /// </summary>
    /// <returns>是否关闭了一层。</returns>
    public bool CloseTop()
    {
        while (_popups.Count > 0)
        {
            GComponent top = _popups.Pop();
            if (top != null && !top.isDisposed)
            {
                top.Dispose();
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 清空栈内所有弹窗（不关 View 本身）。
    /// </summary>
    public void Clear()
    {
        while (_popups.Count > 0)
        {
            GComponent p = _popups.Pop();
            if (p != null && !p.isDisposed)
                p.Dispose();
        }
    }
}
