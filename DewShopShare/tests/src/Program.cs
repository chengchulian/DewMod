using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DewShopShare.controller;

internal static class Program
{
    // 场景测试直接链接生产控制器，覆盖重复传播、库存消耗与商店生命周期。
    private static int Main()
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        Action[] tests =
        {
            SharePreservesNativeOffersAndData,
            RepeatedAndForwardedPingsDoNotDuplicate,
            PurchasesKeepIndependentStock,
            RefreshKeepsRemainingStock,
            SenderRefreshKeepsSoldOutOriginal,
            LateJoinGetsSharedOffersOnce,
            InvalidAndSoldOutPingsDoNothing,
            IdenticalNativeOffersKeepTheirOwnStock,
            ServerAuthorityAndMerchantCleanup
        };

        try
        {
            foreach (Action test in tests)
            {
                test();
                Console.WriteLine("PASS " + test.Method.Name);
            }

            Console.WriteLine($"通过 {tests.Length} 个回归场景。");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static MerchandiseData Item(string name = "Gem_Test", int count = 3)
    {
        return new MerchandiseData
        {
            type = MerchandiseType.Gem, itemName = name, count = count, level = 140,
            price = new Cost { gold = 123, dreamDust = 4, stardust = 5, healthPercentage = 6, platinumCoin = 7 },
            customData = "商品附加数据"
        };
    }

    private static Dictionary<string, MerchandiseData[]> Inventories()
    {
        return new Dictionary<string, MerchandiseData[]>
        {
            ["A"] = new[] { Item() },
            ["B"] = new[] { Item("Gem_B") },
            ["C"] = new[] { Item("Gem_C") }
        };
    }

    // 原生购买通过克隆数组再写回字典扣库存，此处重现该边界。
    private static void Buy(Dictionary<string, MerchandiseData[]> inventories, string player, int index)
    {
        MerchandiseData[] copy = inventories[player].ToArray();
        copy[index].count--;
        inventories[player] = copy;
    }

    private static void Refresh(SharedShopState state, Dictionary<string, MerchandiseData[]> inventories,
        string player)
    {
        MerchandiseData[] previous = state.CaptureInventory(inventories, player);
        inventories[player] = new[] { Item("Gem_Refreshed") };
        state.OnInventoryPopulated(inventories, player, previous);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void SharePreservesNativeOffersAndData()
    {
        var inventories = Inventories();
        var original = inventories["A"];
        var state = new SharedShopState();
        Require(state.Share(inventories, "A", 0), "首次标记应共享");
        Require(ReferenceEquals(original, inventories["A"]), "原商品下标和数组保持稳定");
        Require(inventories["B"].Length == 2 && inventories["C"].Length == 2, "每人增加一项");
        Require(inventories["B"][0].itemName == "Gem_B", "原有商品保留");
        Require(inventories["B"][1].Equals(original[0]), "品质、所有价格字段和附加数据完整保留");
    }

    private static void RepeatedAndForwardedPingsDoNotDuplicate()
    {
        var inventories = Inventories();
        var state = new SharedShopState();
        state.Share(inventories, "A", 0);
        Buy(inventories, "A", 0);
        Require(!state.Share(inventories, "A", 0), "原商品购买后再次标记仍去重");
        Require(!state.Share(inventories, "B", 1), "标记共享副本应去重");
        Require(inventories["C"].Length == 2, "重复标记不追加");
    }

    private static void PurchasesKeepIndependentStock()
    {
        var inventories = Inventories();
        new SharedShopState().Share(inventories, "A", 0);
        Buy(inventories, "B", 1);
        Require(inventories["B"][1].count == 2 && inventories["A"][0].count == 3 &&
                inventories["C"][1].count == 3, "个人购买只减少自己的库存");
    }

    private static void RefreshKeepsRemainingStock()
    {
        var inventories = Inventories();
        var state = new SharedShopState();
        state.Share(inventories, "A", 0);
        Buy(inventories, "B", 1);
        Refresh(state, inventories, "B");
        Require(inventories["B"].Length == 2 && inventories["B"][1].count == 2, "刷新保留已消耗数量");
        Buy(inventories, "B", 1);
        Buy(inventories, "B", 1);
        Refresh(state, inventories, "B");
        Refresh(state, inventories, "B");
        Require(inventories["B"][1].count == 0, "重复刷新不恢复售罄库存");
        Require(!state.Share(inventories, "B", 1), "售罄副本不重新共享");
    }

    private static void SenderRefreshKeepsSoldOutOriginal()
    {
        var inventories = Inventories();
        var state = new SharedShopState();
        state.Share(inventories, "A", 0);
        for (int i = 0; i < 3; i++) Buy(inventories, "A", 0);
        Refresh(state, inventories, "A");
        Require(inventories["A"].Length == 2 && inventories["A"][1].count == 0,
            "原商品刷新后转为共享条目且保留售罄状态");
        Require(inventories["B"][1].count == 3, "标记者消耗库存不影响队友");
    }

    private static void LateJoinGetsSharedOffersOnce()
    {
        var inventories = Inventories();
        var state = new SharedShopState();
        state.Share(inventories, "A", 0);
        MerchandiseData[] previous = state.CaptureInventory(inventories, "D");
        inventories["D"] = new[] { Item("Gem_D") };
        state.OnInventoryPopulated(inventories, "D", previous);
        Require(inventories["D"].Length == 2 && inventories["D"][1].count == 3, "晚加入获取共享库存");
        state.OnInventoryPopulated(inventories, "D", inventories["D"]);
        Buy(inventories, "D", 1);
        Refresh(state, inventories, "D");
        Require(inventories["D"].Length == 2 && inventories["D"][1].count == 2,
            "重复回调和同 GUID 重建库存不补货");
    }

    private static void InvalidAndSoldOutPingsDoNothing()
    {
        var inventories = Inventories();
        var state = new SharedShopState();
        Require(!state.Share(inventories, "A", -1) && !state.Share(inventories, "A", 99) &&
                !state.Share(inventories, "missing", 0) && !state.Share(inventories, null, 0), "拒绝无效来源");
        inventories["A"] = new[] { Item(count: 0), new MerchandiseData() };
        Require(!state.Share(inventories, "A", 0) && !state.Share(inventories, "A", 1), "空槽和售罄不共享");
        Require(inventories["B"].Length == 1, "无效标记不改变其他玩家库存");
    }

    private static void IdenticalNativeOffersKeepTheirOwnStock()
    {
        var inventories = Inventories();
        inventories["B"] = new[] { Item() };
        var state = new SharedShopState();
        state.Share(inventories, "A", 0);
        Require(state.Share(inventories, "B", 0), "独立刷出的同款商品属于独立来源");
        Require(!state.Share(inventories, "B", 1), "同款共享副本仍然去重");
        Require(inventories["C"].Length == 3 && inventories["A"].Length == 2, "两份真实来源保留各自库存");
    }

    private static void ServerAuthorityAndMerchantCleanup()
    {
        ShopShareController.Clear();
        DewPlayer.gamePlayers.Clear();
        var player = new DewPlayer { guid = "A" };
        DewPlayer.gamePlayers.Add(player);
        var merchant = new PropEnt_Merchant_Base();
        foreach (var inventory in Inventories()) merchant.merchandises.Add(inventory.Key, inventory.Value);
        var ping = new PingManager.Ping
        {
            type = PingManager.PingType.ShopItem, target = merchant, sender = player, itemIndex = 0
        };
        Mirror.NetworkServer.active = false;
        ShopShareController.OnValidatedPing(ping);
        Require(merchant.merchandises["B"].Length == 1, "客户端不修改库存");
        Mirror.NetworkServer.active = true;
        ping.sender = new DewPlayer { guid = "A" };
        ShopShareController.OnValidatedPing(ping);
        Require(merchant.merchandises["B"].Length == 1, "仅接受真实游戏玩家");
        ping.sender = player;
        merchant.isActive = false;
        ShopShareController.OnValidatedPing(ping);
        Require(merchant.merchandises["B"].Length == 1, "停用商人不共享");
        merchant.isActive = true;
        ShopShareController.OnValidatedPing(ping);
        Require(merchant.merchandises["B"].Length == 2, "服务端有效标记同步库存");
        ShopShareController.Remove(merchant);
        var previous = merchant.merchandises["A"];
        merchant.merchandises["A"] = new[] { Item("Gem_NextShop") };
        ShopShareController.AfterPopulate(merchant, player, previous);
        Require(merchant.merchandises["A"].Length == 1, "商人销毁后不继承共享池");
        ShopShareController.Clear();
        DewPlayer.gamePlayers.Clear();
    }
}
