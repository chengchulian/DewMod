using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DewSuperSmart;

internal static class SafeZoneTracker
{
    private const string SafeZoneTypeName = "Ai_Mon_Ink_BossWhiteNight_Cataclysm_SafeZone";
    private const string SafeZoneRpcName = "UserCode_RpcPlayShieldEffect__Vector3__Single";

    private static readonly Dictionary<int, Entry> Entries = new Dictionary<int, Entry>();
    private static readonly MethodInfo RpcPostfix = typeof(SafeZoneTracker).GetMethod(
        nameof(OnSafeZoneRpc),
        BindingFlags.Static | BindingFlags.NonPublic);

    private sealed class Entry
    {
        public Actor Actor;
        public readonly List<Vector3> Centers = new List<Vector3>();
    }

    public static void Install(Harmony harmony)
    {
        Type type = AccessTools.TypeByName(SafeZoneTypeName);
        MethodInfo method = type != null ? AccessTools.Method(type, SafeZoneRpcName) : null;
        if (method == null || RpcPostfix == null)
        {
            Debug.LogWarning($"[DewSuperSmart] Safe-zone RPC not found; remote cataclysm centers will use reflection fallback.");
            return;
        }

        try
        {
            harmony.Patch(method, postfix: new HarmonyMethod(RpcPostfix));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[DewSuperSmart] Failed to patch the safe-zone RPC; remote cataclysm centers will use reflection fallback. {exception.Message}");
        }
    }

    public static bool TryGetCenters(Actor actor, out IReadOnlyList<Vector3> centers)
    {
        centers = null;
        if (actor == null)
        {
            return false;
        }

        int id = actor.GetInstanceID();
        if (!Entries.TryGetValue(id, out Entry entry))
        {
            return false;
        }

        if (entry.Actor == null || entry.Actor != actor || entry.Actor.IsNullOrInactive())
        {
            Entries.Remove(id);
            return false;
        }

        centers = entry.Centers;
        return entry.Centers.Count > 0;
    }

    public static void Clear()
    {
        Entries.Clear();
    }

    private static void OnSafeZoneRpc(object __instance, Vector3 pos)
    {
        if (__instance is not Actor actor || !Dew.IsOkay(pos))
        {
            return;
        }

        int id = actor.GetInstanceID();
        if (!Entries.TryGetValue(id, out Entry entry) || entry.Actor != actor)
        {
            entry = new Entry { Actor = actor };
            Entries[id] = entry;
        }

        for (int i = 0; i < entry.Centers.Count; i++)
        {
            if ((entry.Centers[i].ToXY() - pos.ToXY()).sqrMagnitude < 0.01f)
            {
                return;
            }
        }

        entry.Centers.Add(pos);
    }
}
