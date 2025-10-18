using System;

namespace DewDifficultyCustomizeMod.util
{
    public class AttrCustomizeUtil
    {
        public static float ExponentialGrowth(int x, double initialY, double multiplier)
        {
            if (multiplier - 0 < 0.00001)
            {
                return (float)initialY;
            }

            return (float)(initialY * Math.Pow(multiplier, x));
        }

    }
}