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

        private void Awake()
        {
            Instance = this;
            LocalizationSource.Init(this);
            harmony.PatchAll();
        }

        private void ActorManagerOnStartClient()
        {

            Debug.Log( "[DewGemSlotCount]  注册客户端接收事件 ");
            GemSyncHelper.RegisterGemSyncHandler(Config);

            if (NetworkServer.active)
            {
                Debug.Log( "[DewGemSlotCount]  游戏启动发送同步数据 ");
                GemSyncHelper.SyncGemConfigToAllClients(Config);
            }
        }

        private void Start()
        {
            Debug.Log($"[{mod.metadata.id}] 已加载: {mod.metadata.name} by {mod.metadata.author}");

            // ActorManager创建设置 (游戏启动后)
            CallOnNetworkedManager<ActorManager>(ActorManagerOnStartClient);
            // ZoneManager创建设置(过图后)
            CallOnNetworkedManager<ZoneManager>(ZoneManagerOnStartClient);

            // 中途加入发送配置
            DewPlayer.onHumanPlayerAdded += _ =>
            {
                Debug.Log( "[DewGemSlotCount]  中途加入发送同步数据 ");
                GemSyncHelper.SyncGemConfigToAllClients(Config);
            };
        }

        private void ZoneManagerOnStartClient()
        {
            ZoneManager.instance.ClientEvent_OnRoomLoaded += _ =>
            {
                Debug.Log( "[DewGemSlotCount]  过图发送同步数据 ");
                GemSyncHelper.SyncGemConfigToAllClients(Config);
            };
        }

        private void OnDestroy()
        {
            harmony.UnpatchAll(harmony.Id);
        }

        public override void OnConfigChanged()
        {
            if (NetworkServer.active)
            {
                Debug.Log( "[DewGemSlotCount]  配置改变发送同步数据 ");
                GemSyncHelper.SyncGemConfigToAllClients(Config);
            }
        }
    }
}