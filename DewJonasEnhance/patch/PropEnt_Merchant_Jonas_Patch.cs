using HarmonyLib;

namespace DewJonasEnhance.patch;

[HarmonyPatch(typeof(PropEnt_Merchant_Jonas))]
public class PropEnt_Merchant_Jonas_Patch
{
    [HarmonyPatch("OnCreate")]
    [HarmonyPrefix]
    public static void OnCreate_Prefix(PropEnt_Merchant_Jonas __instance)
    {
        if (!DewJonasEnhance.Instance.Config.Invincible)
        {
            return;
        }

        if (!__instance.isServer)
        {
            return;
        }

        NetworkedManagerBase<ActorManager>.instance.serverActor.CreateBasicEffect(__instance, new InvulnerableEffect(),
            float.PositiveInfinity, "ConsoleInvul", DuplicateEffectBehavior.DoNothing);
    }
}