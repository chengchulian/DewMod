using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace DewShopShare.controller;

internal static class ShopShareController
{
    private static readonly Dictionary<PropEnt_Merchant_Base, SharedShopState> Shops =
        new Dictionary<PropEnt_Merchant_Base, SharedShopState>();

    // BroadcastPing 由原生指令校验成功后调用，此时 sender 已被替换为真实连接对应的玩家。
    public static void OnValidatedPing(PingManager.Ping ping)
    {
        if (!NetworkServer.active || ping.type != PingManager.PingType.ShopItem ||
            !(ping.target is PropEnt_Merchant_Base merchant) || !merchant.isServer || !merchant.isActive ||
            ping.sender == null || !DewPlayer.gamePlayers.Contains(ping.sender) ||
            string.IsNullOrEmpty(ping.sender.guid))
        {
            return;
        }

        try
        {
            if (!Shops.TryGetValue(merchant, out SharedShopState shop))
            {
                shop = new SharedShopState();
                Shops.Add(merchant, shop);
            }

            shop.Share(merchant.merchandises, ping.sender.guid, ping.itemIndex);
        }
        catch (Exception exception)
        {
            // 共享失败也继续执行原生标记，避免破坏聊天和其他标记功能。
            Debug.LogError($"[DewShopShare] 共享商品失败：{exception}");
        }
    }

    // 仅在已有共享记录时截取刷新前库存，普通商店初始化无需额外分配状态。
    public static MerchandiseData[] BeforePopulate(PropEnt_Merchant_Base merchant, DewPlayer player)
    {
        if (!NetworkServer.active || !merchant.isServer || player == null ||
            !Shops.TryGetValue(merchant, out SharedShopState shop))
        {
            return null;
        }

        return shop.CaptureInventory(merchant.merchandises, player.guid);
    }

    // 原生初始化也用于晚加入玩家，因此同一入口同时补发共享商品。
    public static void AfterPopulate(PropEnt_Merchant_Base merchant, DewPlayer player, MerchandiseData[] previous)
    {
        if (!NetworkServer.active || !merchant.isServer || player == null ||
            !Shops.TryGetValue(merchant, out SharedShopState shop))
        {
            return;
        }

        shop.OnInventoryPopulated(merchant.merchandises, player.guid, previous);
    }

    // 商人销毁后释放记录，不把上一间商店的商品带到下一间。
    public static void Remove(PropEnt_Merchant_Base merchant)
    {
        Shops.Remove(merchant);
    }

    // 联机管理器退出或 Mod 卸载时统一清理，避免持有上局的 Unity 对象。
    public static void Clear()
    {
        Shops.Clear();
    }
}
