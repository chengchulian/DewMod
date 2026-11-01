using DewGemSlotCount.controller;
using HarmonyLib;
using Mirror;

namespace DewGemSlotCount.patch;

[HarmonyPatch(typeof(GameResultManager), "UpdateGameResult")]
public static class GameResultManager_Patch
{
    // 原生结算携带四槽兼容值，完整槽数放入已有字典，不增加网络协议。
    [HarmonyPostfix]
    public static void Postfix(GameResultManager __instance)
    {
        if (DewGemSlotCount.Instance != null && NetworkServer.active)
            GemSlotStateSync.CaptureResult(__instance.tracked);
    }
}
