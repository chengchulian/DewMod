using System;
using System.Linq;
using UnityEngine;

namespace DewUnLock.controller;

/// <summary>集中处理存档修改，避免 UI 层直接依赖游戏存档 API。</summary>
public static class UnlockController
{
    public static void CompleteAllAchievements()
    {
        var count = 0;
        if (ManagerBase<AchievementManager>.instance != null)
        {
            count = ManagerBase<AchievementManager>.instance.trackedAchievements.Count;
            for (var i = count - 1; i >= 0; i--) ManagerBase<AchievementManager>.instance.CompleteAchievement(ManagerBase<AchievementManager>.instance.trackedAchievements[i]);
        }
        else
        {
            foreach (var pair in DewSave.profileMain.achievements)
            {
                if (pair.Value.isCompleted) continue;
                count++;
                pair.Value.isCompleted = true;
                pair.Value.currentProgress = pair.Value.maxProgress;
                pair.Value.persistentVariables = null;
                var achievement = (DewAchievementItem)Activator.CreateInstance(Dew.achievementsByName[pair.Key]);
                DewSave.profileMain.stardust += achievement.grantedStardust;
            }
            DewSave.SaveProfileMain(false);
            DewSave.profileMain.Validate();
        }
        Debug.Log($"已完成 {count} 个成就");
    }

    public static void DiscoverAll()
    {
        // 仅处理尚未发现的条目，使用游戏定义的枚举避免版本间整数值漂移。
        foreach (var pair in DewSave.profileMain.skills.Where(pair => pair.Value.status == UnlockStatus.NotDiscovered)) DewSave.profileMain.DiscoverSkill(pair.Key);
        foreach (var pair in DewSave.profileMain.gems.Where(pair => pair.Value.status == UnlockStatus.NotDiscovered)) DewSave.profileMain.DiscoverGem(pair.Key);
        foreach (var pair in DewSave.profileMain.artifacts.Where(pair => pair.Value.status == UnlockStatus.NotDiscovered)) DewSave.profileMain.DiscoverArtifact(pair.Key);
        DewSave.SaveProfileMain(false);
        DewSave.profileMain.Validate();
        Debug.Log("已发现所有物品");
    }

    public static void UnlockAllItems()
    {
        foreach (var key in DewSave.profileMain.emotes.Keys.ToArray()) DewSave.profileMain.UnlockEmote(key, null);
        foreach (var key in DewSave.profileMain.accessories.Keys.ToArray()) DewSave.profileMain.UnlockAccessory(key, null);
        foreach (var key in DewSave.profileMain.nametags.Keys.ToArray()) DewSave.profileMain.UnlockNametag(key, null);
        DewSave.SaveProfileMain(false);
        Debug.Log("已解锁所有饰品");
    }

    public static void SetAllMasteryLevels()
    {
        foreach (var heroType in Dew.allHeroes)
            if (DewSave.profileStats.heroes.TryGetValue(heroType.Name, out var data)) data.masteryLevel = 50;
        DewSave.profileStats.UpdateTotalData(0L);
        DewSave.profileMain.didRewardMastery = true;
        DewSave.SaveProfileAll(false);
        DewSave.SaveProfileMain(false);
        Debug.Log("已将所有英雄熟练度设置为50");
    }

    public static void UnlockDejaVu()
    {
        foreach (var pair in DewSave.profileStats.gems) pair.Value.wins = 1L;
        foreach (var pair in DewSave.profileStats.skills) pair.Value.wins = 1L;
        DewSave.profileStats.UpdateTotalData(0L);
        DewSave.SaveProfileAll(false);
        DewSave.SaveProfileMain(false);
        Debug.Log("已解锁既视感");
    }

    public static void AddTenThousandStardust()
    {
        DewSave.profileMain.stardust += 10000;
        DewSave.SaveProfileMain(false);
        Debug.Log("已增加10000星辰");
    }

    /// <summary>按游戏当前版本的最高深度解锁所有英雄迷失域进度。</summary>
    public static void UnlockAllHeroesLimboMaxDepth()
    {
        var targetDepth = GameMod_Limbo.GetMaxDepths();
        var count = 0;
        foreach (var pair in DewSave.profileStats.heroes)
        {
            if (pair.Value.completedLimboDepth >= targetDepth) continue;
            pair.Value.completedLimboDepth = targetDepth;
            count++;
        }
        DewSave.SaveProfileStats(false);
        Debug.Log($"已将 {count} 名英雄的迷失域进度解锁至最高级 {targetDepth}");
    }
}
