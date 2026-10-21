using UnityEngine;

namespace DewPrimusHand.util;

public static class DifficultyMath
{
    public static float SanitizeMultiplier(float value, float fallback = 1f)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return fallback;

        return Mathf.Max(0f, value);
    }

    public static float ScaleDifficultyPercentage(float basePercentage, float multiplier)
    {
        return basePercentage * (SanitizeMultiplier(multiplier) - 1f);
    }

    public static float ScaleMultiplier(float baseMultiplier, float multiplier)
    {
        return baseMultiplier * SanitizeMultiplier(multiplier);
    }

    public static float ExponentialGrowth(float x, float baseValue, float multiplier)
    {
        // 负数底数按原值处理；0 则在正数区域索引时归零。
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0f)
            return baseValue;

        return baseValue * Mathf.Pow(Mathf.Max(0f, multiplier), x);
    }
}
