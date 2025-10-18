using DewDifficultyCustomizeMod.util;
using HarmonyLib;

namespace DewDifficultyCustomizeMod.patch;

[HarmonyPatch(typeof(UI_Common_ChatBox))]
public class UI_Common_ChatBox_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch("ClientEventOnMessageReceived")]
    public static bool ClientEventOnMessageReceived_Prefix(UI_Common_ChatBox __instance, ChatManager.Message obj)
    {
        GameManagerUtil.ClientSyncData(obj);
        return true;
    }
}