using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DewLucidDreamExpansion.patch;
using HarmonyLib;

public class SkillTrigger : Actor
{
    public int level;
    public bool isCharacterSkill;
    private bool prepared;
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected virtual void OnPrepare() { }
    public void Prepare()
    {
        if (prepared) return;
        prepared = true;
        OnPrepare();
    }
}

public class Gem : Actor
{
    public int quality;
    private bool prepared;
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected virtual void OnPrepare() { }
    public void Prepare()
    {
        if (prepared) return;
        prepared = true;
        OnPrepare();
    }
}

public enum MerchandiseType { Skill, Gem, Treasure, Souvenir }
public struct MerchandiseData
{
    public MerchandiseType type;
    public int level;
    public int count;
    public int price;
}
public class PropEnt_Merchant_Base
{
    public readonly Dictionary<string, MerchandiseData[]> merchandises = new Dictionary<string, MerchandiseData[]>();
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void PopulatePlayerMerchandises(DewPlayer player) { }
}

internal static class CurseAcquisitionTests
{
    internal static void Run(Action<bool, string> check)
    {
        var harmony = new Harmony("LucidDreamExpansion.Tests.CurseAcquisition");
        harmony.CreateClassProcessor(typeof(CurseNewMemoryPatch)).Patch();
        harmony.CreateClassProcessor(typeof(CurseNewEssencePatch)).Patch();
        harmony.CreateClassProcessor(typeof(CurseMerchantDisplayPatch)).Patch();
        try
        {
            Mirror.NetworkServer.active = true;
            DreamRules.curseActive = true;
            var memory = new SkillTrigger { level = 9 };
            var essence = new Gem { quality = 250 };
            memory.Prepare();
            essence.Prepare();
            check(memory.level == 1 && essence.quality == 40, "Final acquisition overrides explicit values and room bonuses");
            memory.level += 4;
            essence.quality += 80;
            memory.Prepare();
            essence.Prepare();
            check(memory.level == 5 && essence.quality == 120, "Upgrade and merge values survive repeated prepare requests");
            var restoredMemory = new SkillTrigger { level = 5, isNewInstance = false };
            var restoredEssence = new Gem { quality = 120, isNewInstance = false };
            restoredMemory.Prepare();
            restoredEssence.Prepare();
            check(restoredMemory.level == 5 && restoredEssence.quality == 120, "Restored upgraded inventory is preserved");
            var character = new SkillTrigger { level = 7, isCharacterSkill = true };
            character.Prepare();
            check(character.level == 7, "Hero intrinsic skills retain their level");

            var player = new DewPlayer { guid = "shop" };
            var merchant = new PropEnt_Merchant_Base();
            var original = new[] {
                new MerchandiseData { type = MerchandiseType.Skill, level = 8, count = 2, price = 300 },
                new MerchandiseData { type = MerchandiseType.Gem, level = 210, count = 1, price = 400 },
                new MerchandiseData { type = MerchandiseType.Treasure, level = 6 }
            };
            merchant.merchandises[player.guid] = original;
            merchant.PopulatePlayerMerchandises(player);
            var items = merchant.merchandises[player.guid];
            check(items[0].level == 1 && items[1].level == 40 && items[2].level == 6,
                "Shop synchronizes memory and essence values without altering other merchandise");
            check(original[0].level == 8 && items[0].count == 2 && items[0].price == 300,
                "Shop replaces the array for synchronization and retains count and price");
            var purchasedMemory = new SkillTrigger { level = items[0].level };
            var purchasedEssence = new Gem { quality = items[1].level };
            purchasedMemory.Prepare();
            purchasedEssence.Prepare();
            check(purchasedMemory.level == 1 && purchasedEssence.quality == 40, "Purchase matches displayed shop values");
            merchant.PopulatePlayerMerchandises(player);
            check(ReferenceEquals(items, merchant.merchandises[player.guid]), "Unchanged refresh avoids redundant synchronization");

            foreach (bool client in new[] { false, true })
            {
                DreamRules.curseActive = client;
                Mirror.NetworkServer.active = !client;
                var untouchedMemory = new SkillTrigger { level = 12 };
                var untouchedEssence = new Gem { quality = 360 };
                untouchedMemory.Prepare();
                untouchedEssence.Prepare();
                merchant.merchandises[player.guid] = original;
                merchant.PopulatePlayerMerchandises(player);
                check(untouchedMemory.level == 12 && untouchedEssence.quality == 360 &&
                    ReferenceEquals(original, merchant.merchandises[player.guid]),
                    client ? "Client does not apply authoritative acquisition changes" : "Disabled curse preserves acquisitions and shop");
            }
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            DreamRules.curseActive = false;
            Mirror.NetworkServer.active = true;
        }
    }
}
