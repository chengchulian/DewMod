using System;
using System.Collections.Generic;
using DewLucidDreamExpansion.controller;
using HarmonyLib;

namespace DewLucidDreamExpansion.patch;

[HarmonyPatch(typeof(DewPersistence), nameof(DewPersistence.ApplyGameData))]
internal static class RestoreRunPatch
{
    // 保留原有完成回调，并在界面结束加载前补齐本 Mod 的本局状态。
    private static void Prefix(ref Action onFinish)
    {
        Action original = onFinish;
        onFinish = () =>
        {
            try { RunStateController.RestoreRun(); }
            finally { original?.Invoke(); }
        };
    }
}

[HarmonyPatch(typeof(DewPersistence), nameof(DewPersistence.ApplyPlayerData))]
internal static class RestorePlayerStarsPatch
{
    // 重连单独恢复玩家时，原生方法内部已执行完批量创建；全局读档交给完成回调处理。
    private static void Postfix(DewPlayer player, List<(uint, Action)> batchActions)
    {
        if (batchActions == null) RunStateController.RestoreStars(player.hero);
    }
}
