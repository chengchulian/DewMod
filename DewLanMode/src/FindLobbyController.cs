using System.Collections.Generic;
using System.Globalization;
using DewLanMode.config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DewLanMode;

internal sealed class FindLobbyController : MonoBehaviour, ILangaugeChangedCallback
{
    private enum ConnectionMode
    {
        Steam,
        Lan
    }

    private UI_Title_FindLobbyView _view;
    private GameObject _modeSelector;
    private TMP_Dropdown _modeDropdown;
    private TextMeshProUGUI _modeLabel;
    private Button _joinByIpButton;
    private Button _joinWithLobbyIdButton;
    private TextMeshProUGUI _lanHint;
    private readonly List<LanDiscoveredLobby> _lanLobbies = new List<LanDiscoveredLobby>();
    private ConnectionMode _mode;
    private bool _isVisible;

    public static FindLobbyController Current { get; private set; }

    public bool IsLanSelected => _isVisible && _mode == ConnectionMode.Lan;

    public static FindLobbyController Install(UI_Title_FindLobbyView view)
    {
        if (view == null)
        {
            return null;
        }

        FindLobbyController controller = view.GetComponent<FindLobbyController>();
        if (controller == null)
        {
            DewLanModeUiCleanup.CleanupLegacyController(view, typeof(FindLobbyController));
            DewLanModeUiCleanup.DestroyOwnedChildren(view.transform);
            controller = view.gameObject.AddComponent<FindLobbyController>();
        }

        controller.Initialize(view);
        return controller;
    }

    private void Initialize(UI_Title_FindLobbyView view)
    {
        if (_view != null)
        {
            return;
        }

        _view = view;
        Current = this;
        CreateModeSelector();
        CreateJoinByIpButton();
        CreateLanHint();
        SetMode(ConnectionMode.Steam, false);
    }

    public void OnLanguageChanged()
    {
        if (_view == null)
        {
            return;
        }

        _modeLabel.SetText(LocalizationSource.Get("LanMenu.RoomSource"));
        _modeDropdown.ClearOptions();
        _modeDropdown.AddOptions(new List<string>
        {
            LocalizationSource.Get("LanMenu.SteamRoom"),
            LocalizationSource.Get("LanMenu.LanRoom")
        });
        _modeDropdown.SetValueWithoutNotify(_mode == ConnectionMode.Steam ? 0 : 1);
        SetButtonText(_joinByIpButton, LocalizationSource.Get("LanMenu.JoinByIp"));
        PositionJoinByIpButton();
        Dew.CallDelayed(PositionJoinByIpButton);

        if (_view.loadingObject.activeSelf)
        {
            _lanHint.SetText(LocalizationSource.Get("LanMenu.Searching"));
        }
        else
        {
            _lanHint.SetText(LocalizationSource.Get(_lanLobbies.Count > 0
                ? "LanMenu.LanListHint"
                : "LanMenu.NoLanRooms"));
        }
    }

    public void PrepareForShow()
    {
        _isVisible = true;
        SetMode(ConnectionMode.Steam, false);
    }

    public void OnViewHidden()
    {
        _isVisible = false;
        LanDiscoveryService.Instance?.CancelSearch();
        _mode = ConnectionMode.Steam;
        if (_modeDropdown != null)
        {
            _modeDropdown.SetValueWithoutNotify(0);
        }
    }

    public void RefreshLanLobbies()
    {
        if (!IsLanSelected)
        {
            return;
        }

        _lanLobbies.Clear();
        ClearLobbyItems();
        _view.itemGroup.currentIndex = -1;
        _view.joinButton.interactable = false;
        _view.loadingObject.SetActive(true);
        _view.emptyObject.SetActive(false);
        _lanHint.SetText(LocalizationSource.Get("LanMenu.Searching"));
        _lanHint.gameObject.SetActive(true);

        LanDiscoveryService discovery = LanDiscoveryService.Instance;
        if (discovery == null)
        {
            OnLanSearchCompleted(new List<LanDiscoveredLobby>());
            return;
        }

        discovery.Search(OnLanSearchCompleted);
    }

    public void HandleSelectionChanged()
    {
        if (IsLanSelected)
        {
            int index = _view.itemGroup.currentIndex;
            _view.joinButton.interactable = index >= 0 && index < _lanLobbies.Count;
        }
    }

    public void JoinSelectedLanLobby()
    {
        if (!IsLanSelected)
        {
            return;
        }

        int index = _view.itemGroup.currentIndex;
        if (index < 0 || index >= _lanLobbies.Count)
        {
            return;
        }

        LanDiscoveredLobby lobby = _lanLobbies[index];
        JoinLanLobby(lobby.Address, lobby.Port);
    }

    private void Update()
    {
        if (IsLanSelected)
        {
            HandleSelectionChanged();
        }
    }

