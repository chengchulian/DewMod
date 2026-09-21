using System;
using DewVascularThief.config;

namespace DewVascularThief.util;

internal static class VascularThiefProfileRegistry
{
    public static void Register()
    {
        RegisterContentSettings(DewBuildProfile.current?.content);
        VascularThiefCollectablesRegistry.Register();
        RegisterProfile(DewSave.profileMain);
        RegisterProfileStats(DewSave.profileStats);
    }

    public static void RegisterContentSettings(DewGameContentSettings content)
    {
        if (content == null)
        {
            return;
        }

        bool hasExplicitSkillList =
            (content._availableSkills != null && content._availableSkills.Length > 0) ||
            (content.availableSkills != null && content.availableSkills.Count > 0);
        if (!hasExplicitSkillList)
        {
            return;
        }

        if (content._availableSkills != null && !Contains(content._availableSkills, VascularThiefText.SkillTypeName))
        {
            string[] oldArray = content._availableSkills;
            string[] newArray = new string[oldArray.Length + 1];
            Array.Copy(oldArray, newArray, oldArray.Length);
            newArray[oldArray.Length] = VascularThiefText.SkillTypeName;
            content._availableSkills = newArray;
        }

        if (content.availableSkills != null && !content.availableSkills.Contains(VascularThiefText.SkillTypeName))
        {
            content.availableSkills.Add(VascularThiefText.SkillTypeName);
        }
    }

    public static void RegisterProfile(DewProfile profile)
    {
        if (profile == null)
        {
            return;
        }

        if (profile.skills != null && !profile.skills.ContainsKey(VascularThiefText.SkillTypeName))
        {
            profile.skills.Add(VascularThiefText.SkillTypeName, new DewProfile.UnlockData
            {
                status = UnlockStatus.Complete,
                didReadMemory = true,
                isNewHeroOrHeroSkill = false
            });
        }

        if (profile.dejavuCostReductionPeriodTimestamp != null &&
            !profile.dejavuCostReductionPeriodTimestamp.ContainsKey(VascularThiefText.SkillTypeName))
        {
            profile.dejavuCostReductionPeriodTimestamp.Add(VascularThiefText.SkillTypeName, 0L);
        }
    }

    public static void RegisterProfileStats(DewProfileStats stats)
    {
        if (stats?.skills != null && !stats.skills.ContainsKey(VascularThiefText.SkillTypeName))
        {
            stats.skills.Add(VascularThiefText.SkillTypeName, new DewProfileStats.ItemData());
        }
    }

    private static bool Contains(string[] values, string target)
    {
        foreach (string value in values)
        {
            if (value == target)
            {
                return true;
            }
        }

        return false;
    }
}
