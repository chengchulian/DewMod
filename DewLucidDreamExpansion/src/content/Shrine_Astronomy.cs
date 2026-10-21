using System.Collections.Generic;
using DewLucidDreamExpansion.controller;
using UnityEngine;

namespace DewLucidDreamExpansion.content;

[SaveActor]
public sealed class Shrine_Astronomy : ChoiceShrine, IRewardActor
{
    // 候选不足三个时保留实际数量，每名玩家的选项由原版同步字典复制。
    protected override void OnPopulateChoices(DewPlayer player)
    {
        if (player.hero == null)
        {
            choices[player.guid] = new ChoiceShrineItem[0];
            return;
        }
        List<StarEffect> candidates = AstronomyController.GetCandidates(player.hero);
        var result = new List<ChoiceShrineItem>();
        while (result.Count < 3 && candidates.Count > 0)
        {
            int index = Random.Range(0, candidates.Count);
            StarEffect star = candidates[index];
            candidates.RemoveAt(index);
            result.Add(new ChoiceShrineItem { typeName = star.GetType().Name, level = star.maxStarLevel });
        }
        choices[player.guid] = result.ToArray();
    }

    protected override bool OnUse(Entity entity)
    {
        AstronomyController.RefreshPlayerChoices(this, entity.owner);
        return base.OnUse(entity);
    }

    public override bool CanInteract(Entity entity)
    {
        return base.CanInteract(entity) && entity.owner != null &&
               choices.TryGetValue(entity.owner.guid, out var items) && items.Length > 0;
    }
}
