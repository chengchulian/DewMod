using System.Globalization;
using DewLucidDreamExpansion.ui;
using HarmonyLib;

namespace DewLucidDreamExpansion.patch;

[HarmonyPatch(typeof(UI_Lobby_Limbo_DepthSelect), "Start")]
internal static class LimboDepthSelectorPatch
{
    private static void Postfix(UI_Lobby_Limbo_DepthSelect __instance) => LimboDepthSelector.EnsureItems(__instance);
}

// GetMaxDepths 是常量返回方法，旧调用点可能已内联六级上限；保留传入的扩展深度。
[HarmonyPatch(typeof(UI_LimboDepthIcon), nameof(UI_LimboDepthIcon.Setup))]
internal static class LimboDepthIconPatch
{
    private static void Prefix(int depth, out int __state) => __state = System.Math.Max(0, System.Math.Min(depth, LimboDepthSelector.MaxDepth));

    private static void Postfix(UI_LimboDepthIcon __instance, int __state)
    {
        if (__instance.depth == __state) return;
        __instance.depth = __state;
        __instance.depthText.text = __state.ToString(CultureInfo.InvariantCulture);
        AccessTools.Method(typeof(UI_LimboDepthIcon), "UpdateState").Invoke(__instance, null);
    }
}
