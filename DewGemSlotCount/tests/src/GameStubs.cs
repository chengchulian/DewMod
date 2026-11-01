using System;
using System.Collections.Generic;

// 仅替代 Unity 与游戏运行时；测试直接编译生产控制器。
namespace Mirror
{
    public interface NetworkMessage { }
    public class NetworkConnection { }
    public class NetworkIdentity { public uint netId; }
    public class NetworkConnectionToClient : NetworkConnection { public int connectionId; public NetworkIdentity identity; }
    public class NetworkWriter { public byte value; public void WriteByte(byte item) => value = item; }
    public class NetworkReader { public byte value; public byte ReadByte() => value; }
    public static class Writer<T> { public static Action<NetworkWriter, T> write; }
    public static class Reader<T> { public static Func<NetworkReader, T> read; }
    public static class NetworkServer
    {
        public static bool active;
        private static readonly Dictionary<Type, Delegate> Handlers = new();
        public static void RegisterHandler<T>(Action<NetworkConnectionToClient, T> handler) => Handlers[typeof(T)] = handler;
        public static void UnregisterHandler<T>() => Handlers.Remove(typeof(T));
        public static void Deliver<T>(NetworkConnectionToClient connection, T message) =>
            ((Action<NetworkConnectionToClient, T>)Handlers[typeof(T)])(connection, message);
    }
    public static class NetworkClient
    {
        public static bool active;
        public static bool isConnected;
        public static bool ready;
        public static NetworkConnection connection;
        public static int sent;
        public static void Send<T>(T message) => sent++;
    }
}

namespace UnityEngine
{
    public static class Time { public static float unscaledTime; }
    public static class Debug
    {
        public static void Log(string message) { }
        public static void LogException(Exception exception) { }
    }
    public static class Mathf
    {
        public static float Abs(float value) => Math.Abs(value);
        public static int Max(int a, int b) => Math.Max(a, b);
        public static int Clamp(int value, int min, int max) => Math.Min(max, Math.Max(min, value));
    }
}

public enum HeroSkillLocation { Q, W, E, R, Identity, Movement }

public partial class HeroSkill
{
    private static uint _nextId;
    public uint netId = ++_nextId;
    public bool isServer = true;
    public Hero hero;
    public Entity entity = new();
    public readonly Dictionary<GemLocation, Gem> gems = new();
    public readonly int[] Counts = { 3, 3, 3, 3, 0, 0 };
    public int GetMaxGemCount(HeroSkillLocation type)
    {
        int count = Counts[(int)type];
        DewGemSlotCount.patch.HeroSkill_Patch.GetMaxGemCount_Postfix(this, type, ref count);
        return count;
    }
    public void SetMaxGemCount(HeroSkillLocation type, int count)
    {
        DewGemSlotCount.patch.HeroSkill_Patch.SetMaxGemCount_Prefix(this, type, ref count);
        Counts[(int)type] = count;
    }
    public void OnLateStartServer() { }
    public bool CanReplaceSkill(HeroSkillLocation type) => (int)type < 4;
    public bool TryGetEquippedGemOfSameType(Type type, out GemLocation loc, out Gem gem)
    { loc = default; gem = null; return false; }
}

public struct GemLocation { public HeroSkillLocation skill; public int index; }
public class Gem { }
public class Entity { public Ability Ability = new(); public DewPlayer owner; public bool isOwned; }
public class Ability { public bool IsAbilityEditLocked(int type) => false; }

public class Hero : Entity
{
    private HeroSkill _skill;
    public HeroSkill Skill { get => _skill; set { _skill = value; value.hero = this; } }
    public Hero() { Skill = new HeroSkill(); }
}

public class DewPlayer
{
    public uint netId;
    public string guid = "regression-player";
    public static DewPlayer local;
    public static readonly List<DewPlayer> allHumanPlayers = new();
    public bool isHumanPlayer = true;
    public bool isLocalPlayer;
    public Mirror.NetworkConnectionToClient connectionToClient;
    public Hero hero;
}

public class GameSettingsManager { public readonly Dictionary<string, string> customData = new(); }
public static class NetworkedManagerBase<T> { public static T softInstance; }

public class Se_Shrine_Chaos_StatBonus
{
    public object victim;
    public int currentAddedGemSlotQ;
    public int currentAddedGemSlotW;
    public int currentAddedGemSlotE;
    public int currentAddedGemSlotR;
    public int currentAddedGemSlotIdentity;
}

