using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DewIdentityChange.config;

public static class LoadoutPageMapper
{
    public const int DefaultLoadoutCount = 5;
    public const string IdentityHeroKeyPrefix = "DewIdentityChange_";

    private static readonly MethodInfo StarListRefresh =
        AccessTools.Method(typeof(UI_Lobby_Constellations_StarList), "Refresh", new System.Type[] { });

    private static readonly MethodInfo LoadoutSkillSlotRefresh =
        AccessTools.Method(typeof(UI_Lobby_Loadout_SkillSlot), "OnLoadoutChanged", new System.Type[] { });

    private static readonly MethodInfo SetLoadoutComponentHero =
        AccessTools.PropertySetter(typeof(UI_Lobby_Loadout_ComponentBase), nameof(UI_Lobby_Loadout_ComponentBase.hero));

    private static readonly MethodInfo SetLoadoutComponentLoadout =
        AccessTools.PropertySetter(typeof(UI_Lobby_Loadout_ComponentBase), nameof(UI_Lobby_Loadout_ComponentBase.loadout));

    public static bool IsIdentityEnabled => DewIdentityChange.Instance?.IsIdentityEnabled == true;

    public static string GetStorageHeroType(string heroType)
    {
        if (IsIdentityEnabled && !string.IsNullOrEmpty(heroType))
        {
            return IdentityHeroKeyPrefix + heroType;
        }

        return heroType;
    }

    public static int NormalizeVisibleIndex(int index)
    {
        return index >= 0 && index < DefaultLoadoutCount ? index : 0;
    }

    public static void EnsureProfileLoadoutPages(DewProfile profile)
    {
        if (!IsIdentityEnabled || profile?.heroLoadouts == null)
        {
            return;
        }

        foreach (string heroType in profile.heroLoadouts.Keys.ToArray())
        {
            if (!IsKnownHeroType(heroType))
            {
                continue;
            }

            EnsureStorageLoadouts(profile, heroType);
        }
    }

    public static bool TryGetStorageLoadouts(
        DewProfile profile,
        string heroType,
        out List<HeroLoadoutData> loadouts)
    {
        loadouts = null;
        if (profile?.heroLoadouts == null || string.IsNullOrEmpty(heroType))
        {
            return false;
        }

        EnsureStorageLoadouts(profile, heroType);
        return profile.heroLoadouts.TryGetValue(GetStorageHeroType(heroType), out loadouts) &&
               loadouts != null &&
               loadouts.Count > 0;
    }

    public static HeroLoadoutSwapState SwapHeroLoadoutsToStorage(DewProfile profile, string heroType)
    {
        if (!IsIdentityEnabled ||
            profile?.heroLoadouts == null ||
            string.IsNullOrEmpty(heroType) ||
            !profile.heroLoadouts.TryGetValue(heroType, out var originalLoadouts) ||
            !TryGetStorageLoadouts(profile, heroType, out var storageLoadouts) ||
            ReferenceEquals(originalLoadouts, storageLoadouts))
        {
            return null;
        }

        profile.heroLoadouts[heroType] = storageLoadouts;
        return new HeroLoadoutSwapState(profile, heroType, originalLoadouts);
    }

    public static void RestoreHeroLoadouts(HeroLoadoutSwapState state)
    {
        if (state?.Profile?.heroLoadouts == null || string.IsNullOrEmpty(state.HeroType))
        {
            return;
        }

        state.Profile.heroLoadouts[state.HeroType] = state.OriginalLoadouts;
    }

    public static int GetVisibleSelectedIndex(string heroType)
    {
        var settings = NetworkedManagerBase<GameSettingsManager>.instance?.GetLocalPreferredGameSettings();
        if (settings?.heroSelectedLoadoutIndex == null || string.IsNullOrEmpty(heroType))
        {
            return 0;
        }

        if (!settings.heroSelectedLoadoutIndex.TryGetValue(heroType, out int index))
        {
            index = 0;
        }

        int visibleIndex = NormalizeVisibleIndex(index);
        settings.heroSelectedLoadoutIndex[heroType] = visibleIndex;
        return visibleIndex;
    }

