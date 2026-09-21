using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DewVascularThief.util;

internal static class BossAbilitySelector
{
    public static AbilityTrigger Select(Entity boss)
    {
        List<AbilityTrigger> candidates = new List<AbilityTrigger>();
        foreach (KeyValuePair<int, AbilityTrigger> pair in boss.Ability.abilities)
        {
            if (IsStealableAbility(pair.Key, pair.Value))
            {
                candidates.Add(pair.Value);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    private static bool IsStealableAbility(int index, AbilityTrigger ability)
    {
        if (ability == null || !ability.isActive || ability is SkillTrigger || ability is AttackTrigger)
        {
            return false;
        }

        if (index == EntityAbility.AttackAbilityIndex)
        {
            return false;
        }

        if (ability.configs == null || ability.configs.Length == 0)
        {
            return false;
        }

        return ability.configs.Any(c => c != null && c.isActive);
    }
}
