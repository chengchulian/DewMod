using System;
using DewLucidDreamExpansion.content;
using DewLucidDreamExpansion.controller;
using DewLucidDreamExpansion.patch;
using HarmonyLib;
using UnityEngine;

internal static class AstronomySpawnTests
{
    internal static void Run(Action<bool, string> check)
    {
        var harmony = new Harmony("LucidDreamExpansion.Tests.BossDeath");
        harmony.CreateClassProcessor(typeof(BossRewardPatch)).Patch();
        try
        {
            DreamRules.astronomyActive = true;
            Mirror.NetworkServer.active = true;
            int before = Dew.createdShrines;
            var boss = new BossMonster { position = new Vector3 { x = 30 } };
            boss.SimulateDeath(true);
            check(Dew.createdShrines == before, "Interrupted death creates no reward");
            boss.SimulateDeath();
            check(Dew.createdShrines == before + 1, "Harmony boss death callback creates reward without a Kill subscription");
            check(Dew.lastCreated.position.x == 33, "Spawn at navigable reward position near boss");
            boss.SimulateDeath();
            check(Dew.createdShrines == before + 1, "Repeated death callback creates no duplicate shrine");
            DreamRules.astronomyActive = false;
            new BossMonster().SimulateDeath();
            check(Dew.createdShrines == before + 1, "Disabled astronomy creates no shrine");
            DreamRules.astronomyActive = true;
            Mirror.NetworkServer.active = false;
            new BossMonster().SimulateDeath();
            check(Dew.createdShrines == before + 1, "Client death callback creates no shrine");
            Mirror.NetworkServer.active = true;
            for (int failure = 1; failure <= 3; failure++)
            {
                var retry = new BossMonster();
                Dew.spawnFailure = failure;
                int errors = Debug.errors;
                retry.SimulateDeath();
                check(retry.persistentData.Count == 0 && Debug.errors == errors + 1,
                    "Failed, null or inactive spawn logs an error without consuming reward marker");
                Dew.spawnFailure = 0;
                int count = Dew.createdShrines;
                retry.SimulateDeath();
                check(Dew.createdShrines == count + 1 && retry.persistentData.Count == 1,
                    "Successful retry records reward only after shrine creation");
            }
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            DreamRules.astronomyActive = false;
            Dew.spawnFailure = 0;
            Mirror.NetworkServer.active = true;
        }

        var source = new Shrine_Astronomy();
        var target = new Shrine_Astronomy();
        source.gameObject.layer = 14;
        source.transform.localScale = new Vector3 { x = 2 };
        var on = new GameObject();
        var off = new GameObject();
        var nested = new GameObject();
        source.transform.Add(on.transform);
        source.transform.Add(off.transform);
        on.transform.Add(nested.transform);
        source.availableEffect = on;
        source.unavailableEffect = off;
        source.useEffect = nested;
        ShrinePresentation.Copy(source, target);
        check(target.model == null && target.transform.childCount == 2,
            "Native shrine with null model still copies its complete visual hierarchy");
        check(target.availableEffect == target.transform.GetChild(0).gameObject && target.availableEffect != on,
            "Available effect belongs to custom shrine, not native prefab");
        check(target.unavailableEffect == target.transform.GetChild(1).gameObject && target.unavailableEffect != off,
            "Unavailable effect retains sibling network path in custom shrine");
        check(target.useEffect == target.transform.GetChild(0).GetChild(0).gameObject && target.useEffect != nested,
            "Nested effect reference resolves inside copied hierarchy");
        check(target.gameObject.layer == 14 && target.transform.localScale.x == 2,
            "Presentation retains native layer and scale");
        check(source.transform.childCount == 2 && source.availableEffect == on && on.transform.parent == source.transform,
            "Presentation copy preserves original prefab");
    }
}
