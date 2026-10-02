using HarmonyLib;
using UnityEngine;

namespace DewPrimusHand.patch;

[HarmonyPatch(typeof(RoomProps), nameof(RoomProps.TryGetGoodNodePosition))]
public static class RoomProps_Patch
{
    [HarmonyPostfix]
    public static void TryGetGoodNodePosition_Postfix(ref Vector3 position, ref bool __result)
    {
        if (RoomMod_HeroSoul_Patch.UseBossArenaCenter)
        {
            var arena = SingletonBehaviour<Room_BossArena>.instance;
            if (arena != null)
            {
                // 孤王之巅中的灵魂固定生成在地图中心。
                position = arena.center;
                __result = true;
                return;
            }
        }
    }
}

[HarmonyPatch(typeof(RoomMod_HeroSoul), nameof(RoomMod_HeroSoul.OnStartServer))]
public static class RoomMod_HeroSoul_Patch
{
    public static bool UseBossArenaCenter { get; private set; }

    [HarmonyPrefix]
    public static void OnStartServer_Prefix(out bool __state)
    {
        __state = UseBossArenaCenter;
        UseBossArenaCenter = SingletonBehaviour<Room_BossArena>.instance != null;
    }

    [HarmonyFinalizer]
    public static System.Exception OnStartServer_Finalizer(System.Exception __exception, bool __state)
    {
        UseBossArenaCenter = __state;
        return __exception;
    }
}
