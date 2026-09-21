using System;
using System.Collections.Generic;

namespace DewIdentityChange.config;

public static class HeroSkillSource
{
    private static readonly Lazy<Dictionary<HeroSkillLocation, List<string>>> _skillsByTypeLazy =
        new(() =>
        {
            var dict = new Dictionary<HeroSkillLocation, List<string>>
            {
                { HeroSkillLocation.Q, [] },
                { HeroSkillLocation.R, [] },
                { HeroSkillLocation.Identity, [] },
                { HeroSkillLocation.Movement, [] }
            };

            foreach (var keyValuePair in DewLocalization.data.skills)
            {
                var key = keyValuePair.Key;
                var byShortTypeName = DewResources.GetByShortTypeName("St_" + key);
                if (byShortTypeName is not SkillTrigger skillTrigger) continue;

                if (skillTrigger.rarity is not (Rarity.Character or Rarity.Identity)) continue;

                var parts = key.Split("_");
                if (parts.Length <= 1) continue;

                switch (parts[0])
                {
                    case "Q":
                        dict[HeroSkillLocation.Q].Add(skillTrigger.name);
                        break;
                    case "R":
                        dict[HeroSkillLocation.R].Add(skillTrigger.name);
                        break;
                    case "D":
                        dict[HeroSkillLocation.Identity].Add(skillTrigger.name);
                        break;
                    case "M":
                        dict[HeroSkillLocation.Movement].Add(skillTrigger.name);
                        break;
                    case "QR":
                        dict[HeroSkillLocation.Q].Add(skillTrigger.name);
                        dict[HeroSkillLocation.R].Add(skillTrigger.name);
                        break;
                }
            }

            return dict;
        });

    public static Dictionary<HeroSkillLocation, List<string>> SkillNamesByType => _skillsByTypeLazy.Value;
}
