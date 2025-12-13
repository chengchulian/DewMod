using DewGemSlotCount.config;
using UnityEngine;
using Mirror;

namespace DewGemSlotCount
{
    public static class GemSyncHelper
    {
        /// <summary>
        /// 发送当前 Gem 配置给所有客户端
        /// </summary>
        public static void SyncGemConfigToAllClients(PluginConfig config)
        {
            if (ActorManager.instance == null)
            {
                return;
            }

            if (!NetworkServer.active)
            {
                return;
            }

            var msg = config;
            if (ActorManager.instance.serverActor == null)
            {
                return;
            }

            ActorManager.instance.serverActor.CustomRpc_SendMessageToAllClients(msg);
            Debug.Log("[DewGemSlotCount]  发送 Gem 配置同步消息  " + msg);
        }

        /// <summary>
        /// 注册接收 Gem 配置同步消息的回调
        /// </summary>
        public static void RegisterGemSyncHandler(PluginConfig config)
        {
            ActorManager.instance.serverActor.CustomRpc_RegisterClientMessageHandler<PluginConfig>(msg =>
            {
                Debug.Log("[DewGemSlotCount]  接收 Gem 配置同步消息 " + msg);
                config.SkillQGemCount = msg.SkillQGemCount;
                config.SkillWGemCount = msg.SkillWGemCount;
                config.SkillEGemCount = msg.SkillEGemCount;
                config.SkillRGemCount = msg.SkillRGemCount;
                config.SkillIdentityGemCount = msg.SkillIdentityGemCount;
                config.SkillMovementGemCount = msg.SkillMovementGemCount;
                config.EditIdentitySkill = msg.EditIdentitySkill;
                config.EditMovementSkill = msg.EditMovementSkill;
                config.GemNoMerge = msg.GemNoMerge;
            });
        }
    }
}