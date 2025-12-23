using System;
using System.Collections.Generic;
using System.Linq;
using DewIdentityChange.config;
using HarmonyLib;

namespace DewIdentityChange.patch;

[HarmonyPatch(typeof(HeroSkill))]
public class HeroSkill_Patch
{
    
    private static readonly Dictionary<HeroSkill, LoadoutSnapshot> _original
        = new();    
    [HarmonyPostfix]
    [HarmonyPatch("Awake")]
    public static void Awake_Postfix(HeroSkill __instance)
    {
        // 首次进入：缓存原始配置
        if (!_original.ContainsKey(__instance))
        {
            _original[__instance] = LoadoutSnapshot.Capture(__instance);
        }

        Apply(__instance);
    }
    private static void Apply(HeroSkill hs)
    {
        if (!DewIdentityChange.Instance.config.enable)
        {
            // OFF：恢复原始值
            _original[hs].Restore(hs);
            return;
        }

        // ON：应用替代方案（示例）
        hs.loadoutQ = HeroSkillSource.SkillNamesByType[HeroSkillLocation.Q]
            .Select(name => DewResources.GetByShortTypeName<SkillTrigger>(name))
            .ToArray()
            .ToAssetRefs();
        hs.loadoutR = HeroSkillSource.SkillNamesByType[HeroSkillLocation.R]
            .Select(name => DewResources.GetByShortTypeName<SkillTrigger>(name))
            .ToArray()
            .ToAssetRefs();

        hs.loadoutTrait= HeroSkillSource.SkillNamesByType[HeroSkillLocation.Identity]
            .Select(name => DewResources.GetByShortTypeName<SkillTrigger>(name))
            .ToArray()
            .ToAssetRefs();

        hs.loadoutMovement= HeroSkillSource.SkillNamesByType[HeroSkillLocation.Movement]
            .Select(name => DewResources.GetByShortTypeName<SkillTrigger>(name))
            .ToArray()
            .ToAssetRefs();
    }

}