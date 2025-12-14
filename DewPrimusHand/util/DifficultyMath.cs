using UnityEngine;

namespace DewPrimusHand.util;

public static class DifficultyMath
{
    public static float ExponentialGrowth(float x, float baseValue, float multiplier)
    {
        if (multiplier <= 1e-6f)
            return baseValue;

        return baseValue * Mathf.Pow(multiplier, x);
    }
}