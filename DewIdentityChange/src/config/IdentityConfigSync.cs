using System;
using System.Collections.Generic;
using UnityEngine;

namespace DewIdentityChange.config;

[Serializable]
public sealed class IdentityConfigSyncSnapshot
{
    public const int CurrentProtocolVersion = 2;

    public int ProtocolVersion = CurrentProtocolVersion;
    public uint Revision;
    public bool Enable;
    public bool AddCharacterSkillsToLoot;
}

public sealed class IdentityConfigSync
{
    private const string SyncKey = "DewIdentityChange::config:v2";

    private readonly Action _applyEffects;
    private readonly PluginConfig _config;
    private readonly global::DewIdentityChange.DewIdentityChange _owner;

    private bool _hasLobbyConfig;
    private bool _isGameConfigLocked;
    private bool _lobbyEnable;
    private bool _lobbyAddCharacterSkillsToLoot;
    private bool _lockedEnable;
    private bool _lockedAddCharacterSkillsToLoot;
    private bool _isLobbySubscribed;
    private string _currentLobbyId;
    private string _lastAppliedPayload;
    private uint _lastAppliedRevision;
    private uint _publishedRevision;

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
            LobbyInstance lobby = GetCurrentLobby();
            if (lobby == null)
            {
                return _config.enable;
            }

            EnsureCurrentLobby(lobby);
            EnsureGameConfigLocked(lobby, "enable read");
            if (_isGameConfigLocked)
            {
                return _lockedEnable;
            }