    private void OnDestroy()
    {
        LanDiscoveryService.Instance?.CancelSearch();
        if (Current == this)
        {
            Current = null;
        }
    }

    private void CreateModeSelector()
    {
        _modeSelector = new GameObject(DewLanModeUiCleanup.FindModeSelectorName, typeof(RectTransform), typeof(Image),
            typeof(HorizontalLayoutGroup), typeof(Outline));
        _modeSelector.transform.SetParent(_view.transform, false);

        RectTransform selectorRect = (RectTransform)_modeSelector.transform;
        selectorRect.anchorMin = new Vector2(1f, 1f);
        selectorRect.anchorMax = new Vector2(1f, 1f);
        selectorRect.pivot = new Vector2(1f, 1f);
        selectorRect.anchoredPosition = new Vector2(-90f, -38f);
        selectorRect.sizeDelta = new Vector2(520f, 76f);

        Image background = _modeSelector.GetComponent<Image>();
        background.color = new Color(0.035f, 0.075f, 0.12f, 0.94f);
        Outline outline = _modeSelector.GetComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.8f, 1f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        HorizontalLayoutGroup layout = _modeSelector.GetComponent<HorizontalLayoutGroup>();
        DewGUI.ApplyLayoutGroupDefaultSettings(layout, TextAnchor.MiddleCenter);
        layout.padding = new RectOffset(18, 18, 8, 8);
        layout.spacing = 18f;

        _modeLabel = UnityEngine.Object.Instantiate(DewGUI.widgetTextLabel, layout.transform);
        _modeLabel.name = "Room Source Label";
        _modeLabel.SetText(LocalizationSource.Get("LanMenu.RoomSource"));
        _modeLabel.fontStyle |= FontStyles.Bold;
        _modeLabel.color = new Color(0.55f, 0.88f, 1f);
        _modeLabel.alignment = TextAlignmentOptions.MidlineRight;
        _modeLabel.SetWidth(165f);
        _modeLabel.SetHeight(58f);

        _modeDropdown = UnityEngine.Object.Instantiate(DewGUI.widgetDropdown, layout.transform);
        _modeDropdown.name = "Room Source Dropdown";
        _modeDropdown.ClearOptions();
        _modeDropdown.AddOptions(new List<string>
        {
            LocalizationSource.Get("LanMenu.SteamRoom"),
            LocalizationSource.Get("LanMenu.LanRoom")
        });
        _modeDropdown.SetValueWithoutNotify(0);
        _modeDropdown.SetWidth(290f);
        _modeDropdown.SetHeight(60f);
        _modeDropdown.onValueChanged.AddListener(OnModeDropdownChanged);
    }

    private void OnModeDropdownChanged(int index)
    {
        SetMode(index == 0 ? ConnectionMode.Steam : ConnectionMode.Lan, true);
    }

    private void CreateJoinByIpButton()
    {
        _joinWithLobbyIdButton = FindJoinWithLobbyIdButton();
        if (_joinWithLobbyIdButton != null)
        {
            _joinByIpButton = UnityEngine.Object.Instantiate(_joinWithLobbyIdButton,
                _joinWithLobbyIdButton.transform.parent);
            RectTransform templateRect = (RectTransform)_joinWithLobbyIdButton.transform;
            RectTransform rect = (RectTransform)_joinByIpButton.transform;
            rect.anchorMin = templateRect.anchorMin;
            rect.anchorMax = templateRect.anchorMax;
            rect.pivot = templateRect.pivot;
            rect.anchoredPosition = templateRect.anchoredPosition;
            rect.sizeDelta = templateRect.sizeDelta;
        }
        else
        {
            _joinByIpButton = UnityEngine.Object.Instantiate(DewGUI.widgetButton, _view.transform);
            RectTransform rect = (RectTransform)_joinByIpButton.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-1042f, 84f);
        }

