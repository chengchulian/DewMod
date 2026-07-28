using System;
using System.Collections.Generic;
using System.Linq;
using DewVascularThief.config;
using DewVascularThief.localization;
using Mirror;
using UnityEngine;

namespace DewVascularThief.util;

internal sealed class VascularThiefController
{
    private readonly Dictionary<uint, StolenAbility> _stolenBySkillNetId = new Dictionary<uint, StolenAbility>();

    public bool CanCastVascularThiefNow(SkillTrigger skill, CastInfo info)
    {
        if (!VascularThiefSkillMarker.IsMarked(skill))
        {
            return true;
        }

        if (skill == null || skill.owner == null)
        {
            return false;
        }

        if (!TryGetSkillKey(skill, out uint skillKey))
        {
            return false;
        }

        if (_stolenBySkillNetId.TryGetValue(skillKey, out StolenAbility existing))
        {
            if (IsStolenAbilityUsable(existing))
            {
                VascularThiefUpgradeScaling.SyncStolenAbility(skill, existing.Ability);
                VascularThiefProxyConfig.ApplyStolenAbilityMode(skill, existing.Ability);
                return true;
            }

            RemoveStolenAbility(skill);
        }

        if (!NetworkServer.active && skill.currentConfigIndex == VascularThiefSkillMode.Stolen)
        {
            return true;
        }

        return info.target != null && info.target.IsAnyBoss();
    }

    public void HandleCastComplete(SkillTrigger skill, CastInfo info)
    {
        if (!NetworkServer.active || !VascularThiefSkillMarker.IsMarked(skill))
        {
            return;
        }

        Hero hero = skill.owner;
        if (hero == null)
        {
            return;
        }

        if (!TryGetSkillKey(skill, out uint skillKey))
        {
            return;
        }

        if (_stolenBySkillNetId.TryGetValue(skillKey, out StolenAbility existing))
        {
            if (IsStolenAbilityUsable(existing))
            {
                VascularThiefUpgradeScaling.SyncStolenAbility(skill, existing.Ability);
                VascularThiefProxyConfig.ApplyStolenAbilityMode(skill, existing.Ability);
                StolenAbilityCaster.Cast(existing.Ability, hero, info);
                return;
            }

            RemoveStolenAbility(skill);
        }

        Entity target = info.target;
        if (target == null || !target.IsAnyBoss())
        {
            return;
        }

        AbilityTrigger selected = BossAbilitySelector.Select(target);
        if (selected == null)
        {
            Debug.LogWarning($"[{VascularThiefText.ModKey}] No stealable ability found on {target.GetActorReadableName()}.");
            return;
        }

        try
        {
            AbilityTrigger clone = StolenAbilityFactory.CreateFromSource(selected, hero);
            if (clone == null)
            {
                Debug.LogWarning($"[{VascularThiefText.ModKey}] Failed to create stolen ability from {selected.GetType().Name}.");
                return;
            }

            VascularThiefUpgradeScaling.InitializeStolenAbility(skill, clone);
            _stolenBySkillNetId[skillKey] = new StolenAbility(hero, clone, clone.abilityIndex, selected.GetType().Name);
            VascularThiefProxyConfig.ApplyStolenAbilityMode(skill, clone);
            Debug.Log($"[{VascularThiefText.ModKey}] {hero.GetActorReadableName()} stole {selected.GetType().Name} from {target.GetActorReadableName()}.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    public bool ShouldSkipInitialStealCooldown(SkillTrigger skill, CastInfo info)
    {
        if (!VascularThiefSkillMarker.IsMarked(skill) ||
            skill.currentConfigIndex != VascularThiefSkillMode.Steal ||
            info.target == null ||
            !info.target.IsAnyBoss())
        {
            return false;
        }

        return !TryGetUsableStolenAbility(skill, out _);
    }

    public void RemoveStolenAbility(SkillTrigger skill)
    {
        if (TryGetSkillKey(skill, out uint skillKey))
        {
            RemoveStolenAbility(skillKey);
            VascularThiefProxyConfig.ApplyStealMode(skill);
        }
    }

    public void CleanupAllStolenAbilities()
    {
        foreach (uint skillNetId in _stolenBySkillNetId.Keys.ToArray())
        {
            RemoveStolenAbility(skillNetId);
        }
    }

    public string GetDescription(SkillTrigger skill)
    {
        string description = VascularThiefSkillText.GetDescription(VascularThiefUpgradeScaling.GetDamagePercent(skill));
        if (!TryGetSkillKey(skill, out uint skillKey) ||
            !_stolenBySkillNetId.TryGetValue(skillKey, out StolenAbility stolen))
        {
            return description;
        }

        return description + VascularThiefSkillText.GetCurrentStolenLine(stolen.SourceAbilityType);
    }

    private bool TryGetUsableStolenAbility(SkillTrigger skill, out StolenAbility stolen)
    {
        stolen = default;
        if (!TryGetSkillKey(skill, out uint skillKey) ||
            !_stolenBySkillNetId.TryGetValue(skillKey, out stolen))
        {
            return false;
        }

        return IsStolenAbilityUsable(stolen);
    }

    private void RemoveStolenAbility(uint skillNetId)
    {
        if (!_stolenBySkillNetId.TryGetValue(skillNetId, out StolenAbility stolen))
        {
            return;
        }

        _stolenBySkillNetId.Remove(skillNetId);
        try
        {
            if (ShouldDetachFromHero(stolen))
            {
                stolen.Hero.Ability.RemoveAbility(stolen.Index);
            }

            if (stolen.Ability != null && stolen.Ability.isActive)
            {
                stolen.Ability.Destroy();
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static bool ShouldDetachFromHero(StolenAbility stolen)
    {
        return stolen.Hero != null &&
               stolen.Ability != null &&
               stolen.Ability.isActive &&
               stolen.Hero.Ability.abilities.TryGetValue(stolen.Index, out AbilityTrigger current) &&
               current == stolen.Ability;
    }

    private static bool IsStolenAbilityUsable(StolenAbility stolen)
    {
        return stolen.Hero != null &&
               stolen.Ability != null &&
               stolen.Ability.isActive &&
               stolen.Hero.Ability.abilities.TryGetValue(stolen.Index, out AbilityTrigger current) &&
               current == stolen.Ability;
    }

    private static bool TryGetSkillKey(SkillTrigger skill, out uint skillKey)
    {
        skillKey = 0;
        if (skill == null)
        {
            return false;
        }

        NetworkIdentity identity = skill.GetComponent<NetworkIdentity>();
        if (identity == null || identity.netId == 0)
        {
            return false;
        }

        skillKey = identity.netId;
        return true;
    }
}
