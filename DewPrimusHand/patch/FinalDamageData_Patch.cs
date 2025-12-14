using HarmonyLib;
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
                __instance.amount = Mathf.Min(__instance.amount, victim.Status.maxHealth * multiplier);
            }
        }
    }
}