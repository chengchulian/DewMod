using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

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
    public int gold = 10000;
    public int purchased;
    public bool canPurchase = true;
}

public class PropEnt_Merchant_Base
{
    public bool isServer = true;
    public bool isActive = true;
    public readonly Dictionary<string, MerchandiseData[]> merchandises =
        new Dictionary<string, MerchandiseData[]>();
    public Action onPurchase;

    // 与原版购买相同的校验、克隆扣库存和购买事件顺序，用真实 Harmony 补丁验证整个入口。
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void UserCode_CmdPurchase__Int32__NetworkConnectionToClient(int index,
        Mirror.NetworkConnectionToClient sender)
    {
        DewPlayer player = sender.GetPlayer();
        if (!merchandises.TryGetValue(player.guid, out MerchandiseData[] items) ||
            index < 0 || index >= items.Length || items[index].count <= 0 ||
            player.gold < items[index].price.gold || !player.canPurchase)
        {
            return;
        }

        MerchandiseData[] copy = items.ToArray();
        copy[index].count--;
        merchandises[player.guid] = copy;
        player.gold -= items[index].price.gold;
        player.purchased++;
        onPurchase?.Invoke();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Purchase(int index, Mirror.NetworkConnectionToClient sender)
    {
        UserCode_CmdPurchase__Int32__NetworkConnectionToClient(index, sender);
    }

    // 用不同的原生商品重建数组，验证购买事件触发刷新时不会丢失共享库存的扣除。
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void PopulatePlayerMerchandises(DewPlayer player)
    {
        merchandises[player.guid] = new[]
        {
            new MerchandiseData { type = MerchandiseType.Gem, itemName = "Gem_Refresh", count = 3 }
        };
    }
}

public static class ConnectionExtensions
{
    public static DewPlayer GetPlayer(this Mirror.NetworkConnectionToClient connection) => connection.player;
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
    public class NetworkConnectionToClient
    {
        public DewPlayer player;
    }

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
