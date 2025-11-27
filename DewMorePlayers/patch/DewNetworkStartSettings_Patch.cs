using HarmonyLib;

namespace DewMorePlayers.patch;

[HarmonyPatch(typeof(DewNetworkStartSettings))]
public class DewNetworkStartSettings_Patch
{
    
    // Patch 默认构造函数（.ctor）
    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPostfix]
    public static void Constructor_Postfix(DewNetworkStartSettings __instance)
    {
        // 这里可以动态修改值
        // 例：把最大玩家数改为你配置里的 MaxPlayer
        __instance.maxPlayers = DewMorePlayers.Instance.config.MaxPlayer;
    }
}
