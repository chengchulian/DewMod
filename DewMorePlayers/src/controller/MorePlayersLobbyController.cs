using System.Globalization;
using System.Reflection;
using DewMorePlayers.config;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace DewMorePlayers;

internal sealed class MorePlayersLobbyController : MonoBehaviour, ILangaugeChangedCallback
{
    private static readonly FieldInfo StateField = AccessTools.Field(typeof(UI_Title_CreateLobbyView), "_state");

    private UI_Title_CreateLobbyView _view;
    private GameObject _maxPlayersGroup;
    private TMP_InputField _maxPlayersInput;
    private bool _isVisible;

    public static MorePlayersLobbyController Install(UI_Title_CreateLobbyView view)
    {
        if (view == null)
        {
            return null;
        }

        MorePlayersLobbyController controller = view.GetComponent<MorePlayersLobbyController>();
        if (controller == null)
        {
            DewMorePlayersUiCleanup.CleanupLegacyController(view, typeof(MorePlayersLobbyController));
            DewMorePlayersUiCleanup.DestroyOwnedChildren(view.transform);
            controller = view.gameObject.AddComponent<MorePlayersLobbyController>();
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
        Transform template = view.lobbyNameObject.transform;
        _maxPlayersGroup = Object.Instantiate(template.gameObject, template.parent);
        _maxPlayersGroup.name = DewMorePlayersUiCleanup.MaxPlayersGroupName;
        _maxPlayersGroup.transform.SetSiblingIndex(template.GetSiblingIndex() + 1);

        _maxPlayersInput = _maxPlayersGroup.GetComponentInChildren<TMP_InputField>(true);
        _maxPlayersInput.onValueChanged.RemoveAllListeners();
        _maxPlayersInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        _maxPlayersInput.characterLimit = 2;
        OnLanguageChanged();

        _maxPlayersInput.onValueChanged.AddListener(OnValueChanged);
        _maxPlayersInput.onEndEdit.AddListener(OnEndEdit);
        _view.onHide.AddListener(OnViewHidden);

        Debug.Log("[DewMorePlayers] Lobby player-count field installed.");
    }

    public void OnViewShown()
    {
        _isVisible = true;
        int maxPlayers = DewMorePlayers.MaxPlayers;
        SetMaxPlayers(maxPlayers);
        _maxPlayersInput.SetTextWithoutNotify(maxPlayers.ToString(CultureInfo.InvariantCulture));
        _maxPlayersGroup.SetActive(true);
        RefreshCreateButton();
        Debug.Log($"[DewMorePlayers] Lobby player-count field shown: {maxPlayers}.");
    }

    public void OnViewHidden()
    {
        _isVisible = false;
    }

    public void OnLanguageChanged()
    {
        if (_maxPlayersGroup == null || _maxPlayersInput == null)
        {
            return;
        }

        SetFieldLabel(_maxPlayersGroup, _maxPlayersInput.transform,
            LocalizationSource.GetLocalizationText("LobbyMenu.MaxPlayers"));
    }

    private void OnDestroy()
    {
        if (_view != null)
        {
            _view.onHide.RemoveListener(OnViewHidden);
        }
    }

    public void RefreshCreateButton()
    {
        if (_view == null || !_isVisible)
        {
            return;
        }

        if (!TryReadMaxPlayers(out _))
        {
            _view.createLobbyButton.interactable = false;
        }
    }

    private void OnValueChanged(string _)
    {
        if (TryReadMaxPlayers(out int maxPlayers))
        {
            SetMaxPlayers(maxPlayers);
        }

        RefreshOriginalUIState();
    }

    private void OnEndEdit(string unusedValue)
    {
        if (TryReadMaxPlayers(out _))
        {
            return;
        }

        int maxPlayers = DewMorePlayers.MaxPlayers;
        _maxPlayersInput.SetTextWithoutNotify(maxPlayers.ToString(CultureInfo.InvariantCulture));
        SetMaxPlayers(maxPlayers);
        RefreshOriginalUIState();
    }

    private void SetMaxPlayers(int maxPlayers)
    {
        DewNetworkStartSettings state = StateField?.GetValue(_view) as DewNetworkStartSettings;
        if (state != null)
        {
            state.maxPlayers = maxPlayers;
        }

        DewMorePlayers.SetMaxPlayers(maxPlayers);
    }

    private bool TryReadMaxPlayers(out int maxPlayers)
    {
        return int.TryParse(_maxPlayersInput.text.Trim(), NumberStyles.Integer,
                   CultureInfo.InvariantCulture, out maxPlayers)
               && maxPlayers >= Constant.MinPlayerClamp
               && maxPlayers <= Constant.MaxPlayerClamp;
    }

    private void RefreshOriginalUIState()
    {
        AccessTools.Method(typeof(UI_Title_CreateLobbyView), "UpdateUIState")?.Invoke(_view, null);
    }

    private static void SetFieldLabel(GameObject group, Transform control, string text)
    {
        foreach (TMP_Text label in group.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label.transform.IsChildOf(control))
            {
                continue;
            }

            label.text = text;
            DewLocalizedText localizedText = label.GetComponent<DewLocalizedText>();
            if (localizedText != null)
            {
                Object.Destroy(localizedText);
            }

            return;
        }
    }
}