    public static int GetStorageSelectedIndex(string heroType)
    {
        return GetVisibleSelectedIndex(heroType);
    }

    public static void RefreshLobbyAfterConfigChange()
    {
        CloseOpenLoadoutMenus();
        RefreshLocalSelectedLoadout(forceConstellationsRefresh: true);
    }

    public static void RefreshLocalSelectedLoadout(bool forceConstellationsRefresh = false)
    {
        var player = DewPlayer.local;
        if (player == null ||
            player.state != PlayerState.InLobby ||
            string.IsNullOrEmpty(player.selectedHeroType) ||
            DewSave.profileMain == null)
        {
            return;
        }

        EnsureProfileLoadoutPages(DewSave.profileMain);

        string heroType = player.selectedHeroType;
        if (!TryGetStorageLoadouts(DewSave.profileMain, heroType, out var loadouts))
        {
            return;
        }

        int storageIndex = GetStorageSelectedIndex(heroType);
        if (storageIndex < 0 || storageIndex >= loadouts.Count)
        {
            return;
        }

        var loadout = CloneLoadout(loadouts[storageIndex]);
        if (DewBuildProfile.current.buildType != BuildType.DemoLite)
        {
            loadout.PopulateLevelsByLocalSaveData();
        }

        player.CmdSetHeroLoadoutData(loadout);
        RefreshLobbyLoadoutSkillSlots(heroType, loadout);
        RefreshConstellationsUi(heroType, forceConstellationsRefresh);
    }

    public static bool TrySetLocalSelectedLoadoutIndex(string heroType, int visibleIndex)
    {
        var settings = NetworkedManagerBase<GameSettingsManager>.instance?.GetLocalPreferredGameSettings();
        if (settings?.heroSelectedLoadoutIndex == null || string.IsNullOrEmpty(heroType))
        {
            return false;
        }

        int normalizedIndex = NormalizeVisibleIndex(visibleIndex);
        settings.heroSelectedLoadoutIndex[heroType] = normalizedIndex;

        if (!TryGetStorageLoadouts(DewSave.profileMain, heroType, out var loadouts) ||
            normalizedIndex >= loadouts.Count)
        {
            return false;
        }

        var loadout = CloneLoadout(loadouts[normalizedIndex]);
        if (DewBuildProfile.current.buildType != BuildType.DemoLite)
        {
            loadout.PopulateLevelsByLocalSaveData();
        }

        DewPlayer.local.CmdSetHeroLoadoutData(loadout);
        RefreshLobbyLoadoutSkillSlots(heroType, loadout);
        return true;
    }

    public static bool TryStoreLocalLoadout(HeroLoadoutData loadout)
    {
        string heroType = DewPlayer.local?.selectedHeroType;
        if (loadout == null ||
            string.IsNullOrEmpty(heroType) ||
            !TryGetStorageLoadouts(DewSave.profileMain, heroType, out var loadouts))
        {
            return false;
        }

        int visibleIndex = GetVisibleSelectedIndex(heroType);
        if (visibleIndex < 0 || visibleIndex >= loadouts.Count)
        {
            return false;
        }

        loadouts[visibleIndex] = CloneLoadout(loadout);
        return true;
    }

    private static void RefreshConstellationsUi(string heroType, bool force)
    {
        var constellations = SingletonBehaviour<UI_Lobby_Constellations>.softInstance;
        if (constellations == null || !constellations.isActiveAndEnabled)
        {
            return;
        }

        if (!force && constellations.isDirty)
        {
            return;
        }

        constellations.LoadLoadout(GetVisibleSelectedIndex(heroType));
        RefreshConstellationsStarList();
    }

    private static void RefreshConstellationsStarList()
    {
        var starList = SingletonBehaviour<UI_Lobby_Constellations_StarList>.softInstance;
        if (starList == null || !starList.isActiveAndEnabled)
        {
            return;
        }

        StarListRefresh?.Invoke(starList, null);
    }

