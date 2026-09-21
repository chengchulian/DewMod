using HarmonyLib;

namespace DewMorePlayers.patch;

[HarmonyPatch(typeof(LobbyServiceEOS), nameof(LobbyServiceEOS.GetInitialAttr_maxPlayers))]
internal static class LobbyServiceEOS_GetInitialAttrMaxPlayers_Patch
{
    [HarmonyPostfix]
    private static void Postfix(ref int __result)
    {
        __result = DewMorePlayers.MaxPlayers;
    }
}
