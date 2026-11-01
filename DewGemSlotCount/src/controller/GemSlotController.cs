using System;
using System.Collections.Generic;
using DewGemSlotCount.patch;
using Mirror;
using UnityEngine;

namespace DewGemSlotCount.controller;

public static class GemSlotController
{
    private static readonly HeroSkillLocation[] Locations =
    {
        HeroSkillLocation.Q, HeroSkillLocation.W, HeroSkillLocation.E,
        HeroSkillLocation.R, HeroSkillLocation.Identity, HeroSkillLocation.Movement
    };

    private static readonly Dictionary<HeroSkill, int[]> AppliedBases = new();
    private static readonly Dictionary<HeroSkill, int[]> ActualCounts = new();

    // 只在 Mod 重载时接管仍然存活的英雄，普通生成/读档不把旧 netId 快照当作奖励。
    public static void Resume(HeroSkill skill)
    {
        if (!NetworkServer.active || skill == null || !skill.isServer) return;
        var counts = new int[Locations.Length];
        for (int i = 0; i < counts.Length; i++)
            if (!GemSlotStateSync.TryRead(skill, Locations[i], out counts[i])) return;
        ActualCounts[skill] = counts;
    }

    public static void ResumeExisting()
    {
        if (DewPlayer.allHumanPlayers == null) return;
        foreach (var player in DewPlayer.allHumanPlayers)
            if (player != null && player.hero != null) Resume(player.hero.Skill);
    }

    // 真实槽数保留在服务端和单向快照中，原版 SyncVar 始终不超过四槽。
    public static void RecordCount(HeroSkill skill, HeroSkillLocation location, int count)
    {
        if (!ActualCounts.TryGetValue(skill, out int[] counts))
        {
            counts = new int[Locations.Length];
            for (int i = 0; i < counts.Length; i++) counts[i] = skill.GetMaxGemCount(Locations[i]);
            ActualCounts.Add(skill, counts);
        }
        counts[(int)location] = count;
        GemSlotStateSync.Publish(skill, counts);
    }

    public static bool TryGetCount(HeroSkill skill, HeroSkillLocation location, out int count)
    {
        count = 0;
        if (!ActualCounts.TryGetValue(skill, out int[] counts)) return false;
        int index = (int)location;
        if (index < 0 || index >= counts.Length) return false;
        count = counts[index];
        return true;
    }

    // 仅服务端调整基础槽差值；重复初始化和配置刷新均保留当前运行时加成。
    public static void ApplyConfig(HeroSkill skill)
    {
        if (DewGemSlotCount.Instance == null || !NetworkServer.active || skill == null || !skill.isServer)
            return;

        if (!AppliedBases.TryGetValue(skill, out int[] bases))
        {
            bases = new int[Locations.Length];
            for (int i = 0; i < Locations.Length; i++)
                bases[i] = HeroSkill_Patch.GetVanillaBaseGemCount(Locations[i]);
            AppliedBases.Add(skill, bases);
        }

        for (int i = 0; i < Locations.Length; i++)
        {
            HeroSkillLocation location = Locations[i];
            int configured = HeroSkill_Patch.GetConfiguredBaseGemCount(location, skill);
            int bonus = Mathf.Max(0, skill.GetMaxGemCount(location) - bases[i]);
            skill.SetMaxGemCount(location, Mathf.Clamp(configured + bonus, 0, Constant.MaxGemCount));
            bases[i] = configured;
        }
    }

    // 原版混沌按固定基础槽重写总数，随后用配置基础槽与已保存的加成重算完整容量。
    public static void ApplyChaosBonus(Se_Shrine_Chaos_StatBonus effect)
    {
        if (DewGemSlotCount.Instance == null || !NetworkServer.active || effect == null || effect.victim is not Hero hero)
            return;

        HeroSkill skill = hero.Skill;
        if (skill == null || !skill.isServer) return;
        // 先登记基础槽，避免晚于混沌初始化时将配置基础槽再加一次。
        if (!AppliedBases.ContainsKey(skill))
            ApplyConfig(skill);

        int[] bonuses =
        {
            effect.currentAddedGemSlotQ, effect.currentAddedGemSlotW,
            effect.currentAddedGemSlotE, effect.currentAddedGemSlotR,
            effect.currentAddedGemSlotIdentity
        };
        for (int i = 0; i < bonuses.Length; i++)
        {
            int configured = HeroSkill_Patch.GetConfiguredBaseGemCount(Locations[i], skill);
            skill.SetMaxGemCount(Locations[i], Mathf.Clamp(configured + bonuses[i], 0, Constant.MaxGemCount));
            AppliedBases[skill][i] = configured;
        }
    }

    // 房主修改配置时立即更新已生成英雄，不等待下一次混沌奖励。
    public static void RefreshAll()
    {
        if (!NetworkServer.active) return;
        // Mod 在游戏中启用、或先前启动失败时，补登记已经生成的玩家英雄。
        if (DewPlayer.allHumanPlayers != null)
        {
            foreach (DewPlayer player in DewPlayer.allHumanPlayers)
                if (player != null && player.hero != null) ApplyConfig(player.hero.Skill);
        }
        foreach (HeroSkill skill in new List<HeroSkill>(AppliedBases.Keys))
        {
            if (skill == null || !skill.isServer)
                AppliedBases.Remove(skill);
            else
                ApplyConfig(skill);
        }
        foreach (HeroSkill skill in new List<HeroSkill>(ActualCounts.Keys))
            if (skill == null || !skill.isServer) ActualCounts.Remove(skill);
            else GemSlotStateSync.Publish(skill, ActualCounts[skill]);
    }

    // 房间结束和 Mod 卸载时释放英雄引用。
    public static void Clear()
    {
        AppliedBases.Clear();
        ActualCounts.Clear();
    }

    // 重载前撤回本 Mod 的基础槽差值，保留运行时加成；新实例按原版基础重新应用时不会叠加。
    public static void RestoreAll()
    {
        try
        {
            if (!NetworkServer.active) return;
            foreach (var pair in AppliedBases)
            {
                HeroSkill skill = pair.Key;
                if (skill == null || !skill.isServer) continue;
                try
                {
                    for (int i = 0; i < Locations.Length; i++)
                    {
                        int bonus = Mathf.Max(0, skill.GetMaxGemCount(Locations[i]) - pair.Value[i]);
                        skill.SetMaxGemCount(Locations[i], HeroSkill_Patch.GetVanillaBaseGemCount(Locations[i]) + bonus);
                    }
                }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }
        finally { AppliedBases.Clear(); }
    }
}
