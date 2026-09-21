using System;
using DewGemSlotCount.config;
using Mirror;
using UnityEngine;

namespace DewGemSlotCount
{
    public class DewGemSlotCount : ModBehaviour
    {
        public static DewGemSlotCount Instance;
        public readonly PluginConfig Config = new PluginConfig();

        [NonSerialized]
        private readonly PluginConfig _serverConfig = new PluginConfig();

        private GameSettingsManager _subscribedGameSettings;
        private bool _hasServerConfig;
        private bool _isApplyingServerConfig;
        private uint _lastAppliedRevision;
        private uint _publishedRevision;
        private string _lastAppliedPayload;

        public PluginConfig GameplayConfig => NetworkServer.active ? Config : _serverConfig;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            instance.isAlteringGameplay = true;

            LocalizationSource.Init(this);
            harmony.PatchAll();

            CallOnNetworkedManager<GameSettingsManager>(
                GameSettingsManagerOnStartClient,
                GameSettingsManagerOnStopClient);

            Debug.Log($"[{mod.metadata.id}] Loaded {mod.metadata.name} by {mod.metadata.author}");
        }

        private void GameSettingsManagerOnStartClient()
        {
            var gameSettings = NetworkedManagerBase<GameSettingsManager>.softInstance;
            if (gameSettings == null)
            {
                Debug.LogWarning("[DewGemSlotCount] GameSettingsManager started without an instance");
                return;
            }

            SubscribeGameSettings(gameSettings);

            if (NetworkServer.active)
            {
                PublishServerConfig("game settings start");
            }
            else
            {
                ResetServerConfigCache();
                ApplyServerConfig("game settings initial state");
            }
        }

        private void GameSettingsManagerOnStopClient()
        {
            UnsubscribeGameSettings();
            ResetServerConfigCache();
        }

        private void SubscribeGameSettings(GameSettingsManager gameSettings)
        {
            if (_subscribedGameSettings == gameSettings)
            {
                return;
            }

            UnsubscribeGameSettings();
            gameSettings.ClientEvent_OnCustomDataChanged -= OnGameSettingsCustomDataChanged;
            gameSettings.ClientEvent_OnCustomDataChanged += OnGameSettingsCustomDataChanged;
            _subscribedGameSettings = gameSettings;
        }

        private void UnsubscribeGameSettings()
        {
            if (_subscribedGameSettings != null)
            {
                _subscribedGameSettings.ClientEvent_OnCustomDataChanged -= OnGameSettingsCustomDataChanged;
            }

            _subscribedGameSettings = null;
        }

        private void OnGameSettingsCustomDataChanged(string key)
        {
            if (key != GemSyncHelper.SyncKey || NetworkServer.active)
            {
                return;
            }

            ApplyServerConfig("network snapshot changed");
        }

        private void PublishServerConfig(string reason)
        {
            if (!NetworkServer.active)
            {
                return;
            }

            var gameSettings = _subscribedGameSettings ??
                               NetworkedManagerBase<GameSettingsManager>.softInstance;
            if (gameSettings?.customData == null)
            {
                Debug.LogWarning("[DewGemSlotCount] Cannot publish Gem config: GameSettings custom data is unavailable");
                return;
            }

            var snapshot = GemConfigSyncSnapshot.FromConfig(Config, ++_publishedRevision);
            string payload = GemSyncHelper.Serialize(snapshot);
            gameSettings.customData[GemSyncHelper.SyncKey] = payload;

            Debug.Log(
                "[DewGemSlotCount] Published Gem config snapshot r" + snapshot.Revision +
                ": " + reason);
        }

        private void ApplyServerConfig(string reason, bool force = false)
        {
            if (NetworkServer.active)
            {
                return;
            }

            var gameSettings = _subscribedGameSettings ??
                               NetworkedManagerBase<GameSettingsManager>.softInstance;
            if (gameSettings?.customData == null ||
                !gameSettings.customData.TryGetValue(GemSyncHelper.SyncKey, out string payload))
            {
                return;
            }

            if (!force && payload == _lastAppliedPayload)
            {
                return;
            }

            if (!GemSyncHelper.TryDeserialize(payload, out GemConfigSyncSnapshot snapshot, out string error))
            {
                Debug.LogWarning("[DewGemSlotCount] Ignored invalid Gem config snapshot: " + error);
                return;
            }

            if (!force && _hasServerConfig && snapshot.Revision <= _lastAppliedRevision)
            {
                return;
            }

            _isApplyingServerConfig = true;
            try
            {
                snapshot.ApplyTo(_serverConfig);
                snapshot.ApplyTo(Config);
                _hasServerConfig = true;
                _lastAppliedRevision = snapshot.Revision;
                _lastAppliedPayload = payload;
            }
            finally
            {
                _isApplyingServerConfig = false;
            }

            Debug.Log(
                "[DewGemSlotCount] Applied server Gem config snapshot r" + snapshot.Revision +
                ": " + reason);
        }

        private void ResetServerConfigCache()
        {
            GemConfigSyncSnapshot.FromConfig(new PluginConfig(), 0).ApplyTo(_serverConfig);
            _hasServerConfig = false;
            _lastAppliedRevision = 0;
            _lastAppliedPayload = null;
        }

        private void OnDestroy()
        {
            UnsubscribeGameSettings();
            harmony.UnpatchAll(harmony.Id);
        }

        public override void OnConfigChanged()
        {
            if (_isApplyingServerConfig)
            {
                return;
            }

            if (NetworkServer.active)
            {
                PublishServerConfig("config changed");
                return;
            }

            if (_hasServerConfig)
            {
                ApplyServerConfig("restore authoritative server config", force: true);
            }
        }
    }
}
