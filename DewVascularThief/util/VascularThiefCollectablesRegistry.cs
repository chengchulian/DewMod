using System;
using System.Collections.Generic;
using System.Reflection;

namespace DewVascularThief.util;

internal static class VascularThiefCollectablesRegistry
{
    public static void Register()
    {
        List<Type> allSkills = GetAllSkills();
        if (allSkills == null || allSkills.Contains(typeof(St_U_VascularThief)))
        {
            return;
        }

        allSkills.Add(typeof(St_U_VascularThief));
    }

    private static List<Type> GetAllSkills()
    {
        _ = Dew.allSkills;

        FieldInfo allSkillsField = typeof(Dew).GetField("_allSkills", BindingFlags.NonPublic | BindingFlags.Static);
        return allSkillsField?.GetValue(null) as List<Type>;
    }
}
