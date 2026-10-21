using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine
{
    public class Object
    {
        public static T Instantiate<T>(T source, Transform parent) where T : Component, new()
        {
            var copy = new T();
            parent.Add(copy.transform);
            return copy;
        }
        public static void Destroy(GameObject target) => target.transform.parent?.Remove(target.transform);
        public static GameObject Instantiate(GameObject source, Transform parent, bool worldPositionStays)
        {
            var copy = new GameObject { layer = source.layer };
            copy.transform.localScale = source.transform.localScale;
            parent.Add(copy.transform);
            for (int i = 0; i < source.transform.childCount; i++)
                Instantiate(source.transform.GetChild(i).gameObject, copy.transform, false);
            return copy;
        }
    }
    public class GameObject : Object
    {
        public int layer;
        public Scene scene = new Scene();
        internal readonly List<object> components = new List<object>();
        public readonly Transform transform;
        public GameObject() { transform = new Transform(this); }
    }
    public class Transform
    {
        public readonly GameObject gameObject;
        public Vector3 localScale;
        public Transform parent;
        private readonly List<Transform> children = new List<Transform>();
        public int childCount => children.Count;
        public Transform(GameObject gameObject) { this.gameObject = gameObject; }
        public Transform GetChild(int index) => children[index];
        public void Add(Transform child) { children.Add(child); child.parent = this; }
        public void Remove(Transform child) { children.Remove(child); child.parent = null; }
        public T[] GetComponentsInChildren<T>(bool includeInactive)
            => gameObject.components.OfType<T>().Concat(children.SelectMany(child => child.GetComponentsInChildren<T>(includeInactive))).ToArray();
    }
    public class Component : Object
    {
        public readonly GameObject gameObject = new GameObject();
        public Transform transform => gameObject.transform;
        protected Component() { gameObject.components.Add(this); Resources.instances.Add(this); }
    }
    public class Scene { public bool IsValid() => true; }
    public static class Resources
    {
        internal static readonly List<Component> instances = new List<Component>();
        public static T[] FindObjectsOfTypeAll<T>() => instances.OfType<T>().ToArray();
    }
    public static class Debug
    {
        public static int errors;
        public static void Log(object message) { }
        public static void LogError(object message) { errors++; }
    }
    public struct Vector3
    {
        public float x;
        public static float Distance(Vector3 a, Vector3 b) => Math.Abs(a.x - b.x);
    }
    public static class Random
    {
        private static readonly System.Random Generator = new System.Random(1234);
        public static int Range(int min, int max) => Generator.Next(min, max);
    }
}

namespace Mirror
{
    public static class NetworkServer { public static bool active = true; }
    public sealed class NetworkConnectionToClient
    {
        public DewPlayer player;
        public DewPlayer GetPlayer() => player;
    }
}

public sealed class SaveActorAttribute : Attribute { }
public interface IRewardActor { }
public enum AffordType { Yes, No }
public enum StarType { Destruction, Life, Imagination, Flexible }
public struct LoadoutStarItem { public string name; public int level; }
public struct ChoiceShrineItem { public string typeName; public int level; }
public sealed class CastInfo { public CastInfo(Hero hero) { } }
public class Actor
{
    public bool isNewInstance = true;
    public readonly UnityEngine.GameObject gameObject = new UnityEngine.GameObject();
    public UnityEngine.Transform transform => gameObject.transform;
    public bool isActive = true;
    public UnityEngine.Vector3 position;
    public readonly Dictionary<string, string> persistentData = new Dictionary<string, string>();
}
public class Entity : Actor { public DewPlayer owner; }
public class Monster : Entity { public UnityEngine.Vector3 agentPosition => position; }
public class BossMonster : Monster
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    protected virtual void OnDeath() { }
    public void SimulateDeath(bool interrupted = false)
    {
        if (!interrupted) OnDeath();
    }
}
public class Hero : Entity
{
    public HeroLoadoutData loadout = new HeroLoadoutData();
    public HeroSkill Skill = new HeroSkill();
    public void CreateStatusEffect(StarEffect prefab, Hero victim, CastInfo info, Action<StarEffect> configure)
    {
        var effect = (StarEffect)Activator.CreateInstance(prefab.GetType());
        effect.victim = victim;
        configure(effect);
        NetworkedManagerBase<ActorManager>.instance.allActors.Add(effect);
    }
}
public sealed class OtherHero : Hero { }
public sealed class HeroSkill { public object Movement = new object(); }
public sealed class HeroLoadoutData
{
    private readonly Dictionary<StarType, List<LoadoutStarItem>> Stars = new Dictionary<StarType, List<LoadoutStarItem>>();
    public List<LoadoutStarItem> GetStarList(StarType type)
    {
        if (!Stars.ContainsKey(type)) Stars[type] = new List<LoadoutStarItem>();
        return Stars[type];
    }
}
public sealed class DewPlayer
{
    public string guid;
    public Hero hero;
    public int gold;
    public int dreamDust;
    public int goldEvents;
    public int dustEvents;
    public bool throwOnEarn;

