using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DewShopShare.controller;
using DewShopShare.patch;
using HarmonyLib;

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
            PurchasesReduceEveryonesStock,
            RefreshKeepsRemainingStock,
            SenderRefreshKeepsSoldOutOriginal,
            LateJoinGetsSharedOffersOnce,
            InvalidAndSoldOutPingsDoNothing,
            IdenticalNativeOffersKeepTheirOwnStock,
            ServerAuthorityAndMerchantCleanup,
            FailedPurchaseDoesNotConsumeStock,
            LastStockAndStaleRequests,
            PurchaseEventsCannotReenterSameOffer,
            PurchaseEventsCanRefreshInventories,
            PurchaseExceptionReleasesLock
        };

        var harmony = new Harmony("DewShopShare.RegressionTests");
        try
        {
            harmony.CreateClassProcessor(typeof(MerchantPurchasePatch)).Patch();
            harmony.CreateClassProcessor(typeof(MerchantPopulatePatch)).Patch();
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
        finally
        {
            harmony.UnpatchAll(harmony.Id);
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
    private static bool Buy(SharedShopState state, Dictionary<string, MerchandiseData[]> inventories,
        string player, int index)
    {
        if (!state.BeginPurchase(inventories, player, index, out SharedShopState.PurchaseAttempt attempt))
        {
            return false;
        }

        try
        {
            MerchandiseData[] copy = inventories[player].ToArray();
            if (copy[index].count <= 0) return false;
            copy[index].count--;
            inventories[player] = copy;
            return true;
        }
        finally
        {
            state.FinishPurchase(inventories, attempt);
        }
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
        Buy(state, inventories, "A", 0);
        Require(!state.Share(inventories, "A", 0), "原商品购买后再次标记仍去重");
        Require(!state.Share(inventories, "B", 1), "标记共享副本应去重");
        Require(inventories["C"].Length == 2, "重复标记不追加");
    }

    private static void PurchasesReduceEveryonesStock()
    {
        var inventories = Inventories();
        var state = new SharedShopState();
        state.Share(inventories, "A", 0);
        Buy(state, inventories, "B", 1);
        Require(inventories["B"][1].count == 2 && inventories["A"][0].count == 2 &&
                inventories["C"][1].count == 2, "购买共享副本同步减少原商品和全队副本库存");
        Buy(state, inventories, "A", 0);
        Require(inventories["B"][1].count == 1 && inventories["C"][1].count == 1, "购买原商品也减少全队库存");
        Require(inventories["B"][0].count == 3, "不影响个人未共享商品");
    }

    private static void RefreshKeepsRemainingStock()
    {
        var inventories = Inventories();
        var state = new SharedShopState();
        state.Share(inventories, "A", 0);
        Buy(state, inventories, "B", 1);
        Refresh(state, inventories, "B");
        Require(inventories["B"].Length == 2 && inventories["B"][1].count == 2, "刷新保留已消耗数量");
        Buy(state, inventories, "B", 1);
        Buy(state, inventories, "B", 1);
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
        for (int i = 0; i < 3; i++) Buy(state, inventories, "A", 0);
        Refresh(state, inventories, "A");
        Require(inventories["A"].Length == 2 && inventories["A"][1].count == 0,
            "原商品刷新后转为共享条目且保留售罄状态");
        Require(inventories["B"][1].count == 0, "标记者买完后队友也售罄");
    }

    private static void LateJoinGetsSharedOffersOnce()
    {
        var inventories = Inventories();
        var state = new SharedShopState();
        state.Share(inventories, "A", 0);
        Buy(state, inventories, "A", 0);
        MerchandiseData[] previous = state.CaptureInventory(inventories, "D");
        inventories["D"] = new[] { Item("Gem_D") };
        state.OnInventoryPopulated(inventories, "D", previous);
        Require(inventories["D"].Length == 2 && inventories["D"][1].count == 2, "晚加入获取当前共享库存");
        state.OnInventoryPopulated(inventories, "D", inventories["D"]);
        Buy(state, inventories, "D", 1);
        Refresh(state, inventories, "D");
        Require(inventories["D"].Length == 2 && inventories["D"][1].count == 1,
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
        Buy(state, inventories, "B", 1);
        Require(inventories["C"][1].count == 2 && inventories["C"][2].count == 3,
            "同款不同来源的库存分别计数");
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

    // 后续场景通过生产 Harmony 补丁执行完整购买入口，而不是手工调用同步逻辑。
    private static PropEnt_Merchant_Base SharedMerchant(int count, out DewPlayer first, out DewPlayer second)
    {
        ShopShareController.Clear();
        DewPlayer.gamePlayers.Clear();
        Mirror.NetworkServer.active = true;
        first = new DewPlayer { guid = "A" };
        second = new DewPlayer { guid = "B" };
        DewPlayer.gamePlayers.Add(first);
        DewPlayer.gamePlayers.Add(second);
        var merchant = new PropEnt_Merchant_Base();
        merchant.merchandises["A"] = new[] { Item(count: count) };
        merchant.merchandises["B"] = new[] { Item("Gem_B") };
        ShopShareController.OnValidatedPing(new PingManager.Ping
        {
            sender = first, target = merchant, type = PingManager.PingType.ShopItem, itemIndex = 0
        });
        return merchant;
    }

    private static void FailedPurchaseDoesNotConsumeStock()
    {
        var merchant = SharedMerchant(1, out var first, out var second);
        var connection = new Mirror.NetworkConnectionToClient { player = second };
        second.gold = 0;
        merchant.Purchase(1, connection);
        Require(merchant.merchandises["A"][0].count == 1 && second.purchased == 0, "余额不足不扣共享库存");
        second.gold = 10000;
        second.canPurchase = false;
        merchant.Purchase(1, connection);
        Require(merchant.merchandises["A"][0].count == 1 && second.purchased == 0, "购买条件失败不扣库存");
        second.canPurchase = true;
        merchant.Purchase(1, connection);
        Require(merchant.merchandises["A"][0].count == 0 && second.purchased == 1,
            "失败请求释放锁，随后有效购买正常完成");
    }

    private static void LastStockAndStaleRequests()
    {
        var merchant = SharedMerchant(1, out var first, out var second);
        merchant.Purchase(1, new Mirror.NetworkConnectionToClient { player = second });
        int originalGold = first.gold;
        // 模拟显示尚未同步的第二人继续发送原下标购买请求。
        merchant.Purchase(0, new Mirror.NetworkConnectionToClient { player = first });
        merchant.Purchase(1, new Mirror.NetworkConnectionToClient { player = second });
        Require(first.purchased == 0 && second.purchased == 1 && first.gold == originalGold,
            "最后一份全队只发货一次，过期请求不扣款");
        Require(merchant.merchandises["A"][0].count == 0 && merchant.merchandises["B"][1].count == 0,
            "售罄后全队显示零库存");
    }

    private static void PurchaseEventsCannotReenterSameOffer()
    {
        var merchant = SharedMerchant(1, out var first, out var second);
        merchant.onPurchase = () => merchant.Purchase(0, new Mirror.NetworkConnectionToClient { player = first });
        merchant.Purchase(1, new Mirror.NetworkConnectionToClient { player = second });
        Require(first.purchased == 0 && second.purchased == 1 && merchant.merchandises["A"][0].count == 0,
            "原生购买事件重入同一商品不会超卖");
    }

    private static void PurchaseExceptionReleasesLock()
    {
        var merchant = SharedMerchant(2, out var first, out var second);
        merchant.onPurchase = () => throw new InvalidOperationException("模拟生成物品后异常");
        bool caught = false;
        try { merchant.Purchase(1, new Mirror.NetworkConnectionToClient { player = second }); }
        catch (InvalidOperationException) { caught = true; }
        Require(caught && merchant.merchandises["A"][0].count == 1, "原有异常保留，已扣库存同步全队");
        merchant.onPurchase = null;
        merchant.Purchase(0, new Mirror.NetworkConnectionToClient { player = first });
        Require(first.purchased == 1 && merchant.merchandises["B"][1].count == 0, "异常后释放锁并正常购买剩余份数");
    }

    private static void PurchaseEventsCanRefreshInventories()
    {
        var merchant = SharedMerchant(2, out var first, out var second);
        merchant.onPurchase = () =>
        {
            merchant.PopulatePlayerMerchandises(second);
            merchant.PopulatePlayerMerchandises(first);
        };
        merchant.Purchase(1, new Mirror.NetworkConnectionToClient { player = second });
        Require(merchant.merchandises["A"][1].count == 1 && merchant.merchandises["B"][1].count == 1,
            "购买事件中刷新买家和标记者库存，也仅结算一次购买");
        merchant.onPurchase = null;
        merchant.Purchase(1, new Mirror.NetworkConnectionToClient { player = first });
        Require(first.purchased == 1 && merchant.merchandises["B"][1].count == 0, "刷新后剩余的一份正常售罄");
    }
}
