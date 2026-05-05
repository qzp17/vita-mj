using FairyGUI;
using UnityEngine;

/// <summary>
/// 绑定 SampleScene 中 UIPanel（main_view）上的 FairyGUI 控件。
/// </summary>
[RequireComponent(typeof(UIPanel))]
[RequireComponent(typeof(GameUIManager))]
public class MainViewUI : MonoBehaviour
{
    GameUIManager _uiNav;

    /// <summary>main_view 包装（含弹窗栈，根节点与 <see cref="GameUIManager"/> 共用同一实例）。</summary>
    public MainUIView MainView { get; private set; }

    /// <summary>main_view 根节点。</summary>
    public GComponent MainViewRoot => MainView?.Root;

    /// <summary>FairyGUI 里名为 n0 的按钮。</summary>
    public GButton N0Button { get; private set; }

    public GButton BtnSetting { get; private set; }

    EventCallback1 _onSettingClicked;

    void Awake()
    {
        _uiNav = GetComponent<GameUIManager>();
    }

    void Start()
    {
        if (_uiNav == null)
        {
            Debug.LogError("[MainViewUI] 需要同物体挂载 GameUIManager。");
            return;
        }

        MainView = _uiNav.EnsureMainUIView();
        if (MainView == null)
        {
            Debug.LogError("[MainViewUI] UIPanel.ui 为空，请确认 Package1/main_view 已正确加载。");
            return;
        }

        N0Button = MainView.N0Button;
        if (N0Button == null)
        {
            Debug.LogError("[MainViewUI] main_view 下未找到名为 \"n0\" 的按钮。");
            return;
        }

        N0Button.onClick.Add(OnN0Clicked);

        BtnSetting = MainView.BtnSetting;
        if (BtnSetting != null)
        {
            _onSettingClicked = OnSettingClicked;
            BtnSetting.onClick.Add(_onSettingClicked);
        }
    }

    void OnDestroy()
    {
        if (N0Button != null)
            N0Button.onClick.Remove(OnN0Clicked);
        if (BtnSetting != null && _onSettingClicked != null)
            BtnSetting.onClick.Remove(_onSettingClicked);
    }

    void OnSettingClicked(EventContext _)
    {
        _uiNav.OpenSettingPopup();
    }

    void OnN0Clicked(EventContext _)
    {
        _uiNav.OpenLevelView();
    }
}

