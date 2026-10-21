namespace DewGoldenBurstAutoTarget;

internal sealed class GoldenBurstSkillProvider
{
    private static readonly HeroSkillLocation[] Locations =
    [
        HeroSkillLocation.Q,
        HeroSkillLocation.W,
        HeroSkillLocation.E,
        HeroSkillLocation.R,
        HeroSkillLocation.Identity,
        HeroSkillLocation.Movement
    ];

    // 每次施放前检查所有当前技能槽，换位或放入位移槽后也能找到金色爆发。
    public bool TryGetReadyGoldenBurst(out Hero hero, out SkillTrigger skill)
    {
        hero = DewPlayer.local?.hero;
        skill = null;

        if (hero == null || hero.IsNullInactiveDeadOrKnockedOut() || hero.Skill == null)
        {
            return false;
        }

        foreach (HeroSkillLocation location in Locations)
        {
            if (hero.Skill.TryGetSkill(location, out SkillTrigger candidate) &&
                candidate is St_Q_GoldenBurst && candidate.CanBeCast())
            {
                skill = candidate;
                return true;
            }
        }

        return false;
    }
}
