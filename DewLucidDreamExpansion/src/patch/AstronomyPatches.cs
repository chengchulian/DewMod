using DewLucidDreamExpansion.config;
using DewLucidDreamExpansion.content;
using DewLucidDreamExpansion.controller;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace DewLucidDreamExpansion.patch;

[HarmonyPatch(typeof(HeroSkill), nameof(HeroSkill.OnLateStartServer))]
internal static class AstronomyHeroReadyPatch
{
    private static void Postfix(HeroSkill __instance)
    {
        Hero hero = __instance.GetComponent<Hero>();
        Dew.CallDelayed(() => AstronomyController.RefreshHeroChoices(hero));
    }
}

// 复用原版 Command 的网络注册和发送路径，在房主实际执行位置替换星座领奖逻辑。
[HarmonyPatch(typeof(ChoiceShrine), "UserCode_CmdChoose__Int32__NetworkConnectionToClient")]
internal static class AstronomyChoicePatch
{
    private static bool Prefix(ChoiceShrine __instance, int index, NetworkConnectionToClient sender)
    {
        if (!(__instance is Shrine_Astronomy shrine)) return true;
        if (NetworkServer.active) AstronomyController.Choose(shrine, index, sender);
        return false;
    }
}

[HarmonyPatch(typeof(UI_InGame_FloatingWindow_ChoiceShrine_Item), nameof(UI_InGame_FloatingWindow_ChoiceShrine_Item.UpdateContent))]
internal static class AstronomyItemPatch
{
    private static void Postfix(UI_InGame_FloatingWindow_ChoiceShrine_Item __instance, ChoiceShrineItem d)
    {
        if (!(__instance.cachedObject.lightAsset is StarEffect star)) return;
        __instance.gemIcon.gameObject.SetActive(false);
        __instance.skillIcon.gameObject.SetActive(true);
        __instance.skillIcon.sprite = star.starIcon;
        __instance.skillPlusText.gameObject.SetActive(false);
        __instance.gemQualityText.gameObject.SetActive(false);
        __instance.rarityImage.color = LucidDream.GoodColor;
    }
}

[HarmonyPatch(typeof(UI_InGame_FloatingWindow_ChoiceShrine_Item), nameof(UI_InGame_FloatingWindow_ChoiceShrine_Item.ShowTooltip))]
internal static class AstronomyTooltipPatch
{
    private static bool Prefix(UI_InGame_FloatingWindow_ChoiceShrine_Item __instance, UI_TooltipManager tooltip)
    {
        if (!(__instance.cachedObject.lightAsset is StarEffect star)) return true;
        float strength = DreamRules.Active<LucidDream_FallenStarGod>() ? 0.5f : 1f;
        string description = DewLocalization.ConvertDescriptionNodesToText(DewLocalization.GetStarDescription(star.GetType()),
            new DewLocalization.DescriptionSettings
            {
                currentLevel = __instance.data.level,
                contextEntity = DewPlayer.local?.hero,
                contextObject = star,
                isSkillStar = true,
                starStrength = strength
            });
        tooltip.ShowRawTextTooltip(new TooltipSettings
        {
            mode = TooltipPositionMode.RawValue,
            position = __instance.transform.position + Vector3.up * 30f
        }, "<color=yellow><b>" + DewLocalization.GetStarName(star.GetType()) + "</b></color>\n" + description);
        return false;
    }
}

[HarmonyPatch(typeof(UI_InGame_FloatingWindow_ChoiceShrine), nameof(UI_InGame_FloatingWindow_ChoiceShrine.OnActivate))]
internal static class AstronomyWindowPatch
{
    private static void Postfix(UI_InGame_FloatingWindow_ChoiceShrine __instance)
    {
        if (ManagerBase<FloatingWindowManager>.instance.currentTarget is Shrine_Astronomy shrine &&
            DewPlayer.local != null && shrine.choices.TryGetValue(DewPlayer.local.guid, out var choices) && choices.Length == 0)
            __instance.descriptionText.text = LocalizationSource.GetLocalizationText("Astronomy_NoStars");
    }
}
