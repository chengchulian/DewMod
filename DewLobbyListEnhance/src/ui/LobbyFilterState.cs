using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

namespace DewLobbyListEnhance.ui;

internal sealed class LobbyFilterState
{
    private readonly Transform _root;
    private readonly UI_Title_FindLobbyView _view;
    private readonly TMP_Dropdown[] _dropdowns = new TMP_Dropdown[4];
    // 0=全部，1=仅 Steam，2=跨平台（EOS 属性 ALL）。
    internal int Platform => _dropdowns[0] == null ? 0 : _dropdowns[0].value;
    internal int Sort => _dropdowns[1] == null ? 0 : _dropdowns[1].value;
    internal int Publicity => _dropdowns[2] == null ? 0 : _dropdowns[2].value;
    internal int Difficulty => _dropdowns[3] == null ? 0 : _dropdowns[3].value;
    internal static LobbyFilterState Active => DewLobbyListEnhance.States.Values.FirstOrDefault(state => state != null);

    internal LobbyFilterState(UI_Title_FindLobbyView view)
    {
        _view = view;
        GameObject root = new GameObject("DewLobbyListEnhance.Filters", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(Outline));
        root.name = "DewLobbyListEnhance.Filters";
        root.transform.SetParent(view.transform, false);
        root.transform.SetAsLastSibling();
        _root = root.transform;
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = new Vector2(1f, 1f); rect.anchorMax = new Vector2(1f, 1f);
        // LanMode 的选择框锚定右上角；大厅筛选条紧接其下方。
        rect.pivot = new Vector2(1f, 1f); rect.anchoredPosition = new Vector2(-90f, -122f);
        rect.sizeDelta = new Vector2(1200f, 76f);
        root.GetComponent<Image>().color = new Color(0.035f, 0.075f, 0.12f, 0.94f);
        Outline outline = root.GetComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.8f, 1f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);
        HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
        DewGUI.ApplyLayoutGroupDefaultSettings(layout, TextAnchor.MiddleCenter);
        layout.spacing = 8f; layout.padding = new RectOffset(18, 18, 8, 8);
        AddDropdown(layout.transform, 0, "允许平台", new[] { "全部", "仅 Steam", "跨平台" });
        AddDropdown(layout.transform, 1, "排序方式", new[] { "游戏时间", "延迟", "平台" });
        AddDropdown(layout.transform, 2, "是否公开", new[] { "全部", "公开", "私密" });
        AddDropdown(layout.transform, 3, "难度", new[] { "全部难度", "Easy", "Normal", "Hard", "Nightmare", "Limbo" });
    }

    private void AddDropdown(Transform parent, int index, string labelText, string[] options)
    {
        TextMeshProUGUI label = Object.Instantiate(DewGUI.widgetTextLabel, parent);
        label.text = labelText; label.SetWidth(92f); label.SetHeight(54f);
        _dropdowns[index] = Object.Instantiate(DewGUI.widgetDropdown, parent);
        _dropdowns[index].ClearOptions();
        _dropdowns[index].AddOptions(new System.Collections.Generic.List<string>(options));
        _dropdowns[index].SetValueWithoutNotify(0); _dropdowns[index].SetWidth(170f); _dropdowns[index].SetHeight(54f);
        _dropdowns[index].onValueChanged.AddListener(_ => RefreshLobbyList());
    }

    private void RefreshLobbyList()
    {
        // 下拉条件改变时走与界面刷新按钮相同的大厅搜索流程。
        if (_view != null) _view.Refresh();
    }

    internal void Dispose()
    {
        // 销毁由本 Mod 创建的 UI，避免视图重复打开时残留控件。
        if (_root != null) Object.Destroy(_root.gameObject);
    }
}
