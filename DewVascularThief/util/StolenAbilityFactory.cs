using System;
using DewVascularThief.config;
using UnityEngine;

namespace DewVascularThief.util;

internal static class StolenAbilityFactory
{
    public static AbilityTrigger CreateFromSource(AbilityTrigger source, Hero hero)
    {
        if (source == null || hero == null)
        {
            return null;
        }

        AbilityTrigger prefab = DewResources.GetByType<AbilityTrigger>(source.GetType());
        if (prefab == null)
        {
            Debug.LogWarning($"[{VascularThiefText.ModKey}] No ability prefab found for {source.GetType().Name}.");
            return null;
        }

        AbilityTrigger clone = Dew.CreateAbilityTrigger(prefab, PrepareStolenAbility);
        hero.Ability.AddAbility(clone);
        RefillWhenReady(clone);
        return clone;
    }

    private static void PrepareStolenAbility(AbilityTrigger ability)
    {
        ability.persistentData.SetData(VascularThiefText.ModKey, "StolenBossAbility", true);
        if (ability.configs == null)
        {
            return;
        }

        TriggerConfigRuntimeEditor.AttachAll(ability);
        foreach (TriggerConfig config in ability.configs)
        {
            if (config == null)
            {
                continue;
            }

            TriggerConfigRuntimeEditor.SetManaCost(config, 0f);
            config.canReceiveCooldownReduction = true;
        }
    }

    private static void RefillWhenReady(AbilityTrigger ability)
    {
        if (TryRefillAbility(ability))
        {
            return;
        }

        Dew.CallDelayed(delegate
        {
            try
            {
                TryRefillAbility(ability);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        });
    }

    private static bool TryRefillAbility(AbilityTrigger ability)
    {
        if (ability == null || !ability.isActive || ability.configs == null)
        {
            return true;
        }

        if (ability.currentUnscaledCooldownTimes == null ||
            ability.currentCharges == null ||
            ability.currentUnscaledCooldownTimes.Length < ability.configs.Length ||
            ability.currentCharges.Length < ability.configs.Length)
        {
            return false;
        }

        ability.SetCooldownTimeAll(0f, scaled: false);
        for (int i = 0; i < ability.configs.Length; i++)
        {
            if (ability.configs[i] != null)
            {
                ability.SetCharge(i, ability.configs[i].maxCharges);
            }
        }

        return true;
    }
}
