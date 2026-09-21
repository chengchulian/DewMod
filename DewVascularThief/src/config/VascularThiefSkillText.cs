namespace DewVascularThief.config;

internal static class VascularThiefSkillText
{
    public static string Name => LocalizationSource.GetLocalizationText(VascularThiefI18nKeys.SkillName);
    public static string ShortDescription => LocalizationSource.GetLocalizationText(VascularThiefI18nKeys.SkillShortDescription);
    public static string Memory => LocalizationSource.GetLocalizationText(VascularThiefI18nKeys.SkillMemory);

    public static string GetDescription(int damagePercent)
    {
        return LocalizationSource.GetLocalizationText(VascularThiefI18nKeys.SkillDescription, damagePercent);
    }

    public static string GetCurrentStolenLine(string sourceAbilityType)
    {
        string text = LocalizationSource.GetLocalizationText(VascularThiefI18nKeys.SkillCurrentStolen, sourceAbilityType);
        return "\n<color=#ffb3b3>" + text + "</color>";
    }
}
