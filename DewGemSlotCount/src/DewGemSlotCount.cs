using System;
using DewGemSlotCount.config;
using Mirror;
using UnityEngine;
using DewGemSlotCount.controller;
using DewGemSlotCount.ui;

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
        private float _nextSyncCheckTime;

        public PluginConfig GameplayConfig => NetworkServer.active ? Config : _serverConfig;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            Instance = this;
            instance.isAlteringGameplay = true;
            Config.ClampCounts();

            LocalizationSource.Init(this);
            GemPatchLifecycle.Install();
            GemSlotLayout.RebuildExisting();

            // 在游戏中重新启用时，网络管理器可能已完成启动。
            if (NetworkServer.active)
            {
                GemSlotController.ResumeExisting();
                GemSlotController.RefreshAll();
            }

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
                GemSlotController.RefreshAll();
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
            GemSlotController.RestoreAll();
            GemSlotController.Clear();
            GemSlotStateSync.ClearCache();
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

        // 沿用原版 customData 配置快照；轮询只补偿联网、读档或切场景时错过的事件。
        private void Update()
        {
            if (NetworkServer.active || !NetworkClient.active || Time.unscaledTime < _nextSyncCheckTime) return;
            _nextSyncCheckTime = Time.unscaledTime + 1f;
            var settings = NetworkedManagerBase<GameSettingsManager>.softInstance;
            if (settings == null) return;
            SubscribeGameSettings(settings);
            ApplyServerConfig("检查房主配置");
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

            // 房主重载后延续当前房间的版本号，使未重载客机继续接收新配置。
            if (gameSettings.customData.TryGetValue(GemSyncHelper.SyncKey, out string previousPayload) &&
                GemSyncHelper.TryDeserialize(previousPayload, out GemConfigSyncSnapshot previous, out _))
                _publishedRevision = Math.Max(_publishedRevision, previous.Revision);
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
            // 某个界面已经销毁时仍保证补丁、网络处理器和实例引用被清理。
            try
            {
                UnsubscribeGameSettings();
                GemSlotController.RestoreAll();
                foreach (var ui in Resources.FindObjectsOfTypeAll<UI_InGame_SkillButtons>())
                {
                    if (!ui.gameObject.scene.IsValid()) continue;
                    try { MovementEditLayout.Restore(ui); }
                    catch (Exception exception) { Debug.LogException(exception); }
                }
                GemSlotLayout.Cleanup();
            }
            finally
            {
                try { GemPatchLifecycle.Remove(); }
                finally
                {
                    GemSlotController.Clear();
                    try { GemSlotStateSync.Cleanup(); }
                    finally { if (Instance == this) Instance = null; }
                }
            }
        }

        public override void OnConfigChanged()
        {
            if (_isApplyingServerConfig)
            {
                return;
            }

            Config.ClampCounts();

            if (NetworkServer.active)
            {
                GemSlotController.RefreshAll();
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
