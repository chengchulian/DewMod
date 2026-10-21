using DewShopShare.controller;
using HarmonyLib;

namespace DewShopShare.patch;

// 源码依据：Dewr.1.4.0.13_s/Dew.Core/PropEnt_Merchant_Base.cs。
[HarmonyPatch(typeof(PropEnt_Merchant_Base), nameof(PropEnt_Merchant_Base.PopulatePlayerMerchandises))]
internal static class MerchantPopulatePatch
{
    // 原生刷新覆盖数组前，记录共享商品的真实剩余数量。
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

[HarmonyPatch(typeof(PropEnt_Merchant_Base), "OnDestroyActor_Merchandise")]
internal static class MerchantDestroyPatch
{
    // 该基类清理方法覆盖所有商人子类的正常销毁路径。
    private static void Postfix(PropEnt_Merchant_Base __instance)
    {
        ShopShareController.Remove(__instance);
    }
}
