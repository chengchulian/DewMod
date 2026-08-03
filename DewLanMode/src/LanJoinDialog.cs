using System;
using System.Globalization;
using DewLanMode.config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DewLanMode;

internal sealed class LanJoinDialog : MonoBehaviour, ILangaugeChangedCallback
{
    private static LanJoinDialog _current;

    private UI_Window _window;
    private TMP_Text _title;
    private TMP_Text _description;
    private TMP_Text _addressLabel;
    private TMP_Text _portLabel;
    private TMP_InputField _addressInput;
    private TMP_InputField _portInput;
    private Button _cancelButton;
    private Button _joinButton;
    private GlobalUIManager _globalUIManager;
    private BackHandler _backHandler;
    private Action<string, int> _onJoin;

    public static void Show(Action<string, int> onJoin)
    {
        Close();

        UI_Window window = UnityEngine.Object.Instantiate(DewGUI.widgetWindow, DewGUI.canvasTransform);
        window.name = DewLanModeUiCleanup.JoinDialogName;
        LanJoinDialog dialog = window.gameObject.AddComponent<LanJoinDialog>();
        dialog.Build(window, onJoin);
        _current = dialog;
    }

    public static void Close()
    {
        if (_current != null)
        {
            _current.CloseInternal();
        }

        foreach (UI_Window window in Resources.FindObjectsOfTypeAll<UI_Window>())
        {
            if (DewLanModeUiCleanup.IsRuntimeSceneObject(window) &&
                window.name == DewLanModeUiCleanup.JoinDialogName)
            {
                UnityEngine.Object.Destroy(window.gameObject);
            }
        }
    }

    private void Build(UI_Window window, Action<string, int> onJoin)
    {
        _window = window;
        _onJoin = onJoin;
        _window.isDraggable = false;
        _window.enableBackdrop = true;
        _window.SetWidth(760f);
        _globalUIManager = ManagerBase<GlobalUIManager>.instance;
        if (_globalUIManager != null)
        {
            _backHandler = _globalUIManager.AddBackHandler(this, int.MaxValue, OnBack);
        }

        VerticalLayoutGroup root = DewGUI.CreateVerticalLayoutGroup(_window.transform, TextAnchor.UpperLeft);
        root.name = "Join Dialog Content";
        root.padding = new RectOffset(42, 42, 34, 24);
        root.spacing = 18f;
        root.childForceExpandWidth = true;

        _title = UnityEngine.Object.Instantiate(DewGUI.widgetTextHeader, root.transform);
        _title.alignment = TextAlignmentOptions.Center;
        _title.SetExpandWidth(true);

        _description = UnityEngine.Object.Instantiate(DewGUI.widgetTextBody, root.transform);
        _description.alignment = TextAlignmentOptions.Center;
        _description.color = Color.Lerp(_description.color, Color.white, 0.45f);
        _description.SetExpandWidth(true);

        _addressInput = CreateLabeledInput(root.transform, out _addressLabel);
        _portInput = CreateLabeledInput(root.transform, out _portLabel);
        _portInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        _portInput.characterLimit = 5;
        _portInput.text = LanModeRuntime.Port.ToString(CultureInfo.InvariantCulture);

        HorizontalLayoutGroup buttons = DewGUI.CreateHorizontalLayoutGroup(root.transform, TextAnchor.MiddleCenter);
        buttons.name = "Dialog Buttons";
        buttons.spacing = 24f;
        buttons.SetExpandWidth(true);

        _cancelButton = UnityEngine.Object.Instantiate(DewGUI.widgetButton, buttons.transform);
        _joinButton = UnityEngine.Object.Instantiate(DewGUI.widgetButton, buttons.transform);
        _cancelButton.SetWidth(220f).SetHeight(62f);
        _joinButton.SetWidth(280f).SetHeight(62f);

        _cancelButton.onClick.AddListener(CloseInternal);
        _joinButton.onClick.AddListener(Confirm);
        _addressInput.onSubmit.AddListener(delegate { Confirm(); });
        _portInput.onSubmit.AddListener(delegate { Confirm(); });
        OnLanguageChanged();
        Dew.CallDelayed(_addressInput.ActivateInputField);
    }

    public void OnLanguageChanged()
    {
        if (_window == null)
        {
            return;
        }

        _title.text = LocalizationSource.Get("LanMenu.JoinByIpTitle");
        _description.text = LocalizationSource.Get("LanMenu.JoinByIpDescription");
        _addressLabel.text = LocalizationSource.Get("LanMenu.Address");
        _portLabel.text = LocalizationSource.Get("LanMenu.Port");
        _addressInput.placeholder.GetComponent<TMP_Text>().text = LocalizationSource.Get("LanMenu.AddressPlaceholder");
        _portInput.placeholder.GetComponent<TMP_Text>().text = LocalizationSource.Get("LanMenu.PortPlaceholder");
        SetButtonText(_cancelButton, LocalizationSource.Get("LanMenu.Cancel"));
        SetButtonText(_joinButton, LocalizationSource.Get("LanMenu.Join"));
    }

    private static TMP_InputField CreateLabeledInput(Transform parent, out TMP_Text label)
    {
        HorizontalLayoutGroup row = DewGUI.CreateHorizontalLayoutGroup(parent, TextAnchor.MiddleLeft);
        row.spacing = 18f;
        row.SetExpandWidth(true);

        label = UnityEngine.Object.Instantiate(DewGUI.widgetTextLabel, row.transform);
        label.fontStyle |= FontStyles.Bold;
        label.alignment = TextAlignmentOptions.MidlineRight;
        label.SetWidth(150f).SetHeight(60f);

        TMP_InputField input = UnityEngine.Object.Instantiate(DewGUI.widgetInputField, row.transform);
        input.SetExpandWidth(1f).SetHeight(60f);
        return input;
    }

    private bool OnBack()
    {
        CloseInternal();
        return true;
    }

    private void Confirm()
    {
        if (!int.TryParse(_portInput.text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int port) ||
            port < 1024 || port > 65535)
        {
            FindLobbyController.ShowError(LocalizationSource.Get("LanMenu.InvalidPort"));
            return;
        }

        string address = _addressInput.text.Trim();
        if (string.IsNullOrEmpty(address))
        {
            FindLobbyController.ShowError(LocalizationSource.Get("LanMenu.InvalidAddress"));
            return;
        }

        Action<string, int> onJoin = _onJoin;
        CloseInternal();
        onJoin?.Invoke(address, port);
    }

    private void CloseInternal()
    {
        if (_current == this)
        {
            _current = null;
        }

        if (_window != null)
        {
            UnityEngine.Object.Destroy(_window.gameObject);
        }
    }

    private void OnDestroy()
    {
        if (_globalUIManager != null && _backHandler != null)
        {
            _globalUIManager.RemoveBackHandler(_backHandler);
            _backHandler = null;
        }

        if (_current == this)
        {
            _current = null;
        }
    }

    private static void SetButtonText(Button button, string value)
    {
        button.GetComponentInChildren<TMP_Text>(true).text = value;
        foreach (DewLocalizedText localizedText in button.GetComponentsInChildren<DewLocalizedText>(true))
        {
            UnityEngine.Object.Destroy(localizedText);
        }
    }
}
