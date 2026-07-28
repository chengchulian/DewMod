using DewVascularThief.config;

namespace DewVascularThief.util;

internal static class VascularThiefSkillMarker
{
    public static void Mark(SkillTrigger skill)
    {
        skill.characterSkillOwner = VascularThiefText.MarkerKey;
        skill.persistentData.SetData(VascularThiefText.ModKey, VascularThiefText.MarkerKey, true);
    }

    public static bool IsMarked(SkillTrigger skill)
    {
        if (skill == null)
        {
            return false;
        }

        if (skill is St_U_VascularThief)
        {
            return true;
        }

        if (skill.characterSkillOwner == VascularThiefText.MarkerKey)
        {
            return true;
        }

        return skill.persistentData.GetDataOrDefault(
            VascularThiefText.ModKey,
            VascularThiefText.MarkerKey,
            defaultValue: false);
    }
}
