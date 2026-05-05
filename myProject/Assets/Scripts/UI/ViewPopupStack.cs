using System.Collections.Generic;
using FairyGUI;
using UnityEngine;

/// <summary>
/// 挂在某一 View 根节点上的弹窗栈：新弹窗叠在最上层；关闭顶层后下层自动露出；
/// View 整体关闭时应调用 <see cref="Clear"/>，避免残留引用。
/// <para>压栈时为弹窗套上全屏层：半透明遮罩拦截下层点击；弹窗本体按设计尺寸在 View 内居中。</para>
/// </summary>
public sealed class ViewPopupStack
{
    const float DefaultModalAlpha = 0.55f;

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

        GComponent modalLayer = BuildModalWrappedPopup(_viewRoot, popup);

        _viewRoot.AddChild(modalLayer);
        modalLayer.SetXY(0, 0);
        modalLayer.SetSize(_viewRoot.width, _viewRoot.height);
        modalLayer.AddRelation(_viewRoot, RelationType.Size);
        _viewRoot.SetChildIndex(modalLayer, _viewRoot.numChildren - 1);
        _popups.Push(modalLayer);
    }

    /// <summary>全屏容器 + 底遮罩 + 居中弹窗内容。</summary>
    static GComponent BuildModalWrappedPopup(GComponent viewRoot, GComponent popup)
    {
        var layer = new GComponent();
        layer.name = layer.gameObjectName = "ViewPopupModalLayer";

        float w = Mathf.Max(1f, viewRoot.width);
        float h = Mathf.Max(1f, viewRoot.height);

        layer.SetSize(w, h);

        var mask = new GGraph();
        mask.name = mask.gameObjectName = "ViewPopupModalMask";
        mask.touchable = true;
        Color fill = Color.black;
        fill.a = DefaultModalAlpha;
        mask.DrawRect(w, h, 0, Color.clear, fill);
        mask.AddRelation(layer, RelationType.Size);
        mask.SetXY(0, 0);
        layer.AddChild(mask);

        layer.AddChild(popup);
        popup.Center(true);

        return layer;
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
