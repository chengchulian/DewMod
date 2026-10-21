using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DewLucidDreamExpansion.config;
using DewLucidDreamExpansion.content;
using DewLucidDreamExpansion.controller;
using HarmonyLib;
using Mirror;

namespace DewLucidDreamExpansion.patch;

internal static class DreamRules
{
    internal static bool Active<T>() where T : LucidDream
    {
        var settings = NetworkedManagerBase<GameSettingsManager>.instance;
        return NetworkedManagerBase<GameManager>.instance != null && settings != null &&
               settings.activeLucidDreams.Contains(typeof(T).Name);
    }
}

[HarmonyPatch(typeof(DewResources), nameof(DewResources.Load))]
internal static class ResourcePatch
{
    private static bool Prefix(string guid, ref UnityEngine.Object __result)
    {
        if (!DreamRegistry.IsGuid(guid)) return true;
        __result = DreamRegistry.GetPrefab(guid);
        return false;
    }
}

[HarmonyPatch(typeof(Dew), nameof(Dew.IsLucidDreamIncludedInGame))]
internal static class ContentPatch
{
    private static void Postfix(string type, ref bool __result)
    {
        if (DreamRegistry.Dreams.Any(t => t.Name == type)) __result = true;
    }
}

[HarmonyPatch(typeof(DewProfile), nameof(DewProfile.Validate))]
internal static class ProfilePatch
{
    private static void Postfix(DewProfile __instance) => DreamRegistry.RegisterProfile(__instance);
}

[HarmonyPatch(typeof(GameSettingsManager), nameof(GameSettingsManager.UpdateAvailableLucidDreams))]
internal static class AvailablePatch
{
    private static void Postfix(GameSettingsManager __instance)
    {
        if (!NetworkServer.active) return;
        foreach (Type type in DreamRegistry.Dreams)
            if (!__instance.availableLucidDreams.Contains(type.Name)) __instance.availableLucidDreams.Add(type.Name);
    }
}

[HarmonyPatch(typeof(DewLocalization), nameof(DewLocalization.GetUIValue))]
internal static class LocalizationPatch
{
    private static bool Prefix(string key, ref string __result)
    {
        if (!LocalizationSource.TryGet(key, out string text)) return true;
        __result = text;
        return false;
    }
}

[HarmonyPatch(typeof(GameMod_Limbo), nameof(GameMod_Limbo.GetMaxDepths))]
internal static class LimboPatch
{
    private static void Postfix(ref int __result) => __result = ui.LimboDepthSelector.MaxDepth;
}

// 四个收入入口共用控制器，避免 AddGold/AddDreamDust 相互递归。
[HarmonyPatch]
internal static class IncomePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(DewPlayer), nameof(DewPlayer.AddGold));
        yield return AccessTools.Method(typeof(DewPlayer), nameof(DewPlayer.AddDreamDust));
    }

    private static bool Prefix(DewPlayer __instance, int amount, MethodBase __originalMethod)
    {
        return !DreamRules.Active<LucidDream_ResourceInversion>() ||
               !ResourceInversionController.TryInvertAdd(__instance, amount, __originalMethod.Name == nameof(DewPlayer.AddGold));
    }
}

// Earn 系列需要同步转换通知事件，让收入相关星座按实际资源触发。
[HarmonyPatch]
internal static class EarnPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(DewPlayer), nameof(DewPlayer.EarnGold));
        yield return AccessTools.Method(typeof(DewPlayer), nameof(DewPlayer.EarnDreamDust));
    }

    private static bool Prefix(DewPlayer __instance, int amount, MethodBase __originalMethod)
    {
        return !DreamRules.Active<LucidDream_ResourceInversion>() ||
               !ResourceInversionController.TryInvertEarn(__instance, amount, __originalMethod.Name == nameof(DewPlayer.EarnGold));
    }
}

[HarmonyPatch(typeof(Actor), "PrepareAndSpawn")]
internal static class RuntimePrefabPatch
{
    private static void Prefix(Actor __instance)
    {
        if (DreamRegistry.IsCustom(__instance.GetType()) && !__instance.gameObject.activeSelf)
            __instance.gameObject.SetActive(true);
    }
}

