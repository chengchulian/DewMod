using System;
using System.Collections.Generic;
using System.Linq;
using DewLucidDreamExpansion.content;
using UnityEngine;

namespace DewLucidDreamExpansion.controller;

internal static class AstronomyController
{
    // 中途加入时，祭坛可能先于英雄生成选项；英雄技能就绪后补齐。
    internal static void RefreshHeroChoices(Hero hero)
    {
        if (!Mirror.NetworkServer.active || hero == null || hero.owner == null ||
            NetworkedManagerBase<ActorManager>.instance == null) return;
        foreach (Actor actor in NetworkedManagerBase<ActorManager>.instance.allActors)
            if (actor is Shrine_Astronomy shrine && shrine.isActive)
                RefreshPlayerChoices(shrine, hero.owner);
    }

    // 读档或切换英雄后，展示前移除不属于当前英雄的旧候选。
    internal static void RefreshPlayerChoices(Shrine_Astronomy shrine, DewPlayer player)
    {
        if (!Mirror.NetworkServer.active || player?.hero == null) return;
        var allowed = new HashSet<string>(GetCandidates(player.hero).Select(star => star.GetType().Name));
        if (!shrine.choices.TryGetValue(player.guid, out var choices) || choices.Length == 0 ||
            choices.Any(choice => !allowed.Contains(choice.typeName)))
            shrine.PopulatePlayerChoices(player);
    }

    // 同时检查开局携带与局内奖励，避免重复星座；排除其他英雄的专属星座。
    internal static List<StarEffect> GetCandidates(Hero hero)
    {
        var carried = new HashSet<string>();
        foreach (StarType category in Enum.GetValues(typeof(StarType)))
        {
            if (hero.loadout == null) break;
            foreach (var star in hero.loadout.GetStarList(category))
                if (!string.IsNullOrEmpty(star.name) && star.level > 0) carried.Add(star.name);
        }
        foreach (Actor actor in NetworkedManagerBase<ActorManager>.instance.allActors)
            if (actor is StarEffect star && star.isActive && star.victim == hero) carried.Add(star.GetType().Name);

        var candidates = new List<StarEffect>();
        foreach (Type type in Dew.allStarTypes)
        {
            if (carried.Contains(type.Name)) continue;
            StarEffect star = DewResources.GetByType<StarEffect>(type);
            if (star == null || (star.heroType != null && star.heroType != hero.GetType())) continue;
            if (hero.loadout != null && star.IsIncompatibleWith(hero, hero.loadout)) continue;
            if (star.isMovementSkillType && hero.Skill.Movement == null) continue;
            candidates.Add(star);
        }
        return candidates;
    }

    // 每个领主仅记一次死亡；标记随领主存档保存。
    internal static void RewardLord(Monster lord)
    {
        if (!Mirror.NetworkServer.active) return;
        const string rewarded = "LucidDreamExpansion::AstronomyRewarded";
        if (lord.persistentData.ContainsKey(rewarded)) return;
        Vector3 position = Dew.GetGoodRewardPosition(lord.agentPosition, 3f);
        Shrine_Astronomy shrine = Dew.CreateActor<Shrine_Astronomy>(position, null);
        if (shrine == null || !shrine.isActive)
            throw new InvalidOperationException("星座奖励祭坛未成功生成");
        lord.persistentData[rewarded] = "1";
        Debug.Log($"[LucidDreamExpansion] 天文大师：已为 {lord.GetType().Name} 创建星座奖励祭坛，位置 {position}。");
    }

    // 由房主验证选择、使用次数、距离和当前候选，奖励仅保存在本局状态效果中。
    internal static void Choose(Shrine_Astronomy shrine, int index, Mirror.NetworkConnectionToClient sender)
    {
        if (!Mirror.NetworkServer.active) return;
        DewPlayer player = sender?.GetPlayer();
        Hero hero = player?.hero;
        if (hero == null || !shrine.choices.TryGetValue(player.guid, out var choices) ||
            index < 0 || index >= choices.Length || shrine.isLocked || !shrine.CanInteract(hero) ||
            shrine.CanAfford(hero) != AffordType.Yes || Vector3.Distance(hero.position, shrine.position) > shrine.focusDistance + 2f)
            return;
        StarEffect selected = GetCandidates(hero).FirstOrDefault(s => s.GetType().Name == choices[index].typeName);
        if (selected == null)
        {
            shrine.PopulatePlayerChoices(player);
            return;
        }
        shrine.DoPostUseRoutines(hero);
        StarEffect awarded = hero.CreateStatusEffect(selected, hero, new CastInfo(hero), effect =>
        {
            effect.skillLevel = selected.maxStarLevel;
            effect.strength = 1f;
        });
        if (awarded != null && awarded.isActive) RunStateController.RememberStar(hero, awarded);
    }
}
