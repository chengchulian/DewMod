using HarmonyLib;

namespace DewShieldModDetection;

[HarmonyPatch(typeof(DewMod), nameof(DewMod.isAnyModActive), MethodType.Getter)]
internal static class DewModIsAnyModActivePatch
{
    // 保留原有跨平台检测返回值处理，游戏玩法 Mod 标记由原逻辑维护。
    [HarmonyPostfix]
    private static void Postfix(ref bool __result)
    {
        __result = false;
    }
}
