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

        private const int ClientSyncRequestMaxAttempts = 30;
        private const int ClientSyncRequestRetryFrames = 30;
        private const int ServerPlayerSyncRetryFrames = 30;
        private const int ServerPlayerSyncSecondRetryFrames = 120;

        private Action<GemConfigSyncMessage> _syncHandler;
        private Action<GemConfigSyncRequest, DewPlayer> _syncRequestHandler;
        private bool _isActorManagerStarted;
        private bool _isActorAddSubscribed;
        private bool _isSyncHandlerRegistered;
        private bool _isSyncRequestHandlerRegistered;
        private bool _hasServerConfig;
        private bool _isApplyingServerConfig;
        private int _clientSyncRequestToken;

        public PluginConfig GameplayConfig => NetworkServer.active ? Config : _serverConfig;

        private void Awake()
        {
            Instance = this;
            _syncHandler = ApplyServerConfig;
            _syncRequestHandler = OnGemSyncRequest;
        }

        private void Start()
        {
            instance.isAlteringGameplay = true;

            LocalizationSource.Init(this);
            harmony.PatchAll();

            CallOnNetworkedManager<ActorManager>(ActorManagerOnStartClient, ActorManagerOnStopClient);
            CallOnNetworkedManager<ZoneManager>(ZoneManagerOnStartClient, ZoneManagerOnStopClient);
            SubscribeHumanPlayerAdded();

            Debug.Log($"[{mod.metadata.id}] Loaded {mod.metadata.name} by {mod.metadata.author}");
        }

        private void ActorManagerOnStartClient()
        {
            _isActorManagerStarted = true;
            SubscribeHumanPlayerAdded();
            SubscribeActorAdded();
            ResetServerConfigCache();
            TrySetupGemSync("actor manager start");
            SyncConfigToAllClients("game start");
            Dew.CallDelayed(() => SyncConfigToAllClients("game start retry 1"), ServerPlayerSyncRetryFrames);
            Dew.CallDelayed(() => SyncConfigToAllClients("game start retry 2"), ServerPlayerSyncSecondRetryFrames);
            RequestGemConfigFromServer("actor manager start");
        }

        private void ActorManagerOnStopClient()
        {
            _clientSyncRequestToken++;
            _isActorManagerStarted = false;
            _hasServerConfig = false;
            UnsubscribeActorAdded();

            if (_isSyncHandlerRegistered)
            {
                GemSyncHelper.UnregisterGemSyncHandler(_syncHandler);
                _isSyncHandlerRegistered = false;
            }

            if (_isSyncRequestHandlerRegistered)
            {
                GemSyncHelper.UnregisterGemSyncRequestHandler(_syncRequestHandler);
                _isSyncRequestHandlerRegistered = false;
            }
        }

        private void ZoneManagerOnStartClient()
        {
            if (ZoneManager.instance != null)
            {
                ZoneManager.instance.ClientEvent_OnRoomLoaded += OnRoomLoaded;
            }
        }

        private void ZoneManagerOnStopClient()
        {
            if (ZoneManager.instance != null)
            {
                ZoneManager.instance.ClientEvent_OnRoomLoaded -= OnRoomLoaded;
            }
        }

        private void OnHumanPlayerAdded(DewPlayer player)
        {
            if (!NetworkServer.active)
            {
                return;
            }

            SyncConfigToClient(player, "player joined");
            Dew.CallDelayed(() => SyncConfigToClient(player, "player joined retry 1"), ServerPlayerSyncRetryFrames);
            Dew.CallDelayed(() => SyncConfigToClient(player, "player joined retry 2"), ServerPlayerSyncSecondRetryFrames);
        }

        private void OnRoomLoaded(EventInfoLoadRoom _)
        {
            SyncConfigToAllClients("room loaded");
            RequestGemConfigFromServer("room loaded");
        }

        private void OnActorAdded(Actor actor)
        {
            if (!IsServerActor(actor))
            {
                return;
            }

            TrySetupGemSync("server actor added");
            RequestGemConfigFromServer("server actor added");
        }

        private bool IsServerActor(Actor actor)
        {
            if (actor == null)
            {
                return false;
            }

            if (ActorManager.instance != null && actor == ActorManager.instance.serverActor)
            {
                return true;
            }

            return actor.GetType().Name == "ServerActor";
        }

        private void OnGemSyncRequest(GemConfigSyncRequest request, DewPlayer requester)
        {
            if (!NetworkServer.active || requester == null)
            {
                return;
            }

            Debug.Log("[DewGemSlotCount] Received Gem config sync request from " + requester.playerNameRaw);
            SyncConfigToClient(requester, "client request");
        }

        private void ApplyServerConfig(GemConfigSyncMessage msg)
        {
            if (msg == null)
            {
                return;
            }

            _isApplyingServerConfig = true;
            try
            {
                msg.ApplyTo(_serverConfig);
                _hasServerConfig = true;
                _clientSyncRequestToken++;

                if (!NetworkServer.active)
                {
                    msg.ApplyTo(Config);
                }
            }
            finally
            {
                _isApplyingServerConfig = false;
            }

            Debug.Log("[DewGemSlotCount] Applied server Gem config");
        }

        private void ResetServerConfigCache()
        {
            if (NetworkServer.active)
            {
                return;
            }

            GemConfigSyncMessage.FromConfig(new PluginConfig()).ApplyTo(_serverConfig);
            _hasServerConfig = false;
        }

        private void RestoreServerConfigToLocalConfig()
        {
            if (!_hasServerConfig)
            {
                return;
            }

            _isApplyingServerConfig = true;
            try
            {
                GemConfigSyncMessage.FromConfig(_serverConfig).ApplyTo(Config);
            }
            finally
            {
                _isApplyingServerConfig = false;
            }
        }

        private void TrySetupGemSync(string reason)
        {
            if (!_isSyncHandlerRegistered)
            {
                _isSyncHandlerRegistered = GemSyncHelper.RegisterGemSyncHandler(_syncHandler);
                if (_isSyncHandlerRegistered)
                {
                    Debug.Log("[DewGemSlotCount] Registered Gem config receive handler: " + reason);
                }
            }

            if (NetworkServer.active && !_isSyncRequestHandlerRegistered)
            {
                _isSyncRequestHandlerRegistered = GemSyncHelper.RegisterGemSyncRequestHandler(_syncRequestHandler);
                if (_isSyncRequestHandlerRegistered)
                {
                    Debug.Log("[DewGemSlotCount] Registered Gem config request handler: " + reason);
                }
            }
        }

        private void RequestGemConfigFromServer(string reason)
        {
            if (NetworkServer.active)
            {
                return;
            }

            var token = ++_clientSyncRequestToken;
            RequestGemConfigFromServerAttempt(reason, token, 1);
        }

        private void RequestGemConfigFromServerAttempt(string reason, int token, int attempt)
        {
            if (this == null || token != _clientSyncRequestToken || !_isActorManagerStarted || NetworkServer.active || _hasServerConfig)
            {
                return;
            }

            TrySetupGemSync(reason);

            if (_isSyncHandlerRegistered && GemSyncHelper.RequestGemConfigFromServer())
            {
                Debug.Log("[DewGemSlotCount] Requested server Gem config: " + reason + " (attempt " + attempt + ")");
            }

            if (_hasServerConfig)
            {
                return;
            }

            if (attempt >= ClientSyncRequestMaxAttempts)
            {
                Debug.LogWarning("[DewGemSlotCount] Gem config sync request timed out: " + reason);
                return;
            }

            Dew.CallDelayed(() => RequestGemConfigFromServerAttempt(reason, token, attempt + 1), ClientSyncRequestRetryFrames);
        }

        private void SyncConfigToAllClients(string reason)
        {
            if (this == null || !_isActorManagerStarted || !NetworkServer.active)
            {
                return;
            }

            TrySetupGemSync(reason);
            Debug.Log("[DewGemSlotCount] Sync Gem config: " + reason);
            GemSyncHelper.SyncGemConfigToAllClients(Config);
        }

        private void SyncConfigToClient(DewPlayer player, string reason)
        {
            if (this == null || !_isActorManagerStarted || !NetworkServer.active || player == null)
            {
                return;
            }

            TrySetupGemSync(reason);
            Debug.Log("[DewGemSlotCount] Sync Gem config to " + player.playerNameRaw + ": " + reason);
            GemSyncHelper.SyncGemConfigToClient(Config, player);
        }

        private void SubscribeActorAdded()
        {
            if (_isActorAddSubscribed || ActorManager.instance == null)
            {
                return;
            }

            ActorManager.instance.ClientEvent_OnActorAdd += OnActorAdded;
            _isActorAddSubscribed = true;
        }

        private void UnsubscribeActorAdded()
        {
            if (!_isActorAddSubscribed || ActorManager.instance == null)
            {
                _isActorAddSubscribed = false;
                return;
            }

            ActorManager.instance.ClientEvent_OnActorAdd -= OnActorAdded;
            _isActorAddSubscribed = false;
        }

        private void SubscribeHumanPlayerAdded()
        {
            DewPlayer.onHumanPlayerAdded -= OnHumanPlayerAdded;
            DewPlayer.onHumanPlayerAdded += OnHumanPlayerAdded;
        }

        private void OnDestroy()
        {
            DewPlayer.onHumanPlayerAdded -= OnHumanPlayerAdded;
            ActorManagerOnStopClient();
            ZoneManagerOnStopClient();
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
                SyncConfigToAllClients("config changed");
                return;
            }

            RestoreServerConfigToLocalConfig();
        }
    }
}
