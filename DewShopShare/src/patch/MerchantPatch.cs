using System;
using DewShopShare.controller;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace DewShopShare.patch;

// 源码依据：Dewr.1.4.0.13_s/Dew.Core/PropEnt_Merchant_Base.cs。
[HarmonyPatch(typeof(PropEnt_Merchant_Base), nameof(PropEnt_Merchant_Base.PopulatePlayerMerchandises))]
internal static class MerchantPopulatePatch
{
    // 原生刷新覆盖数组前，保存原数组引用以识别成功刷新。
    private static void Prefix(PropEnt_Merchant_Base __instance, DewPlayer player, out MerchandiseData[] __state)
    {
        __state = ShopShareController.BeforePopulate(__instance, player);
    }

    // 为刷新的库存或新玩家补齐共享条目，不修改原生生成规则。
    private static void Postfix(PropEnt_Merchant_Base __instance, DewPlayer player, MerchandiseData[] __state)
    {
        ShopShareController.AfterPopulate(__instance, player, __state);
    }
}

// Mirror 的 CmdPurchase 是发送包装器；必须拦截服务端实际执行购买的生成方法。
[HarmonyPatch(typeof(PropEnt_Merchant_Base), "UserCode_CmdPurchase__Int32__NetworkConnectionToClient")]
internal static class MerchantPurchasePatch
{
    // 真实买家由连接解析，不使用客机上报的玩家对象或库存。
    private static bool Prefix(PropEnt_Merchant_Base __instance, int index, NetworkConnectionToClient sender,
        out SharedShopState.PurchaseAttempt __state)
    {
        return ShopShareController.BeforePurchase(__instance, sender?.GetPlayer(), index, out __state);
    }

    // Finalizer 在原生提前返回或抛异常时也会运行，避免购买锁残留，并保留原有异常行为。
    private static Exception Finalizer(PropEnt_Merchant_Base __instance,
        SharedShopState.PurchaseAttempt __state, Exception __exception)
    {
        try
        {
            ShopShareController.AfterPurchase(__instance, __state);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[DewShopShare] 同步共享库存失败：{exception}");
        }

        return __exception;
    }
}

[HarmonyPatch(typeof(PropEnt_Merchant_Base), "OnDestroyActor_Merchandise")]
internal static class MerchantDestroyPatch
{
    // 该基类清理方法覆盖所有商人子类的正常销毁路径。
    private static void Postfix(PropEnt_Merchant_Base __instance)
    {
        ShopShareController.Remove(__instance);
    }
}
