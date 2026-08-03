using System.Globalization;
using DewMorePlayers.config;
using DewMorePlayers.patch;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DewMorePlayers;

internal static class ContinuePlayerCountDialog
{
    private static ContinuePlayerCountDialogController _controller;

    public static void Show(TitleManager titleManager)
    {
        if (_controller != null)
        {
            _controller.transform.SetAsLastSibling();
            _controller.FocusInput();
            return;
        }

        UI_Window window = Object.Instantiate(DewGUI.widgetWindow, DewGUI.canvasTransform);
        window.name = DewMorePlayersUiCleanup.ContinueDialogName;
        window.isDraggable = false;
        window.enableBackdrop = true;
        window.SetWidth(760f);

        _controller = window.gameObject.AddComponent<ContinuePlayerCountDialogController>();
        _controller.Initialize(window, titleManager);
        Debug.Log($"[DewMorePlayers] Continue player-count dialog shown: {DewMorePlayers.MaxPlayers}.");
    }

    public static void Close()
    {
        if (_controller == null)
        {
            return;
        }

        Object.Destroy(_controller.gameObject);
        _controller = null;
    }
}

internal sealed class ContinuePlayerCountDialogController : MonoBehaviour, ILangaugeChangedCallback
{
    private TitleManager _titleManager;
    private TMP_Text _title;
    private TMP_Text _description;
    private TMP_Text _playerCountLabel;
    private TMP_InputField _input;
    private Button _confirm;
    private Button _cancel;
    private GlobalUIManager _globalUIManager;
    private BackHandler _backHandler;

    public void Initialize(UI_Window window, TitleManager titleManager)
    {
        _titleManager = titleManager;
        _globalUIManager = ManagerBase<GlobalUIManager>.instance;
        if (_globalUIManager != null)
        {
            _backHandler = _globalUIManager.AddBackHandler(this, int.MaxValue, OnBack);
        }

        VerticalLayoutGroup content = DewGUI.CreateVerticalLayoutGroup(window.transform, TextAnchor.UpperCenter);
        content.name = "Dialog Content";
        content.padding = new RectOffset(40, 40, 32, 32);
        content.spacing = 18f;
        content.childForceExpandWidth = true;

        _title = Object.Instantiate(DewGUI.widgetTextHeader, content.transform);
        _title.alignment = TextAlignmentOptions.Center;
        _description = Object.Instantiate(DewGUI.widgetTextBody, content.transform);
        _description.alignment = TextAlignmentOptions.Center;
        _description.SetHeight(72f);

        HorizontalLayoutGroup inputRow = DewGUI.CreateHorizontalLayoutGroup(content.transform, TextAnchor.MiddleCenter);
        inputRow.name = "Player Count Row";
        inputRow.spacing = 20f;
        inputRow.SetHeight(58f);

        _playerCountLabel = Object.Instantiate(DewGUI.widgetTextLabel, inputRow.transform);
        _playerCountLabel.alignment = TextAlignmentOptions.MidlineRight;
        _playerCountLabel.SetWidth(300f);

        _input = Object.Instantiate(DewGUI.widgetInputField, inputRow.transform);
        _input.contentType = TMP_InputField.ContentType.IntegerNumber;
        _input.characterLimit = 2;
        _input.SetWidth(180f);
        _input.SetHeight(55f);
        _input.text = DewMorePlayers.MaxPlayers.ToString(CultureInfo.InvariantCulture);

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

        _confirm.interactable = TryReadMaxPlayers(out _);
        _input.onValueChanged.AddListener(delegate
        {
            _confirm.interactable = TryReadMaxPlayers(out _);
        });
        _confirm.onClick.AddListener(Confirm);
        _cancel.onClick.AddListener(ContinuePlayerCountDialog.Close);

        OnLanguageChanged();
        FocusInput();
    }

    public void FocusInput()
    {
        if (_input != null)
        {
            Dew.CallDelayed(_input.ActivateInputField);
        }
    }

    public void OnLanguageChanged()
    {
        if (_title == null)
        {
            return;
        }

        _title.text = LocalizationSource.GetLocalizationText("ContinueDialog.Title");
        _description.text = LocalizationSource.GetLocalizationText("ContinueDialog.Description");
        _playerCountLabel.text = LocalizationSource.GetLocalizationText("ContinueDialog.PlayerCount");
        _input.placeholder.GetComponent<TMP_Text>().text =
            LocalizationSource.GetLocalizationText("ContinueDialog.Placeholder");
        SetButtonText(_confirm, LocalizationSource.GetLocalizationText("ContinueDialog.Confirm"));
        SetButtonText(_cancel, LocalizationSource.GetLocalizationText("ContinueDialog.Cancel"));
    }

    private bool OnBack()
    {
        ContinuePlayerCountDialog.Close();
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
        if (!TryReadMaxPlayers(out int maxPlayers))
        {
            return;
        }

        TitleManager titleManager = _titleManager;
        DewMorePlayers.SetMaxPlayers(maxPlayers);
        ContinuePlayerCountDialog.Close();
        TitleManager_EnterContinueDreaming_Patch.Continue(titleManager);
    }

    private bool TryReadMaxPlayers(out int maxPlayers)
    {
        return int.TryParse(_input.text.Trim(), NumberStyles.Integer,
                   CultureInfo.InvariantCulture, out maxPlayers)
               && maxPlayers >= Constant.MinPlayerClamp
               && maxPlayers <= Constant.MaxPlayerClamp;
    }

    private static void SetButtonText(Button button, string text)
    {
        button.GetComponentInChildren<TMP_Text>(true).text = text;
    }
}
