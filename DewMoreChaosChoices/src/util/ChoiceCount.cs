using System;

namespace DewMoreChaosChoices.util;

public static class ChoiceCount
{
    // 保留原版基础数量，使用长整数叠加天赋和配置增量，再按奖励池裁剪。
    public static int Resolve(int vanillaCount, int bonusCount, int extraCount, int availableCount)
    {
        if (availableCount <= 0)
        {
            return 0;
        }

        long requested = (long)vanillaCount + bonusCount + Math.Max(0, extraCount);
        return (int)Math.Min(availableCount, Math.Max(1L, requested));
    }
}
