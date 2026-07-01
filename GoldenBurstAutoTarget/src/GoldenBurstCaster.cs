namespace GoldenBurstAutoTarget;

internal sealed class GoldenBurstCaster
{
    public void Cast(Hero hero, SkillTrigger skill, Entity target)
    {
        CastInfo castInfo = BuildCastInfo(hero, skill, target);
        hero.Control.CmdCast(skill, skill.currentConfigIndex, castInfo, allowMoveToCast: false, skipRangeCheck: false);

        if (!skill.currentConfig.postponeBasicCommand)
        {
            hero.Control.CmdAttack(null, doChase: false);
        }
    }

    private static CastInfo BuildCastInfo(Hero hero, SkillTrigger skill, Entity target)
    {
        switch (skill.currentConfig.castMethod.type)
        {
            case CastMethodType.Point:
                return new CastInfo(hero, target.agentPosition);
            case CastMethodType.Target:
                return new CastInfo(hero, target);
            case CastMethodType.Cone:
            case CastMethodType.Arrow:
                return new CastInfo(hero, CastInfo.GetAngle(target.agentPosition - hero.agentPosition));
            case CastMethodType.None:
                return new CastInfo(hero);
            default:
                return skill.GetPredictedCastInfoToTarget(target, 0.5f);
        }
    }
}
