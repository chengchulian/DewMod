namespace DewVascularThief.localization;

internal static class VascularThiefSkillText
{
    public static string Name => VascularThiefI18n.Get(VascularThiefI18nKeys.SkillName);
    public static string ShortDescription => VascularThiefI18n.Get(VascularThiefI18nKeys.SkillShortDescription);
    public static string Memory => VascularThiefI18n.Get(VascularThiefI18nKeys.SkillMemory);

    public static string GetDescription(int damagePercent)
    {
        return VascularThiefI18n.Format(VascularThiefI18nKeys.SkillDescription, damagePercent);
    }

    public static string GetCurrentStolenLine(string sourceAbilityType)
    {
        string text = VascularThiefI18n.Format(VascularThiefI18nKeys.SkillCurrentStolen, sourceAbilityType);
        return "\n<color=#ffb3b3>" + text + "</color>";
    }
}
