using System;
using System.Collections.Generic;
using HarmonyLib;

namespace DewGemSlotCount.controller;

// 游戏重载会生成新程序集；按 Mod 身份清理所有旧实例的补丁，避免保留旧程序集的静态引用。
public static class GemPatchLifecycle
{
    private const string OwnerPrefix = "com.chengchulian.DewMod.DewGemSlotCount";
    private const string OwnerId = OwnerPrefix + ".runtime";

    public static void Install()
    {
        Remove();
        new Harmony(OwnerId).PatchAll(typeof(GemPatchLifecycle).Assembly);
    }

    public static void Remove()
    {
        var owners = new HashSet<string>();
        foreach (var method in Harmony.GetAllPatchedMethods())
        {
            var info = Harmony.GetPatchInfo(method);
            if (info == null) continue;
            foreach (string owner in info.Owners)
                if (owner.StartsWith(OwnerPrefix, StringComparison.Ordinal)) owners.Add(owner);
        }
        var harmony = new Harmony(OwnerId);
        foreach (string owner in owners) harmony.UnpatchAll(owner);
    }
}
