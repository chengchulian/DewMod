using System;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using HarmonyLib;
using Steamworks;
using DewLobbyListEnhance.ui;

namespace DewLobbyListEnhance.patch;

/// <summary>把筛选条件加入 EOS 与 Steam 的大厅搜索请求。</summary>
[HarmonyPatch]
internal static class LobbySearchFiltersPatch
{
    private static LobbyFilterState State => LobbyFilterState.Active;

    // 游戏设置关闭跨平台时，EOS 默认不会请求 ALL gate；“全部”和“跨平台”都需要搜索它。
    [HarmonyPostfix]
    [HarmonyPatch(typeof(LobbyServiceEOS), "get_IsCrossPlayEligible")]
    private static void EosCrossPlaySearchEligibilityPostfix(ref bool __result)
    {
        LobbyFilterState state = State;
        if (state != null && state.Platform != 1) __result = true;
    }

    // 跨平台搜索允许读取 ALL gate，并将结果中的 ALL 房间纳入列表，不受游戏偏好设置限制。
    [HarmonyPostfix]
    [HarmonyPatch(typeof(LobbyServiceEOS), nameof(LobbyServiceEOS.IsCrossPlayGateCompatible))]
    private static void EosCrossPlaySearchCompatibilityPostfix(string lobbyGate, ref bool __result)
    {
        LobbyFilterState state = State;
        if (state != null && state.Platform != 1 && lobbyGate == "ALL") __result = true;
    }

    // EOS 原生方法会分别搜索 ALL 与 STEAM gate；按下拉选择跳过不匹配的那次请求。
    [HarmonyPrefix]
    [HarmonyPatch(typeof(LobbyServiceEOS), "SearchLobbiesWithGate")]
    private static bool EosGateSearchPrefix(string gate, ref Cysharp.Threading.Tasks.UniTask<LobbySearchResult> __result)
    {
        LobbyFilterState state = State;
        if (state == null || state.Platform == 0) return true;

        bool matches = state.Platform == 1 ? gate == "STEAM" : gate == "ALL";
        if (matches) return true;

        __result = Cysharp.Threading.Tasks.UniTask.FromResult(new LobbySearchResult());
        return false;
    }

    // EOS 搜索参数在 Find 前统一设置；调整现有公开属性条件并追加难度条件。
    [HarmonyPrefix, HarmonyPatch(typeof(LobbySearch), nameof(LobbySearch.SetParameter))]
    private static bool EosSetParameterPrefix(LobbySearch __instance, ref LobbySearchSetParameterOptions options)
    {
        LobbyFilterState state = State;
        if (state == null || !options.Parameter.HasValue) return true;

        AttributeData parameter = options.Parameter.Value;
        // “全部”不限制邀请属性；否则原生固定的 false 条件会排除私密房。
        if (parameter.Key == "isInviteOnly" && state.Publicity == 0) return false;
        if (parameter.Key == "isInviteOnly" && state.Publicity > 0)
        {
            parameter.Value = state.Publicity == 2;
            options.Parameter = parameter;
        }
        return true;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(LobbySearch), nameof(LobbySearch.SetParameter))]
    private static void EosSetParameterPostfix(LobbySearch __instance, LobbySearchSetParameterOptions options)
    {
        LobbyFilterState state = State;
        if (state == null || !options.Parameter.HasValue || options.Parameter.Value.Key != "heartbeat" || state.Difficulty <= 0) return;

        string[] difficulties = { "", "diffEasy", "diffNormal", "diffHard", "diffNightmare", "diffLimbo" };
        // EOS 搜索 gate 已由 EosGateSearchPrefix 精确筛选。
        AddEosSearchParameter(__instance, "difficulty", difficulties[state.Difficulty]);
    }

    private static void AddEosSearchParameter(LobbySearch search, string key, string value)
    {
        LobbySearchSetParameterOptions option = new LobbySearchSetParameterOptions
        {
            Parameter = new AttributeData { Key = key, Value = value },
            ComparisonOp = ComparisonOp.Equal
        };
        search.SetParameter(ref option);
    }

    // Steam 的原生搜索每次请求前添加当前难度筛选，并将公开状态替换成下拉选择。
    [HarmonyPrefix]
    [HarmonyPatch(typeof(SteamMatchmaking), nameof(SteamMatchmaking.AddRequestLobbyListStringFilter))]
    private static bool SteamStringFilterPrefix(ref string __0, ref string __1)
    {
        LobbyFilterState state = State;
        if (state == null) return true;
        if (__0 == "isInviteOnly" && state.Publicity == 0) return false;
        if (__0 != "isInviteOnly" || state.Publicity == 0) return true;

        __1 = DewPersistence.ToJson(state.Publicity == 2);
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SteamMatchmaking), nameof(SteamMatchmaking.RequestLobbyList))]
    private static void SteamRequestLobbyListPrefix()
    {
        LobbyFilterState state = State;
        if (state == null) return;

        if (state.Platform > 0)
        {
            string gate = state.Platform == 1 ? "STEAM" : "ALL";
            SteamMatchmaking.AddRequestLobbyListStringFilter(
                "crossPlayGate",
                DewPersistence.ToJson(gate),
                ELobbyComparison.k_ELobbyComparisonEqual);
        }

        if (state.Difficulty <= 0) return;

        string[] difficulties = { "", "diffEasy", "diffNormal", "diffHard", "diffNightmare", "diffLimbo" };
        SteamMatchmaking.AddRequestLobbyListStringFilter(
            "difficulty",
            DewPersistence.ToJson(difficulties[state.Difficulty]),
            ELobbyComparison.k_ELobbyComparisonEqual);
    }
}
