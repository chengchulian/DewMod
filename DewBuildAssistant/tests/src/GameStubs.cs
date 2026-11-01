using System;
using System.Collections.Generic;
using System.Linq;

// 这些替身只提供调用边界，不启动 Unity；被测预检和提交逻辑直接链接生产文件。
public enum StarType { Destruction, Life, Imagination, Flexible }
public enum HeroSkillLocation { Q, R, Identity, Movement }
public enum GameState { InLobby, Starting, InGame }
public enum PlayerState { None, InLobby, Playing, Spectating }
public enum UnlockStatus { Locked, Complete }
public enum Rarity { Common = 0, Rare = 1, Epic = 2, Legendary = 3, Character = 4, Identity = 5, Unique = 10 }
public enum ResourceLoadSettings { Light }
public interface ILangaugeChangedCallback { void OnLanguageChanged(); }
namespace UnityEngine
{
    public class Sprite { public string name; }
    public class Component
    {
        public GameObject gameObject;
        public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();
    }
    public class MonoBehaviour : Component { }
    public class GameObject
    {
        private readonly Dictionary<Type, Component> _components = new();
        public T GetComponent<T>() where T : class => _components.TryGetValue(typeof(T), out var component) ? component as T : null;
        public T[] GetComponents<T>() where T : class => _components.Values.OfType<T>().ToArray();
        public T AddComponent<T>() where T : Component, new()
        {
            var component = new T { gameObject = this };
            _components[typeof(T)] = component;
            return component;
        }
    }
}
namespace TMPro
{
    public class TMP_Text : UnityEngine.Component { public string text; }
}
public class Actor { }
public class SkillTrigger : Actor { public bool excludeFromPool; public Rarity rarity; }
public class Gem : Actor { public bool excludeFromPool; public Rarity rarity; }
public class Hero : Actor
{
    public HeroSkill Skills = new();
    public T GetComponent<T>() where T : class => Skills as T;
    public HeroConstellationSettings GetConstellationSettings(StarType type) => new();
}
public class HeroConstellationSettings { public int maxCount = 3; public int defaultCount = 2; }
public class HeroSkill
{
    public Dictionary<HeroSkillLocation, SkillTrigger[]> Options = new();
    public SkillTrigger[] GetLoadoutSkills(HeroSkillLocation type) => Options.TryGetValue(type, out var options) ? options : Array.Empty<SkillTrigger>();
}
public class StarEffect : Actor
{
    public Type heroType;
    public int maxStarLevel = 5;
    public bool Incompatible;
    public bool IsIncompatibleWith(Hero hero, HeroLoadoutData loadout) => !DewIdentityChange.config.LoadoutPageMapper.Enabled && Incompatible;
}
public struct LoadoutStarItem { public string name; public int level; }
public class HeroLoadoutData
{
    private readonly Dictionary<HeroSkillLocation, int> _skills = new();
    private readonly Dictionary<StarType, List<LoadoutStarItem>> _stars = new();
    public HeroLoadoutData()
    {
        foreach (StarType type in Enum.GetValues(typeof(StarType))) _stars[type] = new List<LoadoutStarItem> { default, default, default };
        foreach (HeroSkillLocation location in Enum.GetValues(typeof(HeroSkillLocation))) _skills[location] = 0;
    }
    public HeroLoadoutData(HeroLoadoutData source)
    {
        foreach (var pair in source._skills) _skills[pair.Key] = pair.Value;
        foreach (var pair in source._stars) _stars[pair.Key] = new List<LoadoutStarItem>(pair.Value);
    }
    public void SetSkill(HeroSkillLocation location, int index) => _skills[location] = index;
    public int GetSkill(HeroSkillLocation location) => _skills[location];
    public List<LoadoutStarItem> GetStarList(StarType type) => _stars[type];
    public void PopulateLevelsByLocalSaveData()
    {
        foreach (var stars in _stars.Values)
        for (int i = 0; i < stars.Count; i++)
        {
            var item = stars[i];
            if (item.name != null && DewSave.profileMain.newStars.TryGetValue(item.name, out var data))
            {
                item.level = data.level;
                stars[i] = item;
            }
        }
    }
    public bool IsValidFor(string hero) => true;
}
public class NetworkedManagerBase<T> { public static T softInstance; }
public class SingletonBehaviour<T> { public static T softInstance; }
public class UI_Lobby_Constellations
{
    public bool isActiveAndEnabled;
    public HeroLoadoutData loadout;
    public int selectedLoadoutIndex;
    public void LoadLoadout(int slot)
    {
        selectedLoadoutIndex = slot;
        loadout = new HeroLoadoutData(DewSave.profileMain.heroLoadouts[DewPlayer.local.selectedHeroType][slot]);
    }
}
public class PreferredSettings
{
    public Dictionary<string, int> heroSelectedLoadoutIndex = new();
    public string dejavuItem;
}
public class GameSettingsManager
{
    public GameState state;
    public bool allowDejavu = true;
    public HashSet<string> bannedGameItems = new();
    public int localPlayerDejavuCost;
    public PreferredSettings Preferred = new();
    public PreferredSettings GetLocalPreferredGameSettings() => Preferred;
}
public class DewPlayer
{
    public static DewPlayer local;
    public string selectedHeroType;
    public PlayerState state;
    public bool isReady;
    public int Commands;
    public HeroLoadoutData SentLoadout;
    public string SentDejavu;
    public void CmdSetHeroLoadoutData(HeroLoadoutData loadout) { SentLoadout = loadout; Commands++; }
    public void CmdSetDejavuItem(string item) { SentDejavu = item; Commands++; }
}
public static class DewResources
{
    public static IEnumerable<T> FindAllByType<T>(ResourceLoadSettings settings = ResourceLoadSettings.Light) where T : Actor => Assets.Values.OfType<T>();
    public static readonly Dictionary<string, Actor> Assets = new();
    public static T GetByShortTypeName<T>(string name, ResourceLoadSettings settings = ResourceLoadSettings.Light) where T : Actor =>
        name != null && Assets.TryGetValue(name, out var asset) ? asset as T : null;
}
public static class Dew
{
    public static readonly List<Type> allHeroes = new();
    public static bool IsHeroIncludedInGame(string name) => true;
    public static bool IsStarIncludedInGame(string name) => true;
    public static bool IsSkillIncludedInGame(string name) => true;
    public static bool IsGemIncludedInGame(string name) => true;
    public static bool IsDejavuFree(string name) => false;
    public static int GetDejavuCost(Actor actor) => 75;
}
public class DewProfile
{
    public class UnlockData { public bool isAvailableInGame = true; public UnlockStatus status = UnlockStatus.Complete; }
    public class StarData { public int level = 3; }
    public class HeroStarSlotUnlockData
    {
        public List<int> Slots = new();
        public List<int> Get(StarType type) => Slots;
    }
    public Dictionary<string, List<HeroLoadoutData>> heroLoadouts = new();
    public Dictionary<string, HeroStarSlotUnlockData> heroUnlockedStarSlots = new();
    public Dictionary<string, UnlockData> skills = new();
    public Dictionary<string, UnlockData> gems = new();
    public Dictionary<string, StarData> newStars = new();
    public int stardust = 1000;
}
public class DewProfileStats
{
    public class Totals { public int masteryLevel = 100; }
    public class ItemData { public int wins = 1; }
    public Totals total = new();
    public bool HasWins = true;
    public bool TryGetItemData(Type type, out ItemData data) { data = new ItemData(); return HasWins; }
}
public static class DewSave
{
    public static DewProfile profileMain;
    public static DewProfileStats profileStats;
    public static int Saves;
    public static void SaveProfileMain() { Saves++; }
}
namespace DewInternal
{
    public class SkillConfigData { public string name; }
    public class SkillData { public List<SkillConfigData> configs = new(); }
    public class GemData { public string name; }
    public class StarData { public string name; }
    public class PerLanguageLocalizationData
    {
        public Dictionary<string, string> ui = new();
        public Dictionary<string, SkillData> skills = new();
        public Dictionary<string, GemData> gems = new();
        public Dictionary<string, StarData> stars = new();
    }
    public class DewLocalizationBuildData { public Dictionary<string, PerLanguageLocalizationData> dataByLanguage = new(); }
}
public static class DewLocalization
{
    public static DewInternal.DewLocalizationBuildData buildData = new();
    public static DewInternal.PerLanguageLocalizationData data = new();
    public static string GetSkillKey(string type) => type.Substring(3);
    public static bool TryGetUIValue(string key, out string value) => data.ui.TryGetValue(key, out value);
}
namespace DewBuildAssistant.config
{
    internal static class LocalizationSource
    {
        public static string GetLocalizationText(string key, params object[] args) => key + ":" + string.Join(",", args);
    }
}

public class Hero_Test : Hero { }
public class St_Q_First : SkillTrigger { }
public class St_Q_Second : SkillTrigger { }
public class St_R_First : SkillTrigger { }
public class St_I_First : SkillTrigger { }
public class Gem_Test : Gem { }
public class Se_Star_Test : StarEffect { }
public class Hero_Other : Hero { }
public class St_Q_Foreign : SkillTrigger { }
public class St_M_Test : SkillTrigger { }
public class St_ExcludedTest : SkillTrigger { }

namespace DewIdentityChange.config
{
    // 与可选 Mod 的公开接口同名，验证生产反射桥和模式切换。
    public static class LoadoutPageMapper
    {
        public static bool Enabled;
        public static bool IsIdentityEnabled => Enabled;
        public static string GetStorageHeroType(string hero) => Enabled ? "DewIdentityChange_" + hero : hero;
    }
}
