using HarmonyLib;

namespace DewPrimusHand.patch;

[HarmonyPatch(typeof(GameMod_MirageSkin))]
public class GameMod_MirageSkin_Patch
{

    [HarmonyPostfix]
    [HarmonyPatch("GetCurrentBaseMirageChance")]
    public static void GetCurrentBaseMirageChance_Postfix(GameMod_MirageSkin __instance, ref float __result)
    {
        __result *= DewPrimusHand.Instance.Config.LittleMonsterMirageChanceMultiplier;
    }
    
}