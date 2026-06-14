using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace DewIdentityChange.config;

public sealed class IdentityConfigSync
{
    private const string SyncKey = "DewIdentityChange::enable";

    private readonly Action _applyEffects;
    private readonly PluginConfig _config;
    private readonly global::DewIdentityChange.DewIdentityChange _owner;

    private bool _hasHostEnable;
    private bool _hostEnable;
    private bool _isGameConfigLocked;
    private bool _isGameSettingsSubscribed;
    private bool _isLobbySubscribed;
    private bool _lockedEnable;

    public IdentityConfigSync(
        global::DewIdentityChange.DewIdentityChange owner,
        PluginConfig config,
        Action applyEffects)
    {
        _owner = owner;
        _config = config;
        _applyEffects = applyEffects;
    }

    public bool IsIdentityEnabled
    {
        get
        {
            if (!_isGameConfigLocked && IsGameStarted())
            {
                LockGameConfig("read", applyEffects: false);
            }

            if (_isGameConfigLocked)
            {
                return _lockedEnable;
            }

            return IsConfigAuthoritative() ? _config.enable : _hasHostEnable && _hostEnable;
        }
    }

    public void LobbyManagerOnStart()
    {
        SubscribeLobbyChanged();
        SyncOrLockConfig("lobby start");
        Dew.CallDelayed(() => SyncOrLockConfig("lobby retry 1"), 30);
        Dew.CallDelayed(() => SyncOrLockConfig("lobby retry 2"), 120);
    }

    public void LobbyManagerOnStop()
    {
        UnsubscribeLobbyChanged();
    }

    public void GameSettingsManagerOnStartClient()
    {
        SubscribeGameSettingsChanged();
        SyncOrLockConfig("game settings start");
        Dew.CallDelayed(() => SyncOrLockConfig("game settings retry"), 30);
    }

    public void GameSettingsManagerOnStopClient()
    {
        UnsubscribeGameSettingsChanged();
        if (NetworkClient.active || NetworkServer.active)
        {
            return;
        }

        ResetSyncedState();
        _applyEffects();
    }

    public void OnHumanPlayerAdded(DewPlayer player)
    {
        if (!player.isLocalPlayer)
        {
            return;
        }

        Dew.CallOnReady(
            _owner,
            () => DewPlayer.local == player && !string.IsNullOrEmpty(player.selectedHeroType),
            () =>
            {
                SyncOrLockConfig("local player ready");
                _applyEffects();
            });
    }

    public void SyncOrLockConfig(string reason)
    {
        if (_isGameConfigLocked)
        {
            if (!IsInLobby())
            {
                return;
            }

            ResetSyncedState();
        }

        if (IsGameStarted())
        {
            LockGameConfig(reason);
            return;
        }

        if (IsConfigAuthoritative())
        {
            PublishHostConfig(reason);
        }
        else
        {
            ApplyHostConfig(reason);
        }
    }

    public void Dispose()
    {
        UnsubscribeLobbyChanged();
        UnsubscribeGameSettingsChanged();
    }

    private void LockGameConfig(string reason, bool applyEffects = true)
    {
        if (_isGameConfigLocked)
        {
            return;
        }

        bool hasConfig = TryResolveEnable(out _lockedEnable);
        _hostEnable = _lockedEnable;
        _hasHostEnable = true;
        _isGameConfigLocked = true;
        UnsubscribeGameSettingsChanged();

        if (!hasConfig && !IsConfigAuthoritative())
        {
            Debug.LogWarning("[DewIdentityChange] Locked without host config; default disabled: " + reason);
        }
        else
        {
            Debug.Log("[DewIdentityChange] Locked config enable=" + _lockedEnable + ": " + reason);
        }

        if (applyEffects)
        {
            _applyEffects();
        }
    }

    private void PublishHostConfig(string reason)
    {
        if (!IsConfigAuthoritative() || IsGameStarted())
        {
            return;
        }

        string value = Encode(_config.enable);
        PublishLobbyConfig(value, reason);
        PublishGameSettingsConfig(value, reason);
    }

    private void PublishLobbyConfig(string value, string reason)
    {
        var lobbyManager = ManagerBase<LobbyManager>.softInstance;
        var lobby = lobbyManager?.service?.currentLobby;
        if (lobby == null || !lobby.isLobbyLeader)
        {
            return;
        }

        var data = lobby.customData == null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(lobby.customData);

        if (data.TryGetValue(SyncKey, out string current) && current == value)
        {
            return;
        }

        data[SyncKey] = value;
        lobby.customData = data;
        lobbyManager.service.SetLobbyAttribute("customData", data);
        Debug.Log("[DewIdentityChange] Publish lobby config " + _config.enable + ": " + reason);
    }

    private void PublishGameSettingsConfig(string value, string reason)
    {
        var gameSettings = NetworkedManagerBase<GameSettingsManager>.softInstance;
        if (!NetworkServer.active || gameSettings?.customData == null || gameSettings.state != GameState.InLobby)
        {
            return;
        }

        if (gameSettings.customData.TryGetValue(SyncKey, out string current) && current == value)
        {
            return;
        }

        gameSettings.customData[SyncKey] = value;
        Debug.Log("[DewIdentityChange] Publish game settings config " + _config.enable + ": " + reason);
    }

