using System;
using System.Globalization;
using DewLanMode.config;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DewLanMode;

internal sealed class LanLobbyController : MonoBehaviour, ILangaugeChangedCallback
{
    private enum ConnectionMode
    {
        Steam,
        Lan
    }

    private UI_Title_CreateLobbyView _view;
    private UI_Dropdown _modeDropdown;
    private GameObject _modeGroup;
    private GameObject _portGroup;
    private TMP_InputField _portInput;
    private DewLocalizedText _createButtonLocalizedText;
    private string _steamCreateButtonText;
    private bool _hasSteamCreateButtonText;
    private ConnectionMode _mode;
    private bool _isVisible;
    private bool _keepRuntimeAfterHide;

    public static LanLobbyController Current { get; private set; }

    public bool IsLanSelected => _isVisible && _mode == ConnectionMode.Lan;

    public static LanLobbyController Install(UI_Title_CreateLobbyView view)
    {
        if (view == null)
        {
            return null;
        }

        LanLobbyController controller = view.GetComponent<LanLobbyController>();
        if (controller == null)
        {
            DewLanModeUiCleanup.CleanupLegacyController(view, typeof(LanLobbyController));
            DewLanModeUiCleanup.DestroyOwnedChildren(view.transform);
            controller = view.gameObject.AddComponent<LanLobbyController>();
        }

        controller.Initialize(view);
        return controller;
    }

    private void Initialize(UI_Title_CreateLobbyView view)
    {
        if (_view != null)
        {
            return;
        }

        _view = view;
        Current = this;
        _createButtonLocalizedText = _view.createLobbyButton.GetComponentInChildren<DewLocalizedText>(true);
        CreateModeField();
        CreateInputFields();
        HookOriginalFields();
        SetMode(ConnectionMode.Steam);
    }

    public void OnLanguageChanged()
    {
        if (_view == null)
        {
            return;
        }

        _modeDropdown.ClearOptions();
        _modeDropdown.AddOption(LocalizationSource.Get("LanMenu.Steam"), ConnectionMode.Steam);
        _modeDropdown.AddOption(LocalizationSource.Get("LanMenu.Lan"), ConnectionMode.Lan);
        _modeDropdown.SetValueWithoutNotify(_mode);
        SetFieldLabel(_modeGroup, _modeDropdown.transform, LocalizationSource.Get("LanMenu.Mode"));
        SetFieldLabel(_portGroup, _portInput.transform, LocalizationSource.Get("LanMenu.Port"));

        if (_createButtonLocalizedText != null)
        {
            _steamCreateButtonText = DewLocalization.GetUIValue(_createButtonLocalizedText.key);
            _hasSteamCreateButtonText = true;
        }

        if (_mode == ConnectionMode.Lan)
        {
            _view.createLobbyButton.SetText(LocalizationSource.Get("LanMenu.Host"));
        }
        else if (_hasSteamCreateButtonText)
        {
            _view.createLobbyButton.SetText(_steamCreateButtonText);
        }
    }

    public void OnViewShown()
    {
        _isVisible = true;
        _keepRuntimeAfterHide = false;
        if (!_hasSteamCreateButtonText)
        {
            _steamCreateButtonText = GetButtonText(_view.createLobbyButton);
            _hasSteamCreateButtonText = true;
        }
        SetMode(ConnectionMode.Steam);
        _portInput.text = LanModeRuntime.Port.ToString(CultureInfo.InvariantCulture);
        RefreshCreateButton();
    }

    private void OnDestroy()
    {
        if (_view != null)
        {
            _view.lobbyTypeDropdown.onValueChanged -= OnOriginalDropdownChanged;
            _view.gameModeDropdown.onValueChanged -= OnOriginalDropdownChanged;
            _view.lobbyNameInputField.onValueChanged.RemoveListener(OnLobbyNameChanged);
            _view.cancelButton.onClick.RemoveListener(LanModeRuntime.Reset);
        }

        if (Current == this)
        {
            Current = null;
        }
    }

    public void OnViewHidden()
    {
        _isVisible = false;
        _mode = ConnectionMode.Steam;
        if (!_keepRuntimeAfterHide)
        {
            LanModeRuntime.Reset();
        }
        _keepRuntimeAfterHide = false;
        _portGroup.SetActive(false);
    }

    public void RefreshCreateButton()
    {
        if (_view == null || !_isVisible || _mode != ConnectionMode.Lan)
        {
            return;
        }

        bool validPort = TryReadPort(out _);
        if (validPort)
        {
            AccessTools.Method(typeof(UI_Title_CreateLobbyView), "UpdateUIState")?.Invoke(_view, null);
        }
        else
        {
            _view.createLobbyButton.interactable = false;
        }
    }

