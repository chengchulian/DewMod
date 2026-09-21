using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DewIdentityChange.config;

public sealed class LoadoutSnapshot
{
    private static readonly Dictionary<HeroSkill, LoadoutSnapshot> _heroSkillSnapshot = new();
    private static readonly Dictionary<string, LoadoutSnapshot> _heroTypeSnapshot = new();
    private static readonly HashSet<string> _trustedHeroTypeSnapshot = new();

    private AssetRef<SkillTrigger>[] _q;
    private AssetRef<SkillTrigger>[] _r;
    private AssetRef<SkillTrigger>[] _trait;
    private AssetRef<SkillTrigger>[] _movement;

    public static void Capture(HeroSkill hs)
    {
        if (hs == null)
        {
            return;
        }

        string heroType = GetHeroType(hs);
        if (!string.IsNullOrEmpty(heroType) && _heroTypeSnapshot.TryGetValue(heroType, out var typeSnapshot))
        {
            _heroSkillSnapshot[hs] = typeSnapshot;
            return;
        }

        if (_heroSkillSnapshot.ContainsKey(hs))
        {
            EnsureHeroTypeSnapshot(heroType, _heroSkillSnapshot[hs]);
            return;
        }

        var snapshot = Create(hs);
        _heroSkillSnapshot[hs] = snapshot;
        EnsureHeroTypeSnapshot(heroType, snapshot);
    }

    private static LoadoutSnapshot Create(HeroSkill hs)
    {
        return new LoadoutSnapshot
        {
            _q = Clone(hs.loadoutQ),
            _r = Clone(hs.loadoutR),
            _trait = Clone(hs.loadoutTrait),
            _movement = Clone(hs.loadoutMovement)
        };
    }

    public static void Switch(bool isOn)
    {
        if (isOn)
        {
            CaptureLoadedHeroSkills();
            TurnOnAll();
        }
        else
        {
            RestoreAll();
        }
    }

    public static void RestoreCaptured()
    {
        RestoreAll();
    }

    public static void CaptureLoadedHeroSkills()
    {
        CaptureKnownHeroPrefabs();

        string selectedHeroType = DewPlayer.local?.selectedHeroType;
        if (!string.IsNullOrEmpty(selectedHeroType))
        {
            var selectedHero = DewResources.GetByShortTypeName<Hero>(selectedHeroType);
            Capture(selectedHero?.GetComponent<HeroSkill>());
        }

        foreach (var heroSkill in Resources.FindObjectsOfTypeAll<HeroSkill>())
        {
            Capture(heroSkill);
        }
    }

    private static AssetRef<SkillTrigger>[] Clone(AssetRef<SkillTrigger>[] src)
    {
        if (src == null) return null;

        var arr = new AssetRef<SkillTrigger>[src.Length];
        src.CopyTo(arr, 0);
        return arr;
    }

    private static void Restore(HeroSkill hs)
    {
        if (!TryGetSnapshot(hs, out var snapshot))
        {
            return;
        }

        hs.loadoutQ = Clone(snapshot._q);
        hs.loadoutR = Clone(snapshot._r);
        hs.loadoutTrait = Clone(snapshot._trait);
        hs.loadoutMovement = Clone(snapshot._movement);
    }

    private static bool TryGetSnapshot(HeroSkill hs, out LoadoutSnapshot snapshot)
    {
        if (hs == null)
        {
            snapshot = null;
            return false;
        }

        if (_heroSkillSnapshot.TryGetValue(hs, out snapshot))
        {
            return true;
        }

        string heroType = GetHeroType(hs);
        if (!string.IsNullOrEmpty(heroType) && _heroTypeSnapshot.TryGetValue(heroType, out snapshot))
        {
            _heroSkillSnapshot[hs] = snapshot;
            return true;
        }

        snapshot = null;
        return false;
    }

    private static void RestoreAll()
    {
        CaptureKnownHeroPrefabs();
        foreach (var heroSkill in _heroSkillSnapshot.Keys.ToArray())
        {
            Restore(heroSkill);
        }
    }

    private static void TurnOnAll()
    {
        CaptureKnownHeroPrefabs();
        foreach (var heroSkill in _heroSkillSnapshot.Keys.ToArray())
        {
            TurnOn(heroSkill);
        }
    }

    private static void TurnOn(HeroSkill hs)
    {
        TryGetSnapshot(hs, out var snapshot);
        hs.loadoutQ = BuildLoadout(HeroSkillLocation.Q, snapshot?._q);
        hs.loadoutR = BuildLoadout(HeroSkillLocation.R, snapshot?._r);
        hs.loadoutTrait = BuildLoadout(HeroSkillLocation.Identity, snapshot?._trait);
        hs.loadoutMovement = BuildLoadout(HeroSkillLocation.Movement, snapshot?._movement);
    }

    private static void CaptureKnownHeroPrefabs()
    {
        foreach (Type heroType in Dew.allHeroes)
        {
            if (!Dew.IsHeroIncludedInGame(heroType.Name))
            {
                continue;
            }

            var lightHero = DewResources.GetByType<Hero>(heroType, ResourceLoadSettings.Light);
            CaptureTrustedTypeSnapshot(lightHero?.GetComponent<HeroSkill>());

            var hero = DewResources.GetByType<Hero>(heroType);
            Capture(hero?.GetComponent<HeroSkill>());
        }
    }

    private static string GetHeroType(HeroSkill hs)
    {
        return hs == null ? null : hs.GetComponent<Hero>()?.GetType().Name;
    }

    private static void EnsureHeroTypeSnapshot(string heroType, LoadoutSnapshot snapshot)
    {
        if (!string.IsNullOrEmpty(heroType) && !_heroTypeSnapshot.ContainsKey(heroType))
        {
            _heroTypeSnapshot[heroType] = snapshot;
        }
    }

    private static void CaptureTrustedTypeSnapshot(HeroSkill hs)
    {
        if (hs == null)
        {
            return;
        }

        string heroType = GetHeroType(hs);
        if (string.IsNullOrEmpty(heroType) || _trustedHeroTypeSnapshot.Contains(heroType))
        {
            return;
        }

        var snapshot = Create(hs);
        _heroTypeSnapshot[heroType] = snapshot;
        _trustedHeroTypeSnapshot.Add(heroType);
        RebindHeroTypeSnapshot(heroType, snapshot);
    }

    private static void RebindHeroTypeSnapshot(string heroType, LoadoutSnapshot snapshot)
    {
        foreach (var heroSkill in _heroSkillSnapshot.Keys.ToArray())
        {
            if (GetHeroType(heroSkill) == heroType)
            {
                _heroSkillSnapshot[heroSkill] = snapshot;
            }
        }
    }

    private static AssetRef<SkillTrigger>[] BuildLoadout(
        HeroSkillLocation location,
        AssetRef<SkillTrigger>[] fallback)
    {
        var skills = HeroSkillSource.SkillNamesByType[location]
            .Select(name => DewResources.GetByShortTypeName<SkillTrigger>(name))
            .Where(skill => skill != null)
            .ToArray();

        if (skills.Length == 0)
        {
            return Clone(fallback);
        }

        return skills.ToAssetRefs();
    }
}
