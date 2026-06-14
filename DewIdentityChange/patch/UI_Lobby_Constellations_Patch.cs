using DewIdentityChange.config;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DewIdentityChange.patch;

[HarmonyPatch(typeof(UI_Lobby_Constellations))]
public static class UI_Lobby_Constellations_Patch
{
    private static readonly MethodInfo SetLoadout =
        AccessTools.PropertySetter(typeof(UI_Lobby_Constellations), nameof(UI_Lobby_Constellations.loadout));

    private static readonly MethodInfo SetSelectedLoadoutIndex =
        AccessTools.PropertySetter(typeof(UI_Lobby_Constellations), nameof(UI_Lobby_Constellations.selectedLoadoutIndex));

    private static readonly MethodInfo SetIsDirty =
        AccessTools.PropertySetter(typeof(UI_Lobby_Constellations), nameof(UI_Lobby_Constellations.isDirty));

    [HarmonyPrefix]
    [HarmonyPatch(nameof(UI_Lobby_Constellations.ClickOnLoadout))]
    public static bool ClickOnLoadout_Prefix()
    {
        LoadoutPageMapper.EnsureProfileLoadoutPages(DewSave.profileMain);
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(UI_Lobby_Constellations.LoadLoadout))]
    public static bool LoadLoadout_Prefix(UI_Lobby_Constellations __instance, int index)
    {
        if (!LoadoutPageMapper.IsIdentityEnabled)
        {
            return true;
        }

        string heroType = DewPlayer.local?.selectedHeroType;
        if (!LoadoutPageMapper.TryGetStorageLoadouts(DewSave.profileMain, heroType, out var loadouts))
        {
            return true;
        }

        int visibleIndex = LoadoutPageMapper.NormalizeVisibleIndex(index);
        SetSelectedLoadoutIndex.Invoke(__instance, new object[] { visibleIndex });
        SetLoadout.Invoke(__instance, new object[] { new HeroLoadoutData(loadouts[visibleIndex]) });
        SetIsDirty.Invoke(__instance, new object[] { false });

        __instance.onLoadoutChanged?.Invoke();
        foreach (var toggle in __instance.loadoutToggleParent.GetComponentsInChildren<UI_Toggle>())
        {
            toggle.isChecked = toggle.index == visibleIndex;
        }

        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(UI_Lobby_Constellations.LoadLoadout))]
    public static void LoadLoadout_Postfix(UI_Lobby_Constellations __instance)
    {
        foreach (var toggle in __instance.loadoutToggleParent.GetComponentsInChildren<UI_Toggle>())
        {
            toggle.isChecked = toggle.index == __instance.selectedLoadoutIndex;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(UI_Lobby_Constellations.Save))]
    public static bool Save_Prefix(UI_Lobby_Constellations __instance)
    {
        if (!LoadoutPageMapper.IsIdentityEnabled)
        {
            return true;
        }

        string heroType = DewPlayer.local?.selectedHeroType;
        if (!LoadoutPageMapper.TryGetStorageLoadouts(DewSave.profileMain, heroType, out var loadouts))
        {
            return true;
        }

        List<UI_Lobby_Constellations_HeroConstellation_StarSlot> incompatibleSlots =
            UI_Lobby_Constellations_HeroConstellation.instance.slots
                .Where(slot => slot.isIncompatible)
                .ToList();
        if (incompatibleSlots.Count > 0)
        {
            string msg = DewLocalization.GetUIValue("Constellation_IncompatibleFound") + "\n";
            string template = DewLocalization.GetUIValue("Constellations_ThisStarRequiresSpecificMemory_Template");
            foreach (var slot in incompatibleSlots)
            {
                msg += "\n" + string.Format(
                    template,
                    UI_Lobby_Constellations_StarDetails.GetColoredPrefixedStarName(slot.star),
                    "<color=" + Dew.GetRarityColorHex(Rarity.Character) + ">" +
                    DewLocalization.GetSkillName(DewLocalization.GetSkillKey(slot.star.skillType), 0) +
                    "</color>");
            }

            ManagerBase<MessageManager>.instance.ShowMessage(new DewMessageSettings
            {
                owner = __instance,
                rawContent = msg
            });
            return false;
        }

        DewEffect.PlayNew(__instance.fxSaveChanges);
        int visibleIndex = LoadoutPageMapper.NormalizeVisibleIndex(__instance.selectedLoadoutIndex);
        loadouts[visibleIndex] = __instance.loadout;
        __instance.LoadLoadout(visibleIndex);
        DewSave.SaveProfileMain();
        DewPlayer.local.CmdSetHeroLoadoutData(__instance.loadout);
        if (DewInput.currentMode == InputMode.Gamepad)
        {
            ManagerBase<GlobalUIManager>.instance.SetFocusOnComponent(
                SingletonBehaviour<UI_Lobby_Constellations_AlphaStar>.instance);
        }

        return false;
    }
}