    private void ApplyHostConfig(string reason)
    {
        if (IsConfigAuthoritative() || IsGameStarted() || !TryReadSyncedEnable(out bool enable))
        {
            return;
        }

        if (_hasHostEnable && _hostEnable == enable)
        {
            return;
        }

        _hostEnable = enable;
        _hasHostEnable = true;
        Debug.Log("[DewIdentityChange] Applied host config enable=" + enable + ": " + reason);
        _applyEffects();
    }

    private bool TryResolveEnable(out bool enable)
    {
        if (TryReadSyncedEnable(out enable))
        {
            return true;
        }

        if (_hasHostEnable)
        {
            enable = _hostEnable;
            return true;
        }

        if (IsConfigAuthoritative())
        {
            enable = _config.enable;
            return true;
        }

        enable = false;
        return false;
    }

    private bool TryReadSyncedEnable(out bool enable)
    {
        if (TryReadGameSettingsEnable(out enable))
        {
            return true;
        }

        return TryReadLobbyEnable(out enable);
    }

    private bool TryReadGameSettingsEnable(out bool enable)
    {
        var data = NetworkedManagerBase<GameSettingsManager>.softInstance?.customData;
        if (data != null && data.TryGetValue(SyncKey, out string value))
        {
            return TryDecode(value, out enable);
        }

        enable = false;
        return false;
    }

    private bool TryReadLobbyEnable(out bool enable)
    {
        var data = ManagerBase<LobbyManager>.softInstance?.service?.currentLobby?.customData;
        if (data != null && data.TryGetValue(SyncKey, out string value))
        {
            return TryDecode(value, out enable);
        }

        enable = false;
        return false;
    }

    private bool IsConfigAuthoritative()
    {
        if (NetworkServer.active)
        {
            return true;
        }

        var lobby = ManagerBase<LobbyManager>.softInstance?.service?.currentLobby;
        return lobby?.isLobbyLeader ?? !NetworkClient.active;
    }

    private bool IsGameStarted()
    {
        var gameSettings = NetworkedManagerBase<GameSettingsManager>.softInstance;
        return gameSettings != null && gameSettings.state != GameState.InLobby;
    }

    private bool IsInLobby()
    {
        var gameSettings = NetworkedManagerBase<GameSettingsManager>.softInstance;
        return gameSettings != null && gameSettings.state == GameState.InLobby;
    }

    private void OnCurrentLobbyChanged()
    {
        SyncOrLockConfig("lobby changed");
    }

    private void OnGameSettingsCustomDataChanged(string key)
    {
        if (key == SyncKey)
        {
            SyncOrLockConfig("game settings changed");
        }
    }

    private void OnGameSettingsStateChanged()
    {
        SyncOrLockConfig("game state changed");
    }

    private void SubscribeLobbyChanged()
    {
        var lobbyManager = ManagerBase<LobbyManager>.softInstance;
        if (_isLobbySubscribed || lobbyManager == null)
        {
            return;
        }

        lobbyManager.onCurrentLobbyChanged -= OnCurrentLobbyChanged;
        lobbyManager.onCurrentLobbyChanged += OnCurrentLobbyChanged;
        _isLobbySubscribed = true;
    }

    private void UnsubscribeLobbyChanged()
    {
        var lobbyManager = ManagerBase<LobbyManager>.softInstance;
        if (_isLobbySubscribed && lobbyManager != null)
        {
            lobbyManager.onCurrentLobbyChanged -= OnCurrentLobbyChanged;
        }

        _isLobbySubscribed = false;
    }

    private void SubscribeGameSettingsChanged()
    {
        var gameSettings = NetworkedManagerBase<GameSettingsManager>.softInstance;
        if (_isGameSettingsSubscribed || gameSettings == null)
        {
            return;
        }

        gameSettings.ClientEvent_OnCustomDataChanged -= OnGameSettingsCustomDataChanged;
        gameSettings.ClientEvent_OnCustomDataChanged += OnGameSettingsCustomDataChanged;
        gameSettings.ClientEvent_OnStateChanged -= OnGameSettingsStateChanged;
        gameSettings.ClientEvent_OnStateChanged += OnGameSettingsStateChanged;
        _isGameSettingsSubscribed = true;
    }

    private void UnsubscribeGameSettingsChanged()
    {
        var gameSettings = NetworkedManagerBase<GameSettingsManager>.softInstance;
        if (_isGameSettingsSubscribed && gameSettings != null)
        {
            gameSettings.ClientEvent_OnCustomDataChanged -= OnGameSettingsCustomDataChanged;
            gameSettings.ClientEvent_OnStateChanged -= OnGameSettingsStateChanged;
        }

        _isGameSettingsSubscribed = false;
    }

    private void ResetSyncedState()
    {
        _hasHostEnable = false;
        _hostEnable = false;
        _isGameConfigLocked = false;
        _lockedEnable = false;
    }

    private static string Encode(bool enable)
    {
        return enable ? "1" : "0";
    }

    private static bool TryDecode(string value, out bool enable)
    {
        if (value == "1" || value == "0")
        {
            enable = value == "1";
            return true;
        }

        return bool.TryParse(value, out enable);
    }
}
