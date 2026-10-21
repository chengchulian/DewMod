using System;
using System.Linq;
using DewLucidDreamExpansion.content;
using DewLucidDreamExpansion.controller;

internal static class Program
{
    private static int assertions;

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }

    private static DewPlayer Player(string guid)
    {
        var player = new DewPlayer { guid = guid, hero = new Hero() };
        player.hero.owner = player;
        return player;
    }

    private static int Rewards(DewPlayer player) => NetworkedManagerBase<ActorManager>.instance.allActors
        .OfType<StarEffect>().Count(s => s.victim == player.hero);

    public static void Main()
    {
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        var first = Player("first");
        var second = Player("second");
        var connection = new Mirror.NetworkConnectionToClient { player = first };
        Dew.allStarTypes.AddRange(new[] { typeof(StarA), typeof(StarB), typeof(StarC), typeof(StarD),
            typeof(OtherHeroStar), typeof(IncompatibleStar), typeof(MovementStar) });
        first.hero.loadout.GetStarList(StarType.Life).Add(new LoadoutStarItem { name = nameof(StarA), level = 1 });
        first.hero.Skill.Movement = null;
        var candidates = AstronomyController.GetCandidates(first.hero);
        Check(candidates.Count == 3, "Exclude carried, other-hero, incompatible and missing-movement stars");

        var shrine = new Shrine_Astronomy();
        shrine.PopulatePlayerChoices(first);
        shrine.PopulatePlayerChoices(second);
        Check(shrine.choices[first.guid].Length == 3, "Three choices");
        Check(shrine.choices[first.guid].Select(c => c.typeName).Distinct().Count() == 3, "Distinct choices");
        Check(shrine.choices[second.guid].Length == 3, "Independent second-player choices");
        AstronomyController.Choose(shrine, -1, connection);
        AstronomyController.Choose(shrine, 3, connection);
        AstronomyController.Choose(shrine, 0, null);
        Check(Rewards(first) == 0, "Reject malformed selections and missing sender");
        first.hero.position = new UnityEngine.Vector3 { x = 20 };
        AstronomyController.Choose(shrine, 0, connection);
        Check(Rewards(first) == 0, "Reject distant selection");
        first.hero.position = default;
        shrine.isLocked = true;
        AstronomyController.Choose(shrine, 0, connection);
        Check(Rewards(first) == 0, "Reject locked shrine");
        shrine.isLocked = false;
        shrine.afford = AffordType.No;
        AstronomyController.Choose(shrine, 0, connection);
        Check(Rewards(first) == 0, "Reject unaffordable selection");
        shrine.afford = AffordType.Yes;
        Mirror.NetworkServer.active = false;
        AstronomyController.Choose(shrine, 0, connection);
        Check(Rewards(first) == 0, "Client cannot award stars");
        Mirror.NetworkServer.active = true;
        AstronomyController.Choose(shrine, 0, connection);
        Check(Rewards(first) == 1, "Award selected star");
        Check(NetworkedManagerBase<ActorManager>.instance.allActors.OfType<StarEffect>().Single().skillLevel == 5,
            "Award at maximum level");
        AstronomyController.Choose(shrine, 1, connection);
        Check(Rewards(first) == 1, "Reject duplicate click");
        AstronomyController.Choose(shrine, 0, new Mirror.NetworkConnectionToClient { player = second });
        Check(Rewards(second) == 1, "First player does not consume second player's reward");
        Check(AstronomyController.GetCandidates(first.hero).Count == 2, "Exclude in-run reward next time");

        var smallPool = new Shrine_Astronomy();
        smallPool.PopulatePlayerChoices(first);
        Check(smallPool.choices[first.guid].Length == 2, "Offer remaining stars when fewer than three exist");
        string stale = smallPool.choices[first.guid][0].typeName;
        first.hero.loadout.GetStarList(StarType.Life).Add(new LoadoutStarItem { name = stale, level = 1 });
        AstronomyController.Choose(smallPool, 0, connection);
        Check(Rewards(first) == 1, "Reject choice acquired after options were populated");
        Check(smallPool.choices[first.guid].Length == 1, "Refresh stale options");
        AstronomyController.Choose(smallPool, 0, connection);
        Check(Rewards(first) == 2, "Refreshed choice still available");
        var exhausted = new Shrine_Astronomy();
        exhausted.PopulatePlayerChoices(first);
        Check(exhausted.choices[first.guid].Length == 0 && !exhausted.CanInteract(first.hero), "Empty pool is not interactive");
        var joining = Player("joining");
        joining.hero = null;
        exhausted.PopulatePlayerChoices(joining);
        Check(exhausted.choices[joining.guid].Length == 0, "Joining player with no hero is handled");
        joining.hero = new Hero { owner = joining };
        NetworkedManagerBase<ActorManager>.instance.allActors.Add(exhausted);
        AstronomyController.RefreshHeroChoices(joining.hero);
        Check(exhausted.choices[joining.guid].Length == 3, "Populate choices after joining hero is ready");

        var lord = new Monster();
        AstronomyController.RewardLord(lord);
        AstronomyController.RewardLord(lord);
        Check(Dew.createdShrines == 1, "One shrine per lord");
        var restoredLord = new Monster();
        foreach (var item in lord.persistentData) restoredLord.persistentData[item.Key] = item.Value;
        AstronomyController.RewardLord(restoredLord);
        Check(Dew.createdShrines == 1, "Saved reward marker prevents repeat after restore");
        Mirror.NetworkServer.active = false;
        AstronomyController.RewardLord(new Monster());
        Check(Dew.createdShrines == 1, "Client cannot create shrine");
        Mirror.NetworkServer.active = true;
        var wallet = new DewPlayer { gold = 30, dreamDust = 70 };
        wallet.AddGold(10);
        Check(wallet.gold == 30 && wallet.dreamDust == 80, "Gold pickup becomes dust without converting existing balance");
        wallet.AddDreamDust(20);
        Check(wallet.gold == 50 && wallet.dreamDust == 80, "Dust pickup becomes gold");
        wallet.EarnGold(15);
        Check(wallet.gold == 50 && wallet.dreamDust == 95 && wallet.dustEvents == 1 && wallet.goldEvents == 0,
            "Gold earning converts balance and event exactly once");
        wallet.EarnDreamDust(25);
        Check(wallet.gold == 75 && wallet.dreamDust == 95 && wallet.goldEvents == 1 && wallet.dustEvents == 1,
            "Dust earning converts balance and event exactly once");
        wallet.AddGold(0);
        wallet.AddDreamDust(-5);
        wallet.EarnGold(0);
        wallet.EarnDreamDust(-5);
        Check(wallet.gold == 75 && wallet.dreamDust == 95 && wallet.goldEvents == 1 && wallet.dustEvents == 1,
            "Zero and negative income do not mutate balances or emit events");
        wallet.AddGold(int.MaxValue);
        wallet.AddDreamDust(int.MaxValue);
        Check(wallet.gold == int.MaxValue && wallet.dreamDust == int.MaxValue, "Income saturates without integer overflow");
        Mirror.NetworkServer.active = false;
        Check(!ResourceInversionController.TryInvertAdd(wallet, 10, true) &&
            !ResourceInversionController.TryInvertEarn(wallet, 10, false), "Client cannot invert income");
        Mirror.NetworkServer.active = true;
        wallet = new DewPlayer { throwOnEarn = true };
        try { ResourceInversionController.TryInvertEarn(wallet, 10, true); }
        catch (InvalidOperationException) { }
        wallet.throwOnEarn = false;
        wallet.EarnGold(10);
        Check(wallet.dreamDust == 10 && wallet.gold == 0, "Conversion guard clears after an exception");
        AstronomySpawnTests.Run(Check);
        HeroAndLimboTests.Run(Check);
        CurseAcquisitionTests.Run(Check);
        Console.WriteLine($"PASS: {assertions} regression assertions");
    }
}
