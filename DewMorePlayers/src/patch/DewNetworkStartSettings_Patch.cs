using HarmonyLib;

namespace DewMorePlayers.patch;

[HarmonyPatch(typeof(DewNetworkStartSettings))]
public class DewNetworkStartSettings_Patch
{
    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPostfix]
    public static void Constructor_Postfix(DewNetworkStartSettings __instance)
    {
        __instance.maxPlayers = DewMorePlayers.MaxPlayers;
    }
}
