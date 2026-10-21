using DewShopShare.controller;
using HarmonyLib;

namespace DewShopShare.patch;

// 源码依据：Dewr.1.4.0.13_s/Dew.Core/PingManager.cs，校验后的 CmdSendPing 调用此 RPC 包装方法。
[HarmonyPatch(typeof(PingManager), nameof(PingManager.BroadcastPing))]
internal static class PingManagerPatch
{
    // 在服务端发送原生 RPC 前追加库存，所有客机继续接收原版网络消息。
    private static void Prefix(PingManager.Ping ping)
    {
        ShopShareController.OnValidatedPing(ping);
    }
}
