using System.Collections.Generic;
using System.Globalization;
using DewLanMode.config;
using DewLanMode.patch;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DewLanMode;

internal sealed class ContinueModeDialogController : MonoBehaviour, ILangaugeChangedCallback
{
    private TransitionManager _transitionManager;
    private DewNetworkStartSettings _settings;
    private bool _isConfirming;
    private TMP_Text _title;
    private TMP_Text _modeLabel;
    private TMP_Dropdown _modeDropdown;
    private TMP_Text _portLabel;
    private TMP_InputField _portInput;
    private GameObject _portRow;
    private Button _confirm;
    private Button _cancel;
    private GlobalUIManager _globalUIManager;
    private BackHandler _backHandler;

    public void Initialize(UI_Window window, TransitionManager transitionManager, DewNetworkStartSettings settings)
    {
        _transitionManager = transitionManager;
        _settings = settings;
        _globalUIManager = ManagerBase<GlobalUIManager>.instance;
        if (_globalUIManager != null)
        {
            _backHandler = _globalUIManager.AddBackHandler(this, int.MaxValue, OnBack);
        }

        VerticalLayoutGroup content = DewGUI.CreateVerticalLayoutGroup(window.transform, TextAnchor.UpperCenter);
        content.name = "Continue Mode Content";
        content.padding = new RectOffset(40, 40, 32, 32);
        content.spacing = 18f;
        content.childForceExpandWidth = true;
        _title = Object.Instantiate(DewGUI.widgetTextHeader, content.transform);
        _title.alignment = TextAlignmentOptions.Center;

        HorizontalLayoutGroup modeRow = DewGUI.CreateHorizontalLayoutGroup(content.transform, TextAnchor.MiddleCenter);
        modeRow.name = "Connection Mode Row";
        modeRow.spacing = 20f;
        modeRow.SetHeight(58f);
        _modeLabel = Object.Instantiate(DewGUI.widgetTextLabel, modeRow.transform);
        _modeLabel.alignment = TextAlignmentOptions.MidlineRight;
        _modeLabel.SetWidth(300f);
        _modeDropdown = Object.Instantiate(DewGUI.widgetDropdown, modeRow.transform);
        _modeDropdown.SetWidth(280f);
        _modeDropdown.onValueChanged.AddListener(OnModeChanged);

        _portRow = DewGUI.CreateHorizontalLayoutGroup(content.transform, TextAnchor.MiddleCenter).gameObject;
        _portRow.name = "LAN Port Row";
        HorizontalLayoutGroup portLayout = _portRow.GetComponent<HorizontalLayoutGroup>();
        portLayout.spacing = 20f;
        portLayout.SetHeight(58f);
        _portLabel = Object.Instantiate(DewGUI.widgetTextLabel, _portRow.transform);
        _portLabel.alignment = TextAlignmentOptions.MidlineRight;
        _portLabel.SetWidth(300f);
        _portInput = Object.Instantiate(DewGUI.widgetInputField, _portRow.transform);
        _portInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        _portInput.characterLimit = 5;
        _portInput.SetWidth(280f);
        _portInput.SetHeight(55f);
        _portInput.text = LanModeRuntime.Port.ToString(CultureInfo.InvariantCulture);
        _portInput.onValueChanged.AddListener(delegate { RefreshConfirmButton(); });
        _portRow.SetActive(false);

        HorizontalLayoutGroup buttons = DewGUI.CreateHorizontalLayoutGroup(content.transform, TextAnchor.MiddleCenter);
        buttons.name = "Dialog Buttons";
        buttons.spacing = 20f;
        buttons.padding = new RectOffset(0, 0, 8, 0);
        _confirm = Object.Instantiate(DewGUI.widgetButton, buttons.transform);
        _confirm.SetWidth(220f);
        _confirm.SetHeight(60f);
        _cancel = Object.Instantiate(DewGUI.widgetButton, buttons.transform);
        _cancel.SetWidth(220f);
        _cancel.SetHeight(60f);
        _confirm.onClick.AddListener(Confirm);
        _cancel.onClick.AddListener(ContinueModeDialog.Close);
        OnLanguageChanged();
    }

    public void OnLanguageChanged()
    {
        if (_title == null)
        {
            return;
        }

        int selectedMode = _modeDropdown.value;
        _title.text = LocalizationSource.GetLocalizationText("LanMenu.ContinueTitle");
        _modeLabel.text = LocalizationSource.GetLocalizationText("LanMenu.Mode");
        _modeDropdown.ClearOptions();
        _modeDropdown.AddOptions(new List<string>
        {
            LocalizationSource.GetLocalizationText("LanMenu.Steam"),
            LocalizationSource.GetLocalizationText("LanMenu.Lan")
        });
        _modeDropdown.SetValueWithoutNotify(selectedMode);
        _portRow.SetActive(selectedMode == 1);
        _portLabel.text = LocalizationSource.GetLocalizationText("LanMenu.Port");
        SetButtonText(_confirm, LocalizationSource.GetLocalizationText("LanMenu.ContinueConfirm"));
        SetButtonText(_cancel, LocalizationSource.GetLocalizationText("LanMenu.ContinueCancel"));
        RefreshConfirmButton();
    }

    private void OnModeChanged(int value)
    {
        _portRow.SetActive(value == 1);
        RefreshConfirmButton();
    }

    private void RefreshConfirmButton()
    {
        _confirm.interactable = _modeDropdown.value != 1 || TryReadPort(out _);
    }

    private bool OnBack()
    {
        ContinueModeDialog.Close();
        return true;
    }

    private void OnDestroy()
    {
        if (_globalUIManager != null && _backHandler != null)
        {
            _globalUIManager.RemoveBackHandler(_backHandler);
            _backHandler = null;
        }
    }

    private void Confirm()
    {
        // 防止同一帧重复点击确认，导致同一份存档启动两次。
        if (_isConfirming || _transitionManager == null ||
            _transitionManager.state == TransitionManager.StateType.Loading)
        {
            return;
        }

        if (_modeDropdown.value == 1)
        {
            if (!TryReadPort(out int port))
            {
                ManagerBase<MessageManager>.instance.ShowMessage(new DewMessageSettings
                {
                    rawContent = LocalizationSource.GetLocalizationText("LanMenu.InvalidPort"),
                    buttons = DewMessageSettings.ButtonType.Ok
                });
                return;
            }

            if (!LanModeRuntime.TryPrepareHost(port, out string error))
            {
                ManagerBase<MessageManager>.instance.ShowMessage(new DewMessageSettings
                {
                    rawContent = error,
                    buttons = DewMessageSettings.ButtonType.Ok
                });
                return;
            }
        }
        else
        {
            LanModeRuntime.Reset();
        }

        _isConfirming = true;
        _confirm.interactable = false;
        ContinueModeDialog.Close();
        ContinueModePatch.Continue(_transitionManager, _settings);
    }

    private bool TryReadPort(out int port)
    {
        return int.TryParse(_portInput.text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out port)
               && port >= 1024 && port <= 65535;
    }

    private static void SetButtonText(Button button, string text)
    {
        button.GetComponentInChildren<TMP_Text>(true).text = text;
    }
}
