public class St_U_VascularThief : SkillTrigger
{
    public override void OnCastCompleteSetCooldownTime(int configIndex, CastInfo info)
    {
        if (DewVascularThief.DewVascularThief.Instance?.Controller.ShouldSkipInitialStealCooldown(this, info) == true)
        {
            return;
        }

        base.OnCastCompleteSetCooldownTime(configIndex, info);
    }

    public override void OnCastCompleteSetCharge(int configIndex, CastInfo info)
    {
        if (DewVascularThief.DewVascularThief.Instance?.Controller.ShouldSkipInitialStealCooldown(this, info) == true)
        {
            return;
        }

        base.OnCastCompleteSetCharge(configIndex, info);
    }

    protected override void OnLevelChange(int oldLevel, int newLevel)
    {
        if (newLevel < 1)
        {
            return;
        }

        if (owner == null)
        {
            ClientSkillEvent_OnLevelChange?.Invoke(oldLevel, newLevel);
            return;
        }

        base.OnLevelChange(oldLevel, newLevel);
    }

    private void MirrorProcessed()
    {
    }
}
