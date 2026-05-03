using FairyGUI;
using UnityEngine;

/// <summary>
/// 绑定 SampleScene 中 UIPanel（main_view）上的 FairyGUI 控件。
/// </summary>
[RequireComponent(typeof(UIPanel))]
[RequireComponent(typeof(GameUIManager))]
public class MainViewUI : MonoBehaviour
{
    UIPanel _panel;
    GameUIManager _uiNav;
    GButton _n0Button;

    /// <summary>main_view 根节点（与 UIPanel.componentName 一致）。</summary>
    public GComponent MainViewRoot => _panel != null ? _panel.ui : null;

    /// <summary>FairyGUI 里名为 n0 的按钮。</summary>
    public GButton N0Button => _n0Button;

    void Awake()
    {
        _panel = GetComponent<UIPanel>();
        _uiNav = GetComponent<GameUIManager>();
    }

    void Start()
    {
        var root = _panel.ui;
        if (root == null)
        {
            Debug.LogError("[MainViewUI] UIPanel.ui 为空，请确认 Package1/main_view 已正确加载。");
            return;
        }

        var n0 = root.GetChild("n0");
        if (n0 == null)
        {
            Debug.LogError("[MainViewUI] main_view 下未找到名为 \"n0\" 的子节点。");
            return;
        }

        _n0Button = n0.asButton;
        if (_n0Button == null)
        {
            Debug.LogError("[MainViewUI] 子节点 \"n0\" 不是 Button 类型（asButton 为空）。");
            return;
        }

        if (_uiNav == null)
        {
            Debug.LogError("[MainViewUI] 需要同物体挂载 GameUIManager。");
            return;
        }

        _n0Button.onClick.Add(OnN0Clicked);
    }

    void OnDestroy()
    {
        if (_n0Button != null)
            _n0Button.onClick.Remove(OnN0Clicked);
    }

    void OnN0Clicked()
    {
        _uiNav.OpenLevelView();
    }
}
