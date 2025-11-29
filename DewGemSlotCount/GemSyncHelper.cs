using DewGemSlotCount.config;
using DewGemSlotCount.entity;
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

            var msg = new GemConfigSyncMessage
            {
                Q = config.SkillQGemCount,
                W = config.SkillWGemCount,
                E = config.SkillEGemCount,
                R = config.SkillRGemCount,
                Identity = config.SkillIdentityGemCount,
                Movement = config.SkillMovementGemCount,
            };
            if (ActorManager.instance.serverActor != null)
            {
                ActorManager.instance.serverActor.CustomRpc_SendMessageToAllClients(msg);
                Debug.Log( "[DewGemSlotCount]  发送 Gem 配置同步消息  " + msg);
            }
        }

        /// <summary>
        /// 注册接收 Gem 配置同步消息的回调
        /// </summary>
        public static void RegisterGemSyncHandler(PluginConfig config)
        {

            ActorManager.instance.serverActor.CustomRpc_RegisterClientMessageHandler<GemConfigSyncMessage>(msg =>
            {
                Debug.Log( "[DewGemSlotCount]  接收 Gem 配置同步消息 " + msg);
                config.SkillQGemCount = msg.Q;
                config.SkillWGemCount = msg.W;
                config.SkillEGemCount = msg.E;
                config.SkillRGemCount = msg.R;
                config.SkillIdentityGemCount = msg.Identity;
                config.SkillMovementGemCount = msg.Movement;
            });
        }
    }
}