            ApplyLobbyConfig(lobby, "enable read");
            return _hasLobbyConfig
                ? _lobbyEnable
                : lobby.isLobbyLeader && _config.enable;
        }
    }

    public bool IsCharacterSkillLootEnabled
    {
        get
        {
            LobbyInstance lobby = GetCurrentLobby();
            if (lobby == null)
            {
                return _config.addCharacterSkillsToLoot;
            }

            EnsureCurrentLobby(lobby);
            EnsureGameConfigLocked(lobby, "shop config read");
            if (_isGameConfigLocked)
            {
                return _lockedAddCharacterSkillsToLoot;
            }

            ApplyLobbyConfig(lobby, "shop config read");
            return _hasLobbyConfig
                ? _lobbyAddCharacterSkillsToLoot
                : lobby.isLobbyLeader && _config.addCharacterSkillsToLoot;
        }
    }

    public void LobbyManagerOnStart()
    {
        SubscribeLobbyChanged();
        SyncOrLockConfig("lobby manager start");
        Dew.CallDelayed(() => RefreshFromLobby("lobby retry 1"), 30);
        Dew.CallDelayed(() => RefreshFromLobby("lobby retry 2"), 120);
    }

    public void LobbyManagerOnStop()
    {
        UnsubscribeLobbyChanged();
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
            () => RefreshFromLobby("local player ready"));
    }

    public void SyncOrLockConfig(string reason)
    {
        LobbyInstance lobby = GetCurrentLobby();
        if (lobby == null)
        {
            ResetSyncedState();
            return;
        }

        EnsureCurrentLobby(lobby);

        if (_isGameConfigLocked && !IsRunStarted(lobby))
        {
            _isGameConfigLocked = false;
            _lockedEnable = false;
            _lockedAddCharacterSkillsToLoot = false;
        }

        if (IsRunStarted(lobby))
        {
            LockGameConfig(lobby, reason);
            return;
        }

        if (lobby.isLobbyLeader)
        {
            PublishLobbyConfig(lobby, reason);
        }
        else
        {
            ApplyLobbyConfig(lobby, reason);
        }
    }

    public void Dispose()
    {
        UnsubscribeLobbyChanged();
    }

    private void OnCurrentLobbyChanged()
    {
        RefreshFromLobby("current lobby changed");
    }

    private void RefreshFromLobby(string reason)
    {
        SyncOrLockConfig(reason);
        _applyEffects();
    }

    private void SubscribeLobbyChanged()
    {
        LobbyManager lobbyManager = ManagerBase<LobbyManager>.softInstance;
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
        LobbyManager lobbyManager = ManagerBase<LobbyManager>.softInstance;
        if (_isLobbySubscribed && lobbyManager != null)
        {
            lobbyManager.onCurrentLobbyChanged -= OnCurrentLobbyChanged;
        }

        _isLobbySubscribed = false;
    }

    private void PublishLobbyConfig(LobbyInstance lobby, string reason)
    {
        LobbyManager lobbyManager = ManagerBase<LobbyManager>.softInstance;
        if (lobbyManager?.service == null || !lobby.isLobbyLeader || IsRunStarted(lobby))
        {
            return;
        }

        string currentPayload = null;
        lobby.customData?.TryGetValue(SyncKey, out currentPayload);
        if (TryDeserialize(currentPayload, out IdentityConfigSyncSnapshot current, out _) &&
            current.Enable == _config.enable &&
            current.AddCharacterSkillsToLoot == _config.addCharacterSkillsToLoot)
        {
            _publishedRevision = Math.Max(_publishedRevision, current.Revision);
            ApplySnapshot(currentPayload, current, reason);
            return;
        }

        var snapshot = new IdentityConfigSyncSnapshot
        {
            Revision = ++_publishedRevision,
            Enable = _config.enable,
            AddCharacterSkillsToLoot = _config.addCharacterSkillsToLoot
        };
        string payload = JsonUtility.ToJson(snapshot);
        var data = lobby.customData == null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(lobby.customData);
        data[SyncKey] = payload;

        lobby.customData = data;
        ApplySnapshot(payload, snapshot, reason);
        lobbyManager.service.SetLobbyAttribute("customData", data);

        Debug.Log(
            "[DewIdentityChange] Published lobby customData config r" + snapshot.Revision +
            " enable=" + snapshot.Enable +
            ", addCharacterSkillsToLoot=" + snapshot.AddCharacterSkillsToLoot +
            ": " + reason);
    }

    private bool ApplyLobbyConfig(LobbyInstance lobby, string reason, bool force = false)
    {
        if (lobby?.customData == null ||
            !lobby.customData.TryGetValue(SyncKey, out string payload))
        {
            return false;
        }

        if (!force && payload == _lastAppliedPayload)
        {
            return _hasLobbyConfig;
        }

        if (!TryDeserialize(payload, out IdentityConfigSyncSnapshot snapshot, out string error))
        {
            Debug.LogWarning("[DewIdentityChange] Ignored invalid lobby customData config: " + error);
            return false;
        }

        if (!force && _hasLobbyConfig && snapshot.Revision < _lastAppliedRevision)
        {
            return true;
        }

        ApplySnapshot(payload, snapshot, reason);
        return true;
    }

    private void ApplySnapshot(
        string payload,
        IdentityConfigSyncSnapshot snapshot,
        string reason)
    {
        bool changed = !_hasLobbyConfig ||
                       _lobbyEnable != snapshot.Enable ||
                       _lobbyAddCharacterSkillsToLoot != snapshot.AddCharacterSkillsToLoot;

        _lobbyEnable = snapshot.Enable;
        _lobbyAddCharacterSkillsToLoot = snapshot.AddCharacterSkillsToLoot;
        _hasLobbyConfig = true;
        _lastAppliedRevision = snapshot.Revision;
        _lastAppliedPayload = payload;

        if (changed)
        {
            Debug.Log(
                "[DewIdentityChange] Applied lobby customData config r" + snapshot.Revision +
                " enable=" + snapshot.Enable +
                ", addCharacterSkillsToLoot=" + snapshot.AddCharacterSkillsToLoot +
                ": " + reason);
        }
    }

    private void EnsureGameConfigLocked(LobbyInstance lobby, string reason)
    {
        if (!_isGameConfigLocked && IsRunStarted(lobby))
        {
            LockGameConfig(lobby, reason);
        }
    }

    private void LockGameConfig(LobbyInstance lobby, string reason)
    {
        if (_isGameConfigLocked)
        {
            return;
        }

        if (!_hasLobbyConfig && !ApplyLobbyConfig(lobby, reason, force: true))
        {
            if (!lobby.isLobbyLeader)
            {
                Debug.LogWarning(
                    "[DewIdentityChange] Waiting for lobby customData before locking config: " + reason);
                return;
            }

            _lobbyEnable = _config.enable;
            _lobbyAddCharacterSkillsToLoot = _config.addCharacterSkillsToLoot;
            _hasLobbyConfig = true;
        }

        _lockedEnable = _lobbyEnable;
        _lockedAddCharacterSkillsToLoot = _lobbyAddCharacterSkillsToLoot;
        _isGameConfigLocked = true;

        Debug.Log(
            "[DewIdentityChange] Locked lobby customData config enable=" + _lockedEnable +
            ", addCharacterSkillsToLoot=" + _lockedAddCharacterSkillsToLoot +
            ": " + reason);
    }

    private void EnsureCurrentLobby(LobbyInstance lobby)
    {
        if (_currentLobbyId == lobby.id)
        {
            return;
        }

        ResetSyncedState();
        _currentLobbyId = lobby.id;
    }

    private static LobbyInstance GetCurrentLobby()
    {
        return ManagerBase<LobbyManager>.softInstance?.service?.currentLobby;
    }

    private static bool IsRunStarted(LobbyInstance lobby)
    {
        if (lobby?.hasGameStarted == true)
        {
            return true;
        }

        GameSettingsManager gameSettings =
            NetworkedManagerBase<GameSettingsManager>.softInstance;
        return gameSettings != null && gameSettings.state != GameState.InLobby;
    }

    private void ResetSyncedState()
    {
        _hasLobbyConfig = false;
        _isGameConfigLocked = false;
        _lobbyEnable = false;
        _lobbyAddCharacterSkillsToLoot = false;
        _lockedEnable = false;
        _lockedAddCharacterSkillsToLoot = false;
        _currentLobbyId = null;
        _lastAppliedPayload = null;
        _lastAppliedRevision = 0;
    }

    private static bool TryDeserialize(
        string payload,
        out IdentityConfigSyncSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        error = null;

        if (string.IsNullOrWhiteSpace(payload))
        {
            error = "payload is empty";
            return false;
        }

        try
        {
            snapshot = JsonUtility.FromJson<IdentityConfigSyncSnapshot>(payload);
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }

        if (snapshot == null)
        {
            error = "payload did not contain a snapshot";
            return false;
        }

        if (snapshot.ProtocolVersion != IdentityConfigSyncSnapshot.CurrentProtocolVersion)
        {
            error =
                "unsupported protocol version " + snapshot.ProtocolVersion +
                " (expected " + IdentityConfigSyncSnapshot.CurrentProtocolVersion + ")";
            snapshot = null;
            return false;
        }

        return true;
    }
}
