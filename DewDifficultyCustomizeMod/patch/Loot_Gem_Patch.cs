using System.Collections.Generic;
using System.Linq;
using DewDifficultyCustomizeMod.config;
using HarmonyLib;
using UnityEngine;

namespace DewDifficultyCustomizeMod.patch;

[HarmonyPatch(typeof(Loot_Gem))]
public class Loot_Gem_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(Loot_Gem.SelectGemAndQuality))]
    public static bool SelectGemAndQuality_Prefix(Loot_Gem __instance, Rarity rarity, out Gem gem, out int quality)
    {
        // 获取宝石池并存储到局部变量中
        var pool = new HashSet<string>(NetworkedManagerBase<LootManager>.instance.poolGemsByRarity[rarity]);

        // 将需要移除的宝石列表转换为 HashSet 以提高效率
        var removeGemsSet = new HashSet<string>(AttrCustomizeResources.Config.removeGems);
        pool.ExceptWith(removeGemsSet);

        // 如果池为空，则添加默认宝石
        if (pool.Count == 0)
        {
            pool.Add("Gem_C_Charcoal");
        }

        // 随机选择一个宝石并获取其质量
        gem = DewResources.GetByShortTypeName<Gem>(pool.ElementAt(Random.Range(0, pool.Count)));
        quality = __instance.SelectQuality(rarity);

        return false;
    }
}