    public void AddGold(int amount)
    {
        if (DewLucidDreamExpansion.controller.ResourceInversionController.TryInvertAdd(this, amount, true)) return;
        if (amount > 0 && Mirror.NetworkServer.active) gold = (int)Math.Min((long)gold + amount, int.MaxValue);
    }
    public void AddDreamDust(int amount)
    {
        if (DewLucidDreamExpansion.controller.ResourceInversionController.TryInvertAdd(this, amount, false)) return;
        if (amount > 0 && Mirror.NetworkServer.active) dreamDust = (int)Math.Min((long)dreamDust + amount, int.MaxValue);
    }
    public void EarnGold(int amount)
    {
        if (throwOnEarn) throw new InvalidOperationException("Earn fixture failure");
        if (DewLucidDreamExpansion.controller.ResourceInversionController.TryInvertEarn(this, amount, true)) return;
        if (amount <= 0 || !Mirror.NetworkServer.active) return;
        AddGold(amount);
        goldEvents++;
    }
    public void EarnDreamDust(int amount)
    {
        if (throwOnEarn) throw new InvalidOperationException("Earn fixture failure");
        if (DewLucidDreamExpansion.controller.ResourceInversionController.TryInvertEarn(this, amount, false)) return;
        if (amount <= 0 || !Mirror.NetworkServer.active) return;
        AddDreamDust(amount);
        dustEvents++;
    }
}
public class StarEffect : Actor
{
    public Hero victim;
    public int skillLevel;
    public float strength;
    public int maxStarLevel = 5;
    public virtual Type heroType => null;
    public virtual bool isMovementSkillType => false;
    public virtual bool IsIncompatibleWith(Hero hero, HeroLoadoutData loadout) => false;
}
public sealed class StarA : StarEffect { }
public sealed class StarB : StarEffect { }
public sealed class StarC : StarEffect { }
public sealed class StarD : StarEffect { }
public sealed class OtherHeroStar : StarEffect { public override Type heroType => typeof(OtherHero); }
public sealed class SelectedHeroStar : StarEffect { public override Type heroType => typeof(Hero); }
public sealed class IncompatibleStar : StarEffect
{
    public override bool IsIncompatibleWith(Hero hero, HeroLoadoutData loadout) => true;
}
public sealed class MovementStar : StarEffect { public override bool isMovementSkillType => true; }
public sealed class ActorManager { public readonly List<Actor> allActors = new List<Actor>(); }
public static class NetworkedManagerBase<T> where T : new() { public static T instance = new T(); }
public static class Dew
{
    public static readonly List<Type> allStarTypes = new List<Type>();
    public static int createdShrines;
    public static int spawnFailure;
    public static Actor lastCreated;
    public static UnityEngine.Vector3 GetGoodRewardPosition(UnityEngine.Vector3 position, float offset)
        => new UnityEngine.Vector3 { x = position.x + offset };
    public static T CreateActor<T>(UnityEngine.Vector3 position, object rotation) where T : Actor, new()
    {
        if (spawnFailure == 1) throw new InvalidOperationException("Spawn fixture failure");
        if (spawnFailure == 2) return null;
        createdShrines++;
        var actor = new T { position = position, isActive = spawnFailure != 3 };
        lastCreated = actor;
        return actor;
    }
}
public static class DewResources
{
    public static T GetByType<T>(Type type) => (T)Activator.CreateInstance(type);
}
public abstract class Shrine : Actor
{
    public UnityEngine.GameObject model, availableEffect, unavailableEffect, lockedEffect, unlockEffect,
        useLockedEffect, useEffect, useEffectLocal;
    public int mapVisibility;
    public float interactableDelay, useCooldown;
    public bool unavailableByDefault;
    protected abstract bool OnUse(Entity entity);
    public bool SimulateUse(Entity entity) => OnUse(entity);
}
public abstract class ChoiceShrine : Shrine
{
    public int openedSelections;
    protected override bool OnUse(Entity entity) { openedSelections++; return false; }
    public readonly Dictionary<string, ChoiceShrineItem[]> choices = new Dictionary<string, ChoiceShrineItem[]>();
    private readonly HashSet<string> Used = new HashSet<string>();
    public bool isLocked;
    public float focusDistance = 3;
    public AffordType afford = AffordType.Yes;
    public virtual bool CanInteract(Entity entity) => !Used.Contains(entity.owner.guid);
    public AffordType CanAfford(Entity entity) => afford;
    public void DoPostUseRoutines(Entity entity) => Used.Add(entity.owner.guid);
    public void PopulatePlayerChoices(DewPlayer player) => OnPopulateChoices(player);
    protected abstract void OnPopulateChoices(DewPlayer player);
}

