using System;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace DewIdentityChange.util;

internal static class UiEffect
{
    // 1.4 returns GameObject; older builds returned void with the same parameters.
    private static readonly MethodInfo PlayNew = AccessTools.Method(
        typeof(DewEffect), nameof(DewEffect.PlayNew), new[] { typeof(GameObject), typeof(NetworkIdentity) });

    private static bool _loggedFailure;

    public static void Play(GameObject effect)
    {
        if (effect == null || _loggedFailure)
        {
            return;
        }

        try
        {
            if (PlayNew == null)
            {
                throw new MissingMethodException("DewEffect.PlayNew(GameObject, NetworkIdentity)");
            }

            PlayNew.Invoke(null, new object[] { effect, null });
        }
        catch (Exception exception)
        {
            _loggedFailure = true;
            Debug.LogWarning($"[DewIdentityChange] Could not play UI effect: {exception}");
        }
    }
}
