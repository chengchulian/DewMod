using HarmonyLib;
using DewPrimusHand.util;
using UnityEngine;

namespace DewPrimusHand.patch;

[HarmonyPatch(typeof(FinalDamageData))]
public class FinalDamageData_Patch
{
    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPatch(new[] { typeof(DamageData), typeof(float), typeof(Entity) })]
    [HarmonyPostfix]
    public static void Constructor_Postfix(ref FinalDamageData __instance, DamageData data, float armorAmount,
        Entity victim)
    {
        if (victim is BossMonster)
        {
            float multiplier = DewPrimusHand.Instance.Config.BossSingleInjuryHealthMultiplier;
            if (multiplier < 0.999f)
            {
                float cap = victim.Status.maxHealth * DifficultyMath.SanitizeMultiplier(multiplier);
                if (__instance.amount > cap)
                {
                    __instance.discardedAmount += __instance.amount - cap;
                    __instance.amount = cap;
                }
            }
        }
    }
}
