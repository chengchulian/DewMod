using System.Collections.Generic;
using DewIdentityChange.ui;
using HarmonyLib;
using UnityEngine;

namespace DewIdentityChange.patch;

[HarmonyPatch(typeof(UI_Lobby_Constellations_Skills_ContextMenu), "OnEnable")]
internal static class MemoryMenu_Patch
{
    [HarmonyPrefix]
    private static void Prefix(ref List<UI_Lobby_Constellations_Skills_ContextMenu_Item> ____items)
    {
        MemoryMenuCache.RemoveDestroyed(ref ____items);
    }

    [HarmonyPostfix]
    private static void Postfix(UI_Lobby_Constellations_Skills_ContextMenu __instance,
        List<UI_Lobby_Constellations_Skills_ContextMenu_Item> ____items)
    {
        if (DewIdentityChange.Instance?.IsIdentityEnabled == true &&
            __instance.itemsParent != null &&
            ____items != null)
        {
            MemoryMenuLayout.Apply(__instance, __instance.itemsParent, ____items, __instance.currentSkill);
        }
    }
}

[HarmonyPatch(typeof(UI_Lobby_Loadout_AvailableSkills), "OnEnable")]
internal static class AvailableMemoryMenu_Patch
{
    [HarmonyPrefix]
    private static void Prefix(ref List<UI_Lobby_Loadout_AvailableSkills_Item> ____items)
    {
        MemoryMenuCache.RemoveDestroyed(ref ____items);
    }

    [HarmonyPostfix]
    private static void Postfix(UI_Lobby_Loadout_AvailableSkills __instance,
        List<UI_Lobby_Loadout_AvailableSkills_Item> ____items)
    {
        if (DewIdentityChange.Instance?.IsIdentityEnabled == true &&
            __instance.itemsParent != null &&
            ____items != null)
        {
            MemoryMenuLayout.Apply(__instance, __instance.itemsParent, ____items, __instance.currentSkill);
        }
    }

}

internal static class MemoryMenuCache
{
    public static void RemoveDestroyed<T>(ref List<T> items) where T : Component
    {
        if (items == null)
        {
            items = new List<T>();
            return;
        }

        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (items[i] == null)
            {
                items.RemoveAt(i);
            }
        }
    }
}

[HarmonyPatch(typeof(UI_Lobby_Loadout_AvailableSkills_Item))]
internal static class AvailableMemoryNavigation_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(UI_Lobby_Loadout_AvailableSkills_Item.OnGamepadDpadLeft))]
    private static bool Left(UI_Lobby_Loadout_AvailableSkills_Item __instance, ref bool __result)
    {
        return Move(__instance, Vector3.left, ref __result);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(UI_Lobby_Loadout_AvailableSkills_Item.OnGamepadDpadRight))]
    private static bool Right(UI_Lobby_Loadout_AvailableSkills_Item __instance, ref bool __result)
    {
        return Move(__instance, Vector3.right, ref __result);
    }

    private static bool Move(Component item, Vector3 direction, ref bool result)
    {
        var layout = item.GetComponentInParent<MemoryMenuLayout>();
        if (layout == null || !layout.HasViewport) return true;
        ManagerBase<GlobalUIManager>.instance.MoveFocus(direction, 89f, 1f, float.PositiveInfinity,
            f => f.GetTransform().IsChildOf(item.transform.parent));
        ManagerBase<GlobalUIManager>.instance.PlayTickSFX();
        result = true;
        return false;
    }
}