[HarmonyPatch(typeof(SpawnManager), "SpawnFromDewDatabaseHandler")]
internal static class RuntimeClientPrefabPatch
{
    private static void Postfix(UnityEngine.GameObject __result)
    {
        if (__result != null && __result.GetComponent<Actor>() is Actor actor &&
            DreamRegistry.IsCustom(actor.GetType()) && !__result.activeSelf)
            __result.SetActive(true);
    }
}

// 仅约束新战利品的初始数值，不拦截持有物品的升级、合成或读档。
[HarmonyPatch(typeof(LootManager), nameof(LootManager.SelectSkillLevel))]
internal static class SkillLootLevelPatch
{
    private static void Postfix(ref int __result)
    {
        if (DreamRules.Active<LucidDream_PrimordialCurse>()) __result = 1;
    }
}

[HarmonyPatch(typeof(LootManager), nameof(LootManager.SelectGemQuality))]
internal static class GemLootQualityPatch
{
    private static void Postfix(ref int __result)
    {
        if (DreamRules.Active<LucidDream_PrimordialCurse>()) __result = 40;
    }
}

// 区域奖励可能在随机等级之后继续叠加，最终选项同步前再次约束显示值。
[HarmonyPatch(typeof(ChoiceShrine), nameof(ChoiceShrine.PopulatePlayerChoices))]
internal static class CurseChoiceDisplayPatch
{
    private static void Postfix(ChoiceShrine __instance, DewPlayer player)
    {
        if (!NetworkServer.active || !DreamRules.Active<LucidDream_PrimordialCurse>() ||
            !__instance.choices.TryGetValue(player.guid, out var original)) return;
        var choices = original.ToArray();
        bool changed = false;
        for (int i = 0; i < choices.Length; i++)
        {
            var prefab = DewResources.GetByShortTypeName(choices[i].typeName, ResourceLoadSettings.Light);
            int level = prefab is Gem ? 40 : prefab is SkillTrigger ? 1 : choices[i].level;
            if (level == choices[i].level) continue;
            choices[i].level = level;
            changed = true;
        }
        if (changed) __instance.choices[player.guid] = choices;
    }
}

[HarmonyPatch(typeof(Actor), "InvokeOnPrepareIfDidnt")]
internal static class PreparePatch
{
    private static void Prefix(Actor __instance)
    {
        if (!NetworkServer.active) return;
        if (__instance is StarEffect star && DreamRules.Active<LucidDream_FallenStarGod>())
            star.strength = Math.Min(star.strength, 0.5f);
    }
}

[HarmonyPatch(typeof(SkillTrigger), nameof(SkillTrigger.GetCooldownTimeMultiplier))]
internal static class DodgeCooldownPatch
{
    private static void Postfix(SkillTrigger __instance, ref float __result)
    {
        if (DreamRules.Active<LucidDream_HeavyFootsteps>() && __instance.owner is Hero hero &&
            hero.Skill.Movement == __instance) __result *= 1.5f;
    }
}

[HarmonyPatch(typeof(SkillTrigger), nameof(SkillTrigger.GetCooldownTimeOnLevel))]
internal static class DodgeCooldownDisplayPatch
{
    private static void Postfix(SkillTrigger __instance, ref float __result)
    {
        if (DreamRules.Active<LucidDream_HeavyFootsteps>() && __instance.owner is Hero hero &&
            hero.Skill.Movement == __instance) __result *= 1.5f;
    }
}

[HarmonyPatch(typeof(ZoneManager), nameof(ZoneManager.SetCurrentNodeIndexAndRevealAdjacent))]
internal static class TelescopePatch
{
    private static void Postfix(ZoneManager __instance, int index)
    {
        if (!NetworkServer.active || !DreamRules.Active<LucidDream_Telescope>() || index < 0 || index >= __instance.nodes.Count) return;
        for (int i = 0; i < __instance.nodes.Count; i++)
        {
            int distance = __instance.GetNodeDistance(index, i);
            var node = __instance.nodes[i];
            if (distance >= 0 && distance <= 2 && node.status < WorldNodeStatus.RevealedFull)
            {
                node.status = WorldNodeStatus.RevealedFull;
                __instance.nodes[i] = node;
            }
        }
    }
}

