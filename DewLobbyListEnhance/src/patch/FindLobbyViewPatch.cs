using System.Collections.Generic;
using System.Linq;
using System;
using HarmonyLib;
using DewLobbyListEnhance.ui;

namespace DewLobbyListEnhance.patch;

[HarmonyPatch(typeof(UI_Title_FindLobbyView))]
internal static class FindLobbyViewPatch
{
    [HarmonyPostfix, HarmonyPatch("Start")]
    private static void StartPostfix(UI_Title_FindLobbyView __instance)
    {
        EnsureStateForModLoad(__instance);
    }

    // Mod 可能在大厅视图已经 Start 后才加载，因此每次打开视图时也确保筛选栏存在。
    [HarmonyPostfix, HarmonyPatch("OnShow")]
    private static void ShowPostfix(UI_Title_FindLobbyView __instance)
    {
        EnsureStateForModLoad(__instance);
    }

    internal static void EnsureStateForModLoad(UI_Title_FindLobbyView view)
    {
        if (view == null) return;
        int id = view.GetInstanceID();
        if (!DewLobbyListEnhance.States.ContainsKey(id)) DewLobbyListEnhance.States[id] = new LobbyFilterState(view);
    }

    [HarmonyPrefix, HarmonyPatch("OnLobbyListUpdated")]
    private static void UpdatePrefix(UI_Title_FindLobbyView __instance)
    {
        if (!DewLobbyListEnhance.States.TryGetValue(__instance.GetInstanceID(), out LobbyFilterState state)) return;
        LobbyServiceProvider service = ManagerBase<LobbyManager>.instance?.service;
        LobbySearchResult result = service?.foundLobbies;
        if (result?.lobbies == null) return;
        // 对搜索结果复核公开状态，防止平台服务或游戏版本忽略请求筛选条件。
        IEnumerable<LobbyInstance> query = result.lobbies.Where(lobby => lobby != null);
        if (state.Publicity == 1) query = query.Where(lobby => !lobby.isInviteOnly);
        else if (state.Publicity == 2) query = query.Where(lobby => lobby.isInviteOnly);
        query = state.Sort == 1
            ? query.OrderBy(lobby => GetExactLatency(lobby) ?? int.MaxValue)
            : state.Sort == 2
                ? query.OrderBy(lobby => lobby.crossPlayGate ?? string.Empty)
                : query.OrderByDescending(lobby => lobby.gameStartTimestamp);
        // 对本次刚返回的搜索结果应用条件，避免保留旧搜索结果造成筛选状态过期。
        LobbySearchResult filteredResult = new LobbySearchResult
        {
            lobbies = query.ToList(),
            continuationToken = result.continuationToken
        };
        service.foundLobbies = filteredResult;
    }

    private static int? GetExactLatency(LobbyInstance lobby)
    {
        if (lobby.customData == null) return null;
        if (lobby.customData.TryGetValue("latencyMs", out string value) && int.TryParse(value, out int latency) && latency >= 0) return latency;
        if (lobby.customData.TryGetValue("pingMs", out value) && int.TryParse(value, out latency) && latency >= 0) return latency;
        return null;
    }
}
