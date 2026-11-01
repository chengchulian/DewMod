using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using DewLucidDreamExpansion.content;
using DewLucidDreamExpansion.controller;
using DewLucidDreamExpansion.patch;
using HarmonyLib;

public static class DewPersistence
{
    public static Action pendingFinish;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ApplyGameData(Action onFinish = null) => pendingFinish = onFinish;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ApplyPlayerData(DewPlayer player, List<(uint, Action)> batchActions = null) { }
}

internal static class RunStateTests
{
    internal static void Run(Action<bool, string> check)
    {
        var actors = NetworkedManagerBase<ActorManager>.instance.allActors;
        var game = NetworkedManagerBase<GameManager>.instance;
        var savedActors = actors.ToArray();
        var harmony = new Harmony("LucidDreamExpansion.Tests.RunState");
        harmony.CreateClassProcessor(typeof(RestoreRunPatch)).Patch();
        harmony.CreateClassProcessor(typeof(RestorePlayerStarsPatch)).Patch();
        try
        {
            actors.Clear();
            var player = new DewPlayer { guid = "restore", hero = new Hero() };
            player.hero.owner = player;
            var shrine = new Shrine_Astronomy();
            shrine.PopulatePlayerChoices(player);
            AstronomyController.Choose(shrine, 0, new Mirror.NetworkConnectionToClient { player = player });
            StarEffect awarded = actors.OfType<StarEffect>().Single();
            check(player.hero.persistentData.Keys.Any(k => k.StartsWith("LucidDreamExpansion::AstronomyStar::")),
                "Actual shrine selection records the awarded star in hero run data");
            RunStateController.RestoreStars(player.hero);
            check(actors.OfType<StarEffect>().Count() == 1 && ReferenceEquals(awarded, actors.OfType<StarEffect>().Single()),
                "Already restored native star is retained without recreating or resetting it");

            // 模拟一次完整 SL：只复制持久字典，所有旧英雄、效果和实体均丢弃。
            var restored = new Hero { owner = player };
            foreach (var item in player.hero.persistentData) restored.persistentData[item.Key] = item.Value;
            player.hero = restored;
            actors.Clear();
            DewPlayer.gamePlayers.Add(player);
            game.dreams.Add(nameof(LucidDream_AstronomyMaster));
            bool finished = false;
            DewPersistence.ApplyGameData(() => finished = actors.OfType<StarEffect>().Count() == 1 &&
                actors.OfType<LucidDream_AstronomyMaster>().Count() == 1);
            check(actors.Count == 0, "Run restoration waits until native load completion");
            DewPersistence.pendingFinish();
            StarEffect recovered = actors.OfType<StarEffect>().Single();
            check(finished && recovered.GetType() == awarded.GetType() && recovered.victim == restored &&
                recovered.skillLevel == awarded.skillLevel && recovered.strength == awarded.strength,
                "Save/load restores selected star and missing dream before forwarding completion callback");
            check(restored.loadout.GetStarList(StarType.Flexible).Count == 0 &&
                !AstronomyController.GetCandidates(restored).Any(s => s.GetType() == recovered.GetType()),
                "Recovered reward stays run-only and is excluded from later choices");
            DewPersistence.pendingFinish();
            check(actors.Count == 2, "Repeated load completion adds neither duplicate dream nor duplicate star");
            var secondLoad = new Hero { owner = player };
            foreach (var item in restored.persistentData) secondLoad.persistentData[item.Key] = item.Value;
            player.hero = secondLoad;
            actors.Clear();
            DewPersistence.ApplyGameData();
            DewPersistence.pendingFinish();
            check(actors.OfType<StarEffect>().Single().victim == secondLoad &&
                actors.OfType<LucidDream_AstronomyMaster>().Count() == 1,
                "Second consecutive save/load restores reward and HUD actor with a null completion callback");
            var other = new DewPlayer { guid = "independent", hero = new Hero() };
            other.hero.owner = other;
            RunStateController.RememberStar(other.hero, new StarD { skillLevel = 3, strength = 0.5f });
            DewPlayer.gamePlayers.Add(other);
            RunStateController.RestoreRun();
            check(actors.OfType<StarD>().Any(s => s.victim == other.hero && s.skillLevel == 3 && s.strength == 0.5f) &&
                actors.OfType<StarEffect>().Count(s => s.victim == secondLoad) == 1,
                "Multiple players recover their own rewards without sharing or consuming another player's selection");
            player.hero = restored;
            game.dreams.Add(nameof(LucidDream_PrimordialCurse));
            Mirror.NetworkServer.active = false;
            actors.Clear();
            RunStateController.RestoreRun();
            check(actors.Count == 0, "Client never creates restored gameplay actors");
            Mirror.NetworkServer.active = true;

            // 原生恢复成功时保持其状态；补缺路径保留已存的 50% 星座强度。
            RunStateController.RememberStar(restored, new StarB { skillLevel = 4, strength = 0.5f });
            restored.persistentData["LucidDreamExpansion::AstronomyStar::StarC"] = "5|NaN";
            restored.persistentData["LucidDreamExpansion::AstronomyStar::StarD"] = "bad";
            restored.persistentData["LucidDreamExpansion::AstronomyStar::MissingStar"] = "5|1";
            DewPersistence.ApplyPlayerData(player, new List<(uint, Action)>());
            check(actors.Count == 0, "External native batch is left to global load completion");
            DewPersistence.ApplyPlayerData(player);
            StarEffect half = actors.OfType<StarB>().Single();
            check(half.skillLevel == 4 && half.strength == 0.5f && !actors.OfType<StarC>().Any() &&
                !actors.OfType<StarD>().Any(), "Standalone reconnect restores stars and skips invalid saved values");
            DewPersistence.ApplyPlayerData(player);
            check(actors.OfType<StarB>().Count() == 1, "Repeated reconnect restoration is idempotent");
            actors.Clear();
            restored.isActive = false;
            RunStateController.RestoreStars(restored);
            check(actors.Count == 0, "Destroyed hero does not receive recovered stars");
            RunStateController.RestoreStars(null);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            Mirror.NetworkServer.active = true;
            DewPlayer.gamePlayers.Clear();
            game.dreams.Clear();
            actors.Clear();
            actors.AddRange(savedActors);
        }
    }
}
