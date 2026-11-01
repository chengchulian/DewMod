using System;
using HarmonyLib;
using UnityEngine;

namespace DewMorePlayers.patch;

// 1.4 moved lobby creation to EOS, and LobbyServiceEOS.GetInitialAttr_maxPlayers()
// returns a hardcoded 4 (ldc.i4.4 / ret). that value goes straight into
// CreateLobbyOptions.MaxLobbyMembers, so the lobby is created with 4 slots and the game
// rejects anyone past that, no matter what maxPlayers we set on DewNetworkStartSettings.
// capacity is never updated after creation, so it has to be right at creation time.
//
// heads up: the method is 2 instructions, so mono inlines it. the mod has to be enabled
// before the caller gets jitted (active at boot), otherwise the patch is a no-op and
// everything looks fine in the logs.
internal static class LobbyServiceEOS_MaxPlayers_Patch
{
    const string Target = "LobbyServiceEOS:GetInitialAttr_maxPlayers";

    internal static void Apply(Harmony harmony)
    {
        try
        {
            var m = AccessTools.Method(Target);
            if (m == null)
            {
                Debug.LogWarning($"[DewMorePlayers] {Target} not found, EOS lobby size falls back to the game default");
                return;
            }

            harmony.Patch(m, postfix: new HarmonyMethod(
                AccessTools.Method(typeof(LobbyServiceEOS_MaxPlayers_Patch), nameof(Postfix))));
        }
        catch (Exception e)
        {
            Debug.LogError($"[DewMorePlayers] failed to patch {Target}: {e}");
        }
    }

    static void Postfix(ref int __result)
    {
        __result = DewMorePlayers.MaxPlayers;
        Debug.Log($"[DewMorePlayers] EOS lobby capacity -> {__result}");
    }
}
