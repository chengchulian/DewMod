using DewSafeShare.util;
using HarmonyLib;
using Mirror;

namespace DewSafeShare.patch;

[HarmonyPatch(typeof(PingManager), "UserCode_CmdSendPing__Ping__NetworkConnectionToClient")]
public static class PingManagerPatch
{
    private static void Prefix(PingManager.Ping ping, NetworkConnectionToClient sender, out SafeShareController.PingState __state)
    {
        __state = SafeShareController.CapturePingState(ping, sender?.GetPlayer());
    }

    private static void Postfix(SafeShareController.PingState __state)
    {
        SafeShareController.ScheduleRelockAfterPing(__state);
    }
}
