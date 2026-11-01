using System;
using System.Globalization;
using System.Linq;
using Mirror;
using UnityEngine;

namespace DewLucidDreamExpansion.controller;

internal static class RunStateController
{
    private const string StarKeyPrefix = "LucidDreamExpansion::AstronomyStar::";

    // 本局奖励单独记在英雄持久数据中，不写入玩家的永久星座配置。
    internal static void RememberStar(Hero hero, StarEffect star)
    {
        hero.persistentData[StarKeyPrefix + star.GetType().Name] =
            star.skillLevel.ToString(CultureInfo.InvariantCulture) + "|" +
            star.strength.ToString("R", CultureInfo.InvariantCulture);
    }

    // 等原生状态效果读档完成再补缺，保留原生恢复的星座及其内部状态。
    internal static void RestoreStars(Hero hero)
    {
        var actors = NetworkedManagerBase<ActorManager>.instance;
        if (!NetworkServer.active || hero == null || !hero.isActive || actors == null) return;
        foreach (var entry in hero.persistentData.Where(p => p.Key.StartsWith(StarKeyPrefix, StringComparison.Ordinal)).ToArray())
        {
            string name = entry.Key.Substring(StarKeyPrefix.Length);
            Type type = Dew.allStarTypes.FirstOrDefault(t => t.Name == name);
            string[] values = entry.Value?.Split('|');
            if (type == null || values == null || values.Length != 2 ||
                !int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int level) || level <= 0 ||
                !float.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float strength) ||
                float.IsNaN(strength) || float.IsInfinity(strength) || strength < 0f) continue;
            if (actors.allActors.OfType<StarEffect>().Any(s => s.isActive && s.victim == hero && s.GetType() == type)) continue;
            StarEffect prefab = DewResources.GetByType<StarEffect>(type);
            if (prefab == null || (prefab.heroType != null && prefab.heroType != hero.GetType())) continue;
            hero.CreateStatusEffect(prefab, hero, new CastInfo(hero), effect =>
            {
                effect.skillLevel = Math.Min(level, prefab.maxStarLevel);
                effect.strength = strength;
            });
        }
    }

    // 原生继续游戏跳过开局创建流程；补齐缺失实体，让原生 HUD 和晚加入客户端同步获得图标。
    internal static void RestoreDreams()
    {
        var actors = NetworkedManagerBase<ActorManager>.instance;
        var game = NetworkedManagerBase<GameManager>.instance;
        if (!NetworkServer.active || actors == null || game == null) return;
        foreach (Type type in DreamRegistry.Dreams)
        {
            if (!game.GetLucidDreams().Contains(type.Name) ||
                actors.allActors.Any(a => a.isActive && a.GetType() == type)) continue;
            LucidDream prefab = DewResources.GetByType<LucidDream>(type);
            Dew.CreateActor(prefab, Vector3.zero, null);
        }
    }

    // ApplyGameData 的完成回调在全部批量创建和房间恢复之后执行。
    internal static void RestoreRun()
    {
        RestoreDreams();
        foreach (DewPlayer player in DewPlayer.gamePlayers)
            RestoreStars(player.hero);
    }
}
