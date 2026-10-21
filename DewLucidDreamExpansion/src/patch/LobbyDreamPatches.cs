using System.Linq;
using DewLucidDreamExpansion.controller;
using HarmonyLib;

namespace DewLucidDreamExpansion.patch;

// 旧版大厅分组使用序列化名称列表，完整窗口则直接读取已注册资源。
[HarmonyPatch(typeof(UI_Lobby_LucidDreams), "Start")]
internal static class LobbyDreamGroupPatch
{
    private static void Prefix(UI_Lobby_LucidDreams __instance)
    {
        string[] original = __instance.lucidDreams;
        if (original == null || original.Length == 0) return;
        LucidDream source = DewResources.GetByShortTypeName<LucidDream>(original[0]);
        if (source == null) return;
        __instance.lucidDreams = original.Concat(DreamRegistry.Dreams
                .Where(type => DreamRegistry.GetDreamType(type) == source.type)
                .Select(type => type.Name))
            .Distinct().ToArray();
    }
}
