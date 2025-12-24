using System;
using System.Collections.Generic;
using System.Linq;

namespace DewIdentityChange.config;

public class HeroSkillSource
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

                if (skillTrigger.rarity is Rarity.Character or Rarity.Identity)
                {
                    var strings = key.Split("_");
                    if (strings.Length <= 1) continue;
                    var heroSkillLocation = strings[0];
                    switch (heroSkillLocation)
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
            }

            return dict;
        });

    public static Dictionary<HeroSkillLocation, List<string>> SkillNamesByType => _skillsByTypeLazy.Value;
    




}