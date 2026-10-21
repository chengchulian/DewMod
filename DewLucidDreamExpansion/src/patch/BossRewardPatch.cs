using System;
using DewLucidDreamExpansion.content;
using DewLucidDreamExpansion.controller;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace DewLucidDreamExpansion.patch;

// 跟随原生领主死亡结算；死亡中断未结束时不会进入此方法。
[HarmonyPatch(typeof(BossMonster), "OnDeath")]
internal static class BossRewardPatch
{
    private static void Postfix(BossMonster __instance)
    {
        if (!NetworkServer.active || !DreamRules.Active<LucidDream_AstronomyMaster>()) return;
        try
        {
            AstronomyController.RewardLord(__instance);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[LucidDreamExpansion] 天文大师奖励创建失败：{__instance.GetType().Name}\n{exception}");
        }
    }
}
