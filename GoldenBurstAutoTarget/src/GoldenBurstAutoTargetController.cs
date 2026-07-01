using System;
using UnityEngine;

namespace GoldenBurstAutoTarget;

internal sealed class GoldenBurstAutoTargetController
{
    private readonly GoldenBurstCastInput _input;
    private readonly GoldenBurstSkillProvider _skillProvider;
    private readonly GoldenBurstTargetSelector _targetSelector;
    private readonly GoldenBurstCaster _caster;

    public GoldenBurstAutoTargetController(
        GoldenBurstCastInput input,
        GoldenBurstSkillProvider skillProvider,
        GoldenBurstTargetSelector targetSelector,
        GoldenBurstCaster caster)
    {
        _input = input;
        _skillProvider = skillProvider;
        _targetSelector = targetSelector;
        _caster = caster;
    }

    public void Update(float time)
    {
        if (!_input.ShouldCast(time))
        {
            return;
        }

        TryCast();
    }

    private void TryCast()
    {
        try
        {
            if (!_skillProvider.TryGetReadyGoldenBurst(out Hero hero, out SkillTrigger skill))
            {
                return;
            }

            Entity target = _targetSelector.FindNearestTarget(hero, skill);
            if (target == null)
            {
                return;
            }

            _caster.Cast(hero, skill, target);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}