public enum LimboDepthState { Locked, Available, Completed }
public static class GameMod_Limbo
{
    public static int completedDepth;
    public static int selectedDepth;
    public static LimboDepthState Lobby_GetLimboDepthState(int depth)
        => depth > completedDepth + 1 ? LimboDepthState.Locked : depth <= completedDepth ? LimboDepthState.Completed : LimboDepthState.Available;
    public static void Lobby_SetDepth(int depth) => selectedDepth = depth;
}
public sealed class TestDepthText { public string text; }
public class UI_LimboDepthIcon : UnityEngine.Component
{
    public int depth;
    public TestDepthText depthText = new TestDepthText();
    public LimboDepthState state;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Setup(int depth)
    {
        depth = Math.Min(Math.Max(depth, 0), 6);
        this.depth = depth;
        depthText.text = depth.ToString();
        UpdateState();
    }
    private void UpdateState() => state = GameMod_Limbo.Lobby_GetLimboDepthState(depth);
}
public class UI_Lobby_Limbo_DepthSelect_Item : UnityEngine.Component
{
    public int depth;
    public UI_LimboDepthIcon icon = new UI_LimboDepthIcon();
    public void Setup(int depth) { this.depth = depth; icon.Setup(depth); }
    public void Click()
    {
        if (Mirror.NetworkServer.active && GameMod_Limbo.Lobby_GetLimboDepthState(depth) != LimboDepthState.Locked)
            GameMod_Limbo.Lobby_SetDepth(depth);
    }
}
public class UI_Lobby_Limbo_DepthSelect : UnityEngine.Component
{
    public UI_Lobby_Limbo_DepthSelect_Item prefab = new UI_Lobby_Limbo_DepthSelect_Item();
    public UnityEngine.Transform parent = new UnityEngine.GameObject().transform;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void Start()
    {
        for (int depth = 1; depth <= 6; depth++) UnityEngine.Object.Instantiate(prefab, parent).Setup(depth);
    }
    public void SimulateStart() => Start();
}

namespace DewLucidDreamExpansion.content
{
    public sealed class LucidDream_AstronomyMaster { }
    public sealed class LucidDream_PrimordialCurse { }
}
namespace DewLucidDreamExpansion.patch
{
    internal static class DreamRules
    {
        internal static bool astronomyActive;
        internal static bool curseActive;
        internal static bool Active<T>() => typeof(T) == typeof(content.LucidDream_PrimordialCurse) ? curseActive : astronomyActive;
    }
}
