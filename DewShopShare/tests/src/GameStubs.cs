using System.Collections.Generic;

// 仅模拟库存结构与服务端入口，不模拟 Unity、Mirror 传输或购买扣款。
public enum MerchandiseType { Empty, Skill, Gem, Souvenir, Treasure }

public struct Cost
{
    public int gold, dreamDust, stardust, healthPercentage, platinumCoin;
}

public struct MerchandiseData
{
    public MerchandiseType type;
    public string itemName;
    public int level;
    public Cost price;
    public int count;
    public string customData;
}

public class DewPlayer
{
    public static readonly List<DewPlayer> gamePlayers = new List<DewPlayer>();
    public string guid;
}

public class PropEnt_Merchant_Base
{
    public bool isServer = true;
    public bool isActive = true;
    public readonly Dictionary<string, MerchandiseData[]> merchandises =
        new Dictionary<string, MerchandiseData[]>();
}

public class PingManager
{
    public enum PingType { Move, Entity, Interactable, EquippedItem, ShopItem, ChoiceItem, WorldNode }
    public struct Ping
    {
        public DewPlayer sender;
        public PingType type;
        public object target;
        public int itemIndex;
    }
}

namespace Mirror
{
    public static class NetworkServer
    {
        public static bool active;
    }
}

namespace UnityEngine
{
    public static class Debug
    {
        public static void LogError(object message)
        {
            throw new System.InvalidOperationException(message.ToString());
        }
    }
}
