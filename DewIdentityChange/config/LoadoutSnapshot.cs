using System.Collections.Generic;
using System.Linq;

namespace DewIdentityChange.config;

public sealed class LoadoutSnapshot
{
    private static readonly Dictionary<HeroSkill, LoadoutSnapshot> _heroSkillSnapshot = new();


    public AssetRef<SkillTrigger>[] Q;
    public AssetRef<SkillTrigger>[] R;
    public AssetRef<SkillTrigger>[] Trait;
    public AssetRef<SkillTrigger>[] Movement;

    public static AssetRef<SkillTrigger>[] Clone(AssetRef<SkillTrigger>[] src)
    {
        if (src == null) return null;
        var arr = new AssetRef<SkillTrigger>[src.Length];
        src.CopyTo(arr, 0);
        return arr;
    }

    public static void Capture(HeroSkill hs)
    {
        _heroSkillSnapshot[hs] = new LoadoutSnapshot
        {
            Q = Clone(hs.loadoutQ),
            R = Clone(hs.loadoutR),
            Trait = Clone(hs.loadoutTrait),
            Movement = Clone(hs.loadoutMovement)
        };
    }

    public static void Restore(HeroSkill hs)
    {
        if (!_heroSkillSnapshot.TryGetValue(hs, out var snapshot))
        {
            return;
        }

        hs.loadoutQ = Clone(snapshot.Q);
        hs.loadoutR = Clone(snapshot.R);
        hs.loadoutTrait = Clone(snapshot.Trait);
        hs.loadoutMovement = Clone(snapshot.Movement);
    }

    public static void Switch(bool isOn)
    {
        if (isOn)
        {
            TurnOnAll();
        }
        else
        {
            RestoreAll();
        }
    }


    public static void RestoreAll()
    {
        foreach (var heroSkill in _heroSkillSnapshot.Keys)
        {
            Restore(heroSkill);
        }
    }

    public static void TurnOnAll()
    {
        foreach (var heroSkill in _heroSkillSnapshot.Keys)
        {
            TurnOn(heroSkill);
        }
    }

    public static void TurnOn(HeroSkill hs)
    {
        hs.loadoutQ = HeroSkillSource.SkillNamesByType[HeroSkillLocation.Q]
            .Select(name => DewResources.GetByShortTypeName<SkillTrigger>(name))
            .ToArray()
            .ToAssetRefs();
        hs.loadoutR = HeroSkillSource.SkillNamesByType[HeroSkillLocation.R]
            .Select(name => DewResources.GetByShortTypeName<SkillTrigger>(name))
            .ToArray()
            .ToAssetRefs();

        hs.loadoutTrait = HeroSkillSource.SkillNamesByType[HeroSkillLocation.Identity]
            .Select(name => DewResources.GetByShortTypeName<SkillTrigger>(name))
            .ToArray()
            .ToAssetRefs();

        hs.loadoutMovement = HeroSkillSource.SkillNamesByType[HeroSkillLocation.Movement]
            .Select(name => DewResources.GetByShortTypeName<SkillTrigger>(name))
            .ToArray()
            .ToAssetRefs();
    }
}