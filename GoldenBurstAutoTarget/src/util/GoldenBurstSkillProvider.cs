namespace GoldenBurstAutoTarget;

internal sealed class GoldenBurstSkillProvider
{
    public bool TryGetReadyGoldenBurst(out Hero hero, out SkillTrigger skill)
    {
        hero = DewPlayer.local?.hero;
        skill = null;

        if (hero == null || hero.IsNullInactiveDeadOrKnockedOut())
        {
            return false;
        }

        if (!hero.Skill.TryGetSkill(HeroSkillLocation.Q, out skill))
        {
            return false;
        }

        return skill != null && skill.CanBeCast();
    }
}