    private static void RefreshLobbyLoadoutSkillSlots(string heroType, HeroLoadoutData loadout)
    {
        if (string.IsNullOrEmpty(heroType) || loadout == null)
        {
            return;
        }

        var hero = DewResources.GetByShortTypeName<Hero>(heroType);
        if (hero == null)
        {
            return;
        }

#pragma warning disable CS0618
        foreach (var slot in Object.FindObjectsOfType<UI_Lobby_Loadout_SkillSlot>(includeInactive: true))
#pragma warning restore CS0618
        {
            SetLoadoutComponentHero?.Invoke(slot, new object[] { (AssetRef<Hero>)hero });
            SetLoadoutComponentLoadout?.Invoke(slot, new object[] { loadout });
            LoadoutSkillSlotRefresh?.Invoke(slot, null);
        }
    }

    private static void CloseOpenLoadoutMenus()
    {
#pragma warning disable CS0618
        var constellationMenu = Object.FindObjectOfType<UI_Lobby_Constellations_Skills_ContextMenu>(includeInactive: true);
        if (constellationMenu != null && constellationMenu.gameObject.activeSelf)
        {
            constellationMenu.gameObject.SetActive(false);
        }

        var loadoutMenu = Object.FindObjectOfType<UI_Lobby_Loadout_AvailableSkills>(includeInactive: true);
#pragma warning restore CS0618
        if (loadoutMenu != null && loadoutMenu.gameObject.activeSelf)
        {
            loadoutMenu.gameObject.SetActive(false);
        }
    }

    private static void EnsureStorageLoadouts(DewProfile profile, string heroType)
    {
        if (profile?.heroLoadouts == null || string.IsNullOrEmpty(heroType))
        {
            return;
        }

        string storageHeroType = GetStorageHeroType(heroType);
        if (string.IsNullOrEmpty(storageHeroType))
        {
            return;
        }

        if (!profile.heroLoadouts.TryGetValue(storageHeroType, out var loadouts) || loadouts == null)
        {
            loadouts = new List<HeroLoadoutData>();
            profile.heroLoadouts[storageHeroType] = loadouts;
        }

        while (loadouts.Count > DefaultLoadoutCount)
        {
            loadouts.RemoveAt(loadouts.Count - 1);
        }

        while (loadouts.Count < DefaultLoadoutCount)
        {
            loadouts.Add(new HeroLoadoutData());
        }
    }

    private static bool IsKnownHeroType(string heroType)
    {
        return !string.IsNullOrEmpty(heroType) &&
               !heroType.StartsWith(IdentityHeroKeyPrefix) &&
               Dew.IsHeroIncludedInGame(heroType) &&
               DewResources.GetByShortTypeName<Hero>(heroType, ResourceLoadSettings.Light) != null;
    }

    private static HeroLoadoutData CloneLoadout(HeroLoadoutData source)
    {
        if (source == null)
        {
            return new HeroLoadoutData();
        }

        return new HeroLoadoutData
        {
            skillQ = source.skillQ,
            skillR = source.skillR,
            skillTrait = source.skillTrait,
            skillMovement = source.skillMovement,
            cDestruction = source.cDestruction != null ? new List<LoadoutStarItem>(source.cDestruction) : new List<LoadoutStarItem>(),
            cLife = source.cLife != null ? new List<LoadoutStarItem>(source.cLife) : new List<LoadoutStarItem>(),
            cImagination = source.cImagination != null ? new List<LoadoutStarItem>(source.cImagination) : new List<LoadoutStarItem>(),
            cFlexible = source.cFlexible != null ? new List<LoadoutStarItem>(source.cFlexible) : new List<LoadoutStarItem>()
        };
    }

    public sealed class HeroLoadoutSwapState
    {
        public readonly DewProfile Profile;
        public readonly string HeroType;
        public readonly List<HeroLoadoutData> OriginalLoadouts;

        public HeroLoadoutSwapState(
            DewProfile profile,
            string heroType,
            List<HeroLoadoutData> originalLoadouts)
        {
            Profile = profile;
            HeroType = heroType;
            OriginalLoadouts = originalLoadouts;
        }
    }
}
