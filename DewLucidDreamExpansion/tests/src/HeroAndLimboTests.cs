using System;
using System.Linq;
using DewLucidDreamExpansion.content;
using DewLucidDreamExpansion.controller;
using DewLucidDreamExpansion.patch;
using DewLucidDreamExpansion.ui;
using HarmonyLib;

internal static class HeroAndLimboTests
{
    internal static void Run(Action<bool, string> check)
    {
        var previousPool = Dew.allStarTypes.ToArray();
        Dew.allStarTypes.Clear();
        Dew.allStarTypes.AddRange(new[] { typeof(StarB), typeof(SelectedHeroStar), typeof(OtherHeroStar) });
        try
        {
            var first = new DewPlayer { guid = "hero-one", hero = new Hero() };
            first.hero.owner = first;
            var other = new DewPlayer { guid = "hero-two", hero = new OtherHero() };
            other.hero.owner = other;
            var firstPool = AstronomyController.GetCandidates(first.hero).Select(star => star.GetType()).ToArray();
            var otherPool = AstronomyController.GetCandidates(other.hero).Select(star => star.GetType()).ToArray();
            check(firstPool.Contains(typeof(SelectedHeroStar)) && firstPool.Contains(typeof(StarB)) &&
                !firstPool.Contains(typeof(OtherHeroStar)), "First hero gets only own and universal stars");
            check(otherPool.Contains(typeof(OtherHeroStar)) && otherPool.Contains(typeof(StarB)) &&
                !otherPool.Contains(typeof(SelectedHeroStar)), "Second hero excludes base hero's stars despite inheritance");
            var shrine = new Shrine_Astronomy();
            shrine.PopulatePlayerChoices(first);
            shrine.PopulatePlayerChoices(other);
            check(shrine.choices[first.guid].All(choice => firstPool.Any(type => type.Name == choice.typeName)) &&
                shrine.choices[other.guid].All(choice => otherPool.Any(type => type.Name == choice.typeName)),
                "Each player's actual choices stay inside their hero pool");
            shrine.choices[first.guid] = new[] { new ChoiceShrineItem { typeName = nameof(OtherHeroStar), level = 5 } };
            shrine.SimulateUse(first.hero);
            check(shrine.openedSelections == 1 && shrine.choices[first.guid].All(choice => choice.typeName != nameof(OtherHeroStar)),
                "Opening restored foreign choices repairs them before showing selection");
            shrine.choices[first.guid] = new[] { new ChoiceShrineItem { typeName = nameof(OtherHeroStar), level = 5 } };
            int rewards = NetworkedManagerBase<ActorManager>.instance.allActors.OfType<StarEffect>().Count();
            AstronomyController.Choose(shrine, 0, new Mirror.NetworkConnectionToClient { player = first });
            check(NetworkedManagerBase<ActorManager>.instance.allActors.OfType<StarEffect>().Count() == rewards &&
                shrine.choices[first.guid].All(choice => choice.typeName != nameof(OtherHeroStar)),
                "Server rejects foreign hero star even from stale saved selection");
        }
        finally
        {
            Dew.allStarTypes.Clear();
            Dew.allStarTypes.AddRange(previousPool);
        }

        var harmony = new Harmony("LucidDreamExpansion.Tests.LimboUi");
        harmony.CreateClassProcessor(typeof(LimboDepthSelectorPatch)).Patch();
        harmony.CreateClassProcessor(typeof(LimboDepthIconPatch)).Patch();
        try
        {
            GameMod_Limbo.completedDepth = 6;
            var selector = new UI_Lobby_Limbo_DepthSelect();
            selector.SimulateStart();
            var items = selector.parent.GetComponentsInChildren<UI_Lobby_Limbo_DepthSelect_Item>(true);
            check(items.Select(item => item.depth).OrderBy(depth => depth).SequenceEqual(Enumerable.Range(1, 9)),
                "Native Start initially builds six entries and patched selector adds seven through nine");
            check(items.All(item => item.icon.depth == item.depth && item.icon.depthText.text == item.depth.ToString()),
                "Extended icons retain seven through nine despite native six-depth clamp");
            LimboDepthSelector.EnsureItems(selector);
            check(selector.parent.GetComponentsInChildren<UI_Lobby_Limbo_DepthSelect_Item>(true).Length == 9,
                "Repeated selector refresh does not duplicate entries");
            check(items.Single(item => item.depth == 7).icon.state == LimboDepthState.Available &&
                items.Single(item => item.depth == 8).icon.state == LimboDepthState.Locked,
                "Expanded icons retain sequential unlock state");
            items.Single(item => item.depth == 7).Click();
            check(GameMod_Limbo.selectedDepth == 7, "New entry selects native seventh depth");
            items.Single(item => item.depth == 8).Click();
            check(GameMod_Limbo.selectedDepth == 7, "Locked eighth entry keeps native restriction");
            GameMod_Limbo.completedDepth = 8;
            items.Single(item => item.depth == 9).Click();
            check(GameMod_Limbo.selectedDepth == 9, "Ninth depth can be selected after eighth completion");
            Mirror.NetworkServer.active = false;
            items.Single(item => item.depth == 7).Click();
            check(GameMod_Limbo.selectedDepth == 9, "Clients cannot change selected depth");
            Mirror.NetworkServer.active = true;
            var cached = new UI_Lobby_Limbo_DepthSelect();
            for (int depth = 1; depth <= 6; depth++) UnityEngine.Object.Instantiate(cached.prefab, cached.parent).Setup(depth);
            LimboDepthSelector.RefreshExisting();
            check(cached.parent.GetComponentsInChildren<UI_Lobby_Limbo_DepthSelect_Item>(true).Length == 9,
                "Hot-load refresh fills already initialized six-entry selector");
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            LimboDepthSelector.Clear();
            Mirror.NetworkServer.active = true;
        }
    }
}
