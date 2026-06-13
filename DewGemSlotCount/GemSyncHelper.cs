using System;
using DewGemSlotCount.config;
using Mirror;
using UnityEngine;

namespace DewGemSlotCount
{
    public class GemConfigSyncMessage
    {
        public int SkillQGemCount;
        public int SkillWGemCount;
        public int SkillEGemCount;
        public int SkillRGemCount;
        public int SkillIdentityGemCount;
        public int SkillMovementGemCount;
        public int SkillQCorruptedChaosMaxGemCount;
        public int SkillWCorruptedChaosMaxGemCount;
        public int SkillECorruptedChaosMaxGemCount;
        public int SkillRCorruptedChaosMaxGemCount;
        public int SkillIdentityCorruptedChaosMaxGemCount;
        public int SkillMovementCorruptedChaosMaxGemCount;
        public bool EditIdentitySkill;
        public bool EditMovementSkill;
        public bool AllowMovementCorruptedChaos;
        public bool GemNoMerge;

        public static GemConfigSyncMessage FromConfig(PluginConfig config)
        {
            return new GemConfigSyncMessage
            {
                SkillQGemCount = config.SkillQGemCount,
                SkillWGemCount = config.SkillWGemCount,
                SkillEGemCount = config.SkillEGemCount,
                SkillRGemCount = config.SkillRGemCount,
                SkillIdentityGemCount = config.SkillIdentityGemCount,
                SkillMovementGemCount = config.SkillMovementGemCount,
                SkillQCorruptedChaosMaxGemCount = config.SkillQCorruptedChaosMaxGemCount,
                SkillWCorruptedChaosMaxGemCount = config.SkillWCorruptedChaosMaxGemCount,
                SkillECorruptedChaosMaxGemCount = config.SkillECorruptedChaosMaxGemCount,
                SkillRCorruptedChaosMaxGemCount = config.SkillRCorruptedChaosMaxGemCount,
                SkillIdentityCorruptedChaosMaxGemCount = config.SkillIdentityCorruptedChaosMaxGemCount,
                SkillMovementCorruptedChaosMaxGemCount = config.SkillMovementCorruptedChaosMaxGemCount,
                EditIdentitySkill = config.EditIdentitySkill,
                EditMovementSkill = config.EditMovementSkill,
                AllowMovementCorruptedChaos = config.AllowMovementCorruptedChaos,
                GemNoMerge = config.GemNoMerge
            };
        }

        public void ApplyTo(PluginConfig config)
        {
            config.SkillQGemCount = SkillQGemCount;
            config.SkillWGemCount = SkillWGemCount;
            config.SkillEGemCount = SkillEGemCount;
            config.SkillRGemCount = SkillRGemCount;
            config.SkillIdentityGemCount = SkillIdentityGemCount;
            config.SkillMovementGemCount = SkillMovementGemCount;
            config.SkillQCorruptedChaosMaxGemCount = SkillQCorruptedChaosMaxGemCount;
            config.SkillWCorruptedChaosMaxGemCount = SkillWCorruptedChaosMaxGemCount;
            config.SkillECorruptedChaosMaxGemCount = SkillECorruptedChaosMaxGemCount;
            config.SkillRCorruptedChaosMaxGemCount = SkillRCorruptedChaosMaxGemCount;
            config.SkillIdentityCorruptedChaosMaxGemCount = SkillIdentityCorruptedChaosMaxGemCount;
            config.SkillMovementCorruptedChaosMaxGemCount = SkillMovementCorruptedChaosMaxGemCount;
            config.EditIdentitySkill = EditIdentitySkill;
            config.EditMovementSkill = EditMovementSkill;
            config.AllowMovementCorruptedChaos = AllowMovementCorruptedChaos;
            config.GemNoMerge = GemNoMerge;
        }
    }

    public class GemConfigSyncRequest
    {
        public int Version = 1;
    }

    public static class GemSyncHelper
    {
        public static void SyncGemConfigToAllClients(PluginConfig config)
        {
            var serverActor = GetServerActor();
            if (!NetworkServer.active || serverActor == null)
            {
                Debug.Log("[DewGemSlotCount] Skip syncing Gem config to all clients: server not ready");
                return;
            }

            serverActor.CustomRpc_SendMessageToAllClients(GemConfigSyncMessage.FromConfig(config));
            Debug.Log("[DewGemSlotCount] Sync Gem config to all clients");
        }

        public static void SyncGemConfigToClient(PluginConfig config, DewPlayer target)
        {
            var serverActor = GetServerActor();
            if (!NetworkServer.active || serverActor == null || target == null)
            {
                Debug.Log("[DewGemSlotCount] Skip syncing Gem config to client: server, actor, or target not ready");
                return;
            }

            serverActor.CustomRpc_SendMessageToClient(target, GemConfigSyncMessage.FromConfig(config));
            Debug.Log("[DewGemSlotCount] Sync Gem config to client " + target.playerNameRaw);
        }

        public static bool RegisterGemSyncHandler(Action<GemConfigSyncMessage> handler)
        {
            var serverActor = GetServerActor();
            if (serverActor == null || handler == null)
            {
                return false;
            }

            serverActor.CustomRpc_UnregisterClientMessageHandler(handler);
            serverActor.CustomRpc_RegisterClientMessageHandler(handler);
            return true;
        }

        public static bool RegisterGemSyncRequestHandler(Action<GemConfigSyncRequest, DewPlayer> handler)
        {
            var serverActor = GetServerActor();
            if (!NetworkServer.active || serverActor == null || handler == null)
            {
                return false;
            }

            serverActor.CustomRpc_UnregisterServerMessageHandler(handler);
            serverActor.CustomRpc_RegisterServerMessageHandler("DewGemSlotCount", handler);
            return true;
        }

        public static void UnregisterGemSyncHandler(Action<GemConfigSyncMessage> handler)
        {
            var serverActor = GetServerActor();
            if (serverActor == null || handler == null)
            {
                return;
            }

            serverActor.CustomRpc_UnregisterClientMessageHandler(handler);
        }

        public static void UnregisterGemSyncRequestHandler(Action<GemConfigSyncRequest, DewPlayer> handler)
        {
            var serverActor = GetServerActor();
            if (serverActor == null || handler == null)
            {
                return;
            }

            serverActor.CustomRpc_UnregisterServerMessageHandler(handler);
        }

        public static bool RequestGemConfigFromServer()
        {
            var serverActor = GetServerActor();
            if (!NetworkClient.active || serverActor == null)
            {
                return false;
            }

            serverActor.CustomRpc_SendMessageToServer(new GemConfigSyncRequest());
            return true;
        }

        private static Actor GetServerActor()
        {
            return ActorManager.instance == null ? null : ActorManager.instance.serverActor;
        }
    }
}
