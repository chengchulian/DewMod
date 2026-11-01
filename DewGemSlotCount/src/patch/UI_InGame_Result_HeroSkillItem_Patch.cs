using DewGemSlotCount.ui;
using DewGemSlotCount.controller;
using HarmonyLib;
using UnityEngine;

namespace DewGemSlotCount.patch;

// 收集统计组件前创建扩展槽，首次刷新即可包含全部图标和提示框。
[HarmonyPatch(typeof(UI_InGame_ResultView), "Refresh")]
public static class UI_InGame_ResultView_Patch
{
    [HarmonyPrefix]
    public static void Prefix(UI_InGame_ResultView __instance)
    {
        foreach (var skill in __instance.GetComponentsInChildren<UI_InGame_Result_HeroSkillItem>(true))
            UI_InGame_Result_HeroSkillItem_Patch.EnsureGroups(skill);
    }
}

[HarmonyPatch(typeof(UI_InGame_Result_HeroSkillItem), nameof(UI_InGame_Result_HeroSkillItem.UpdateAndGetScore))]
public static class UI_InGame_Result_HeroSkillItem_Patch
{
    // 直接刷新单项也支持全部容量。
    [HarmonyPrefix]
    public static void Prefix(UI_InGame_Result_HeroSkillItem __instance, DewGameResult data, int playerIndex, out int __state)
    {
        __state = data.players[playerIndex].maxGemCounts[(int)__instance.type];
        EnsureGroups(__instance);
        var player = data.players[playerIndex];
        if (player.capturedStarTooltipFields.TryGetValue(GemSlotStateSync.ResultKey, out string payload) &&
            GemSlotStateSync.TryParse(payload, out int[] counts))
            player.maxGemCounts[(int)__instance.type] = counts[(int)__instance.type];
    }

    // 渲染期间仅临时使用完整容量，结束及异常时均恢复原生数据，避免影响后续转发。
    [HarmonyFinalizer]
    public static void Finalizer(UI_InGame_Result_HeroSkillItem __instance, DewGameResult data, int playerIndex, int __state)
    {
        data.players[playerIndex].maxGemCounts[(int)__instance.type] = __state;
    }

    // 显式刷新精华，覆盖首次创建、切换玩家及历史结算。
    [HarmonyPostfix]
    public static void Postfix(UI_InGame_Result_HeroSkillItem __instance,
        DewGameResult data, int playerIndex, float scoreMultiplier)
    {
        int count = data.players[playerIndex].maxGemCounts[(int)__instance.type];
        GameObject single = GemSlotLayout.ShowSingle<UI_InGame_Result_HeroGemItem>(__instance.gemObjects234,
            __instance.skillIcon.transform, count == 1, slot => slot.index, (slot, index) => slot.index = index);
        if (single != null && count == 1)
            RefreshGems(single, data, playerIndex, scoreMultiplier);
        if (count >= 2 && count <= Constant.MaxGemCount)
            RefreshGems(__instance.gemObjects234[count - 2], data, playerIndex, scoreMultiplier);
    }

    // 共用官方镜像布局，保留原版结算数据与精华提示框绑定。
    public static void EnsureGroups(UI_InGame_Result_HeroSkillItem skill)
    {
        GemSlotLayout.Extend<UI_InGame_Result_HeroGemItem>(ref skill.gemObjects234,
            2, skill.skillIcon.transform, slot => slot.index, (slot, index) => slot.index = index);
    }

    // 刷新新槽位，避免首次创建时漏过结算组件快照。
    private static void RefreshGems(GameObject group, DewGameResult data, int playerIndex, float multiplier)
    {
        foreach (var gem in group.GetComponentsInChildren<UI_InGame_Result_HeroGemItem>(true))
            gem.UpdateAndGetScore(data, playerIndex, multiplier);
    }
}
