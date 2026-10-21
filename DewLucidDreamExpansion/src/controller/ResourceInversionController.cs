using System;
using Mirror;

namespace DewLucidDreamExpansion.controller;

internal static class ResourceInversionController
{
    [ThreadStatic] private static bool convertingEarn;

    // 只交换新增收入，保留已有余额并使用与原版相同的饱和上限。
    internal static bool TryInvertAdd(DewPlayer player, int amount, bool isGold)
    {
        if (convertingEarn || !NetworkServer.active || amount <= 0) return false;
        if (isGold) player.dreamDust = (int)Math.Min((long)player.dreamDust + amount, int.MaxValue);
        else player.gold = (int)Math.Min((long)player.gold + amount, int.MaxValue);
        return true;
    }

    // Earn 系列连同通知一起交换；重入原版方法时跳过第二次转换。
    internal static bool TryInvertEarn(DewPlayer player, int amount, bool isGold)
    {
        if (convertingEarn || !NetworkServer.active || amount <= 0) return false;
        convertingEarn = true;
        try
        {
            if (isGold) player.EarnDreamDust(amount);
            else player.EarnGold(amount);
        }
        finally { convertingEarn = false; }
        return true;
    }
}