        _joinByIpButton.name = DewLanModeUiCleanup.JoinByIpButtonName;
        _joinByIpButton.onClick = new Button.ButtonClickedEvent();
        _joinByIpButton.SetWidth(260f);
        _joinByIpButton.SetHeight(60f);
        SetButtonText(_joinByIpButton, LocalizationSource.Get("LanMenu.JoinByIp"));
        _joinByIpButton.onClick.AddListener(OpenIpJoinWindow);
        PositionJoinByIpButton();
        Dew.CallDelayed(PositionJoinByIpButton);
    }

    private void PositionJoinByIpButton()
    {
        if (_joinByIpButton == null || _joinWithLobbyIdButton == null)
        {
            return;
        }

        const float gap = 80f;
        RectTransform lobbyIdRect = (RectTransform)_joinWithLobbyIdButton.transform;
        RectTransform ipRect = (RectTransform)_joinByIpButton.transform;
        LayoutRebuilder.ForceRebuildLayoutImmediate(lobbyIdRect);
        LayoutRebuilder.ForceRebuildLayoutImmediate(ipRect);
        float lobbyIdWidth = Mathf.Max(lobbyIdRect.rect.width, LayoutUtility.GetPreferredWidth(lobbyIdRect));
        ipRect.anchoredPosition = lobbyIdRect.anchoredPosition + new Vector2(-lobbyIdWidth - gap, 0f);
    }

    private void CreateLanHint()
    {
        _lanHint = UnityEngine.Object.Instantiate(DewGUI.widgetTextBody, _view.transform);
        _lanHint.name = DewLanModeUiCleanup.LanHintName;
        _lanHint.SetText(LocalizationSource.Get("LanMenu.Searching"));
        _lanHint.raycastTarget = false;
        _lanHint.alignment = TextAlignmentOptions.Center;
        RectTransform rect = (RectTransform)_lanHint.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(900f, 100f);
        _lanHint.gameObject.SetActive(false);
    }

    private Button FindJoinWithLobbyIdButton()
    {
        foreach (Button button in _view.GetComponentsInChildren<Button>(true))
        {
            if (button.gameObject.name == "Join With Lobby ID")
            {
                return button;
            }
        }

        return null;
    }

    private void SetMode(ConnectionMode mode, bool refresh)
    {
        _mode = mode;
        _modeDropdown.SetValueWithoutNotify(mode == ConnectionMode.Steam ? 0 : 1);
        bool lan = mode == ConnectionMode.Lan;
        LanDiscoveryService.Instance?.CancelSearch();
        LanModeRuntime.Reset();

        ClearLobbyItems();
        _view.itemParent.gameObject.SetActive(true);
        _view.loadingObject.SetActive(false);
        _view.emptyObject.SetActive(false);
        _view.refreshButton.gameObject.SetActive(true);
        _view.refreshButton.interactable = true;
        _view.joinButton.gameObject.SetActive(true);
        _view.joinButton.interactable = false;
        _lanHint.gameObject.SetActive(false);

        if (lan)
        {
            RefreshLanLobbies();
        }
        else if (refresh && _isVisible && !ManagerBase<LobbyManager>.instance.service.isRefreshingLobby)
        {
            _view.Refresh();
        }
    }

    private void OnLanSearchCompleted(IReadOnlyList<LanDiscoveredLobby> lobbies)
    {
        if (this == null || !IsLanSelected)
        {
            return;
        }

        _view.loadingObject.SetActive(false);
        _view.refreshButton.interactable = true;
        _lanLobbies.Clear();
        _lanLobbies.AddRange(lobbies);
        ClearLobbyItems();

        for (int index = 0; index < _lanLobbies.Count; index++)
        {
            LanDiscoveredLobby discovered = _lanLobbies[index];
            UI_Title_FindLobby_LobbyItem item = UnityEngine.Object.Instantiate(_view.itemPrefab, _view.itemParent);
            item.onDoubleClick = JoinSelectedLanLobby;
            item.Setup(discovered.ToLobbyInstance(), index);
        }

        bool hasRooms = _lanLobbies.Count > 0;
        _lanHint.SetText(LocalizationSource.Get(hasRooms ? "LanMenu.LanListHint" : "LanMenu.NoLanRooms"));
        _lanHint.gameObject.SetActive(!hasRooms);
        _view.itemGroup.currentIndex = hasRooms ? 0 : -1;
        HandleSelectionChanged();
    }

    private void ClearLobbyItems()
    {
        for (int index = _view.itemParent.childCount - 1; index >= 0; index--)
        {
            UnityEngine.Object.Destroy(_view.itemParent.GetChild(index).gameObject);
        }
    }

    private void OpenIpJoinWindow()
    {
        LanJoinDialog.Show(JoinLanLobby);
    }

    private void JoinLanLobby(string address, int port)
    {
        if (!LanModeRuntime.TryPrepareJoin(address, port, out string error))
        {
            ShowError(error);
            return;
        }

        ManagerBase<TransitionManager>.instance.PlayGame(new DewNetworkStartSettings
        {
            networkMode = DewNetworkMode.MultiplayerJoinLobby,
            address = address.Trim()
        });
    }

    private static void SetButtonText(Button button, string value)
    {
        button.SetText(value);
        foreach (DewLocalizedText localizedText in button.GetComponentsInChildren<DewLocalizedText>(true))
        {
            UnityEngine.Object.Destroy(localizedText);
        }
    }

    internal static void ShowError(string message)
    {
        ManagerBase<MessageManager>.instance.ShowMessage(new DewMessageSettings
        {
            rawContent = message,
            buttons = DewMessageSettings.ButtonType.Ok
        });
    }
}
