using HarmonyLib;
using UnityEngine;

namespace DewClientWorldReveal.patch;

[HarmonyPatch(typeof(UI_InGame_World_NodeItem), "Setup")]
internal static class UI_InGame_World_NodeItem_Patch
{
    private static readonly System.Reflection.MethodInfo SetIconActive = AccessTools.Method(
        typeof(UI_InGame_World_NodeItem), "SetActive");

    private static void Postfix(UI_InGame_World_NodeItem __instance)
    {
        WorldNodeData node = __instance.node;
        if (node.status != WorldNodeStatus.Unexplored)
        {
            return;
        }

        // 只修改当前客户端的 UI 副本，不写回 ZoneManager.nodes。
        node.status = WorldNodeStatus.RevealedFull;
        Traverse.Create(__instance).Property("node").SetValue(node);

        SetIcon(__instance, "unexplored", false);
        SetIcon(__instance, "normal", node.type.IsNormalNode());
        SetIcon(__instance, "merchant", node.type == WorldNodeType.Merchant);
        SetIcon(__instance, "quest", node.type == WorldNodeType.Quest);
        SetIcon(__instance, "exitBoss", node.type == WorldNodeType.ExitBoss);

        if (node.modifiers == null)
        {
            return;
        }

        int dotIndex = 0;
        for (int i = 0; i < node.modifiers.Count && dotIndex < __instance.modifierDots.Length; i++)
        {
            if (!node.IsModifierVisible(i))
            {
                continue;
            }

            RoomModifierBase modifier = DewResources.GetByShortTypeName<RoomModifierBase>(node.modifiers[i].type);
            if (modifier == null)
            {
                continue;
            }

            var dot = __instance.modifierDots[dotIndex++];
            dot.gameObject.SetActive(true);
            dot.color = modifier.mainColor;
            if (modifier.mapSprite != null)
            {
                dot.sprite = modifier.mapSprite;
            }
            dot.transform.localScale *= modifier.mapSpriteScale;
        }
    }

    private static void SetIcon(UI_InGame_World_NodeItem item, string fieldName, bool active)
    {
        object icon = AccessTools.Field(typeof(UI_InGame_World_NodeItem), fieldName).GetValue(item);
        SetIconActive.Invoke(item, new[] { icon, (object)active });
    }
}
