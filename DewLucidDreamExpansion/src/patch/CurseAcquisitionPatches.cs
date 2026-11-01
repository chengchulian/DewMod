using System.Linq;
using DewLucidDreamExpansion.content;
using HarmonyLib;
using Mirror;

namespace DewLucidDreamExpansion.patch;

// 首次生成时约束最终数值，覆盖掉落加成和商店购买；存档恢复不重新应用。
[HarmonyPatch(typeof(SkillTrigger), "OnPrepare")]
internal static class CurseNewMemoryPatch
{
    private static void Postfix(SkillTrigger __instance)
    {
        if (NetworkServer.active && !MirrorOfRemorsePatch.IsTransforming && __instance.isNewInstance && !__instance.isCharacterSkill &&
            DreamRules.Active<LucidDream_PrimordialCurse>()) __instance.level = 1;
    }
}

[HarmonyPatch(typeof(Gem), "OnPrepare")]
internal static class CurseNewEssencePatch
{
    private static void Postfix(Gem __instance)
    {
        if (NetworkServer.active && !MirrorOfRemorsePatch.IsTransforming && __instance.isNewInstance &&
            DreamRules.Active<LucidDream_PrimordialCurse>()) __instance.quality = 40;
    }
}

// 商品记录也同步约束，让商店显示值与购买后物品一致。
[HarmonyPatch(typeof(PropEnt_Merchant_Base), nameof(PropEnt_Merchant_Base.PopulatePlayerMerchandises))]
internal static class CurseMerchantDisplayPatch
{
    private static void Postfix(PropEnt_Merchant_Base __instance, DewPlayer player)
    {
        if (!NetworkServer.active || !DreamRules.Active<LucidDream_PrimordialCurse>() ||
            !__instance.merchandises.TryGetValue(player.guid, out var original)) return;
        var items = original.ToArray();
        bool changed = false;
        for (int i = 0; i < items.Length; i++)
        {
            int level = items[i].type == MerchandiseType.Skill ? 1 :
                items[i].type == MerchandiseType.Gem ? 40 : items[i].level;
            if (items[i].level == level) continue;
            items[i].level = level;
            changed = true;
        }
        if (changed) __instance.merchandises[player.guid] = items;
    }
}