    public bool TryPrepareHost(out string error)
    {
        if (!IsLanSelected)
        {
            error = null;
            LanModeRuntime.Reset();
            return true;
        }

        if (!TryReadPort(out int port))
        {
            error = LocalizationSource.Get("LanMenu.InvalidPort");
            return false;
        }

        bool prepared = LanModeRuntime.TryPrepareHost(port, out error);
        _keepRuntimeAfterHide = prepared;
        return prepared;
    }

    private void CreateModeField()
    {
        Transform template = _view.lobbyTypeDropdown.transform.parent.parent;
        _modeGroup = UnityEngine.Object.Instantiate(template.gameObject, template.parent);
        _modeGroup.name = DewLanModeUiCleanup.CreateModeGroupName;
        _modeGroup.transform.SetSiblingIndex(template.GetSiblingIndex());
        _modeDropdown = _modeGroup.GetComponentInChildren<UI_Dropdown>(true);
        _modeDropdown.onValueChanged = null;
        _modeDropdown.ClearOptions();
        _modeDropdown.AddOption(LocalizationSource.Get("LanMenu.Steam"), ConnectionMode.Steam);
        _modeDropdown.AddOption(LocalizationSource.Get("LanMenu.Lan"), ConnectionMode.Lan);
        _modeDropdown.onValueChanged += OnModeChanged;
        SetFieldLabel(_modeGroup, _modeDropdown.transform, LocalizationSource.Get("LanMenu.Mode"));
    }

    private void CreateInputFields()
    {
        Transform template = _view.lobbyNameObject.transform;
        _portGroup = CreateInputGroup(template, DewLanModeUiCleanup.CreatePortGroupName, LocalizationSource.Get("LanMenu.Port"), out _portInput);
        _portInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        _portInput.characterLimit = 5;
        _portInput.onValueChanged.AddListener(delegate
        {
            RefreshCreateButton();
        });
        _portGroup.transform.SetSiblingIndex(_modeGroup.transform.GetSiblingIndex() + 1);
    }

    private GameObject CreateInputGroup(Transform template, string name, string label, out TMP_InputField input)
    {
        GameObject group = UnityEngine.Object.Instantiate(template.gameObject, template.parent);
        group.name = name;
        input = group.GetComponentInChildren<TMP_InputField>(true);
        input.onValueChanged.RemoveAllListeners();
        SetFieldLabel(group, input.transform, label);
        return group;
    }

    private void HookOriginalFields()
    {
        _view.lobbyTypeDropdown.onValueChanged += OnOriginalDropdownChanged;
        _view.lobbyNameInputField.onValueChanged.AddListener(OnLobbyNameChanged);
        _view.gameModeDropdown.onValueChanged += OnOriginalDropdownChanged;
        _view.cancelButton.onClick.AddListener(LanModeRuntime.Reset);
    }

    private void OnOriginalDropdownChanged(object value)
    {
        RefreshCreateButton();
    }

    private void OnLobbyNameChanged(string value)
    {
        RefreshCreateButton();
    }

    private void OnModeChanged(object value)
    {
        SetMode((ConnectionMode)value);
    }

    private void SetMode(ConnectionMode mode)
    {
        _mode = mode;
        _modeDropdown.SetValueWithoutNotify(mode);
        bool lan = mode == ConnectionMode.Lan;
        LanModeRuntime.Reset();
        _portGroup.SetActive(lan);
        _view.lobbyTypeDropdown.transform.parent.parent.gameObject.SetActive(!lan);
        _view.lobbyNameObject.SetActive(!lan);
        if (lan)
        {
            _view.createLobbyButton.SetText(LocalizationSource.Get("LanMenu.Host"));
        }
        else if (_hasSteamCreateButtonText)
        {
            _view.createLobbyButton.SetText(_steamCreateButtonText);
        }

        if (lan)
        {
            _view.lobbyTypeDropdown.value = DewLobbyType.InviteOnly;
            if (TryReadPort(out int port))
            {
                LanModeRuntime.TryPrepareHost(port, out _);
            }
        }
        else
        {
            _view.lobbyTypeDropdown.value = DewLobbyType.Public;
        }

        RefreshCreateButton();
    }

    private bool TryReadPort(out int port)
    {
        return int.TryParse(_portInput.text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out port) && port >= 1024 && port <= 65535;
    }

    private static void SetFieldLabel(GameObject group, Transform control, string text)
    {
        foreach (TMP_Text label in group.GetComponentsInChildren<TMP_Text>(true))
        {
            if (!label.transform.IsChildOf(control))
            {
                label.text = text;
                DewLocalizedText localizedText = label.GetComponent<DewLocalizedText>();
                if (localizedText != null)
                {
                    UnityEngine.Object.Destroy(localizedText);
                }
                return;
            }
        }
    }

    private static string GetButtonText(Button button)
    {
        TMP_Text text = button == null ? null : button.GetComponentInChildren<TMP_Text>(true);
        return text == null ? string.Empty : text.text;
    }

}
