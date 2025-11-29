using System;
using System.Collections.Generic;
using System.Linq;
using DewHeroSkillJonas.patch;
using Mirror;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DewHeroSkillJonas.util;

public static class PropEnt_Merchant_HeroSkill
{
    private static readonly Lazy<Dictionary<HeroSkillLocation, List<string>>> _skillsByTypeLazy =
        new(() =>
        {
            var dict = new Dictionary<HeroSkillLocation, List<string>>
            {
                { HeroSkillLocation.Q, [] },
                { HeroSkillLocation.R, [] },
                { HeroSkillLocation.Identity, [] },
                { HeroSkillLocation.Movement, [] }
            };

            foreach (var keyValuePair in DewLocalization.data.skills)
            {
                var key = keyValuePair.Key;
                var byShortTypeName = DewResources.GetByShortTypeName("St_" + key);
                if (byShortTypeName is not SkillTrigger skillTrigger) continue;

                if (skillTrigger.rarity is Rarity.Character or Rarity.Identity)
                {
                    var strings = key.Split("_");
                    if (strings.Length <= 1) continue;
                    var heroSkillLocation = strings[0];
                    switch (heroSkillLocation)
                    {
                        case "Q":
                            dict[HeroSkillLocation.Q].Add(skillTrigger.name);
                            break;
                        case "R":
                            dict[HeroSkillLocation.R].Add(skillTrigger.name);
                            break;
                        case "D":
                            dict[HeroSkillLocation.Identity].Add(skillTrigger.name);
                            break;
                        case "M":
                            dict[HeroSkillLocation.Movement].Add(skillTrigger.name);
                            break;
                        case "QR":
                            dict[HeroSkillLocation.Q].Add(skillTrigger.name);
                            dict[HeroSkillLocation.R].Add(skillTrigger.name);
                            break;
                    }
                }
            }

            return dict;
        });

    public static Dictionary<HeroSkillLocation, List<string>> SkillsByType => _skillsByTypeLazy.Value;


    public static void ZoneManagerOnStart()
    {
        if (!NetworkServer.active)
        {
            return;
        }

        ZoneManager.instance.ClientEvent_OnRoomLoaded += _ =>
        {
            if (!DewHeroSkillJonas.Instance.Config.Enable)
                return;

            var room = SingletonDewNetworkBehaviour<Room>.instance;

            // 判断 GiftMerchant 是否存在
            bool hasGiftMerchant = room.modifiers.modifierInstances
                .Any(m => m.modData.type == "RoomMod_GiftMerchant");

            if (!hasGiftMerchant)
                return;

            // 获取房间内可用的位置
            if (!room.props.TryGetGoodNodePosition(out var position))
            {
                Debug.LogWarning("未找到合适的位置来生成技能商人！");
                return;
            }

            Debug.Log("Spawning hero skill merchant at " + position);

            var merchantResource = DewResources.GetByType<PropEnt_Merchant_Jonas>();

            // 生成商人
            Dew.SpawnEntity(
                merchantResource,
                position,
                Quaternion.identity,
                NetworkedManagerBase<ActorManager>.instance.serverActor,
                DewPlayer.creep,
                NetworkedManagerBase<GameManager>.instance.ambientLevel,
                beforeSpawn: actor =>
                {
                    actor.gameObject.AddComponent<PropEnt_Merchant_Base_Patch.HeroSkillMarker>();
                });
        };
    }

    public static void OnPopulateMerchandises(DewPlayer player, PropEnt_Merchant_Jonas jonas)
    {
        MerchandiseData[] baseSkills = GetBaseSkills(jonas);

        MerchandiseData[] baseGems = GetBaseSkills(jonas);


        MerchandiseData[] arr = new MerchandiseData[jonas.skillTypeCount + jonas.gemTypeCount];
        Array.Copy(baseSkills, 0, arr, 0, baseSkills.Length);
        Array.Copy(baseGems, 0, arr, baseSkills.Length, baseGems.Length);


        // MerchandiseData[] arr = new MerchandiseData[jonas.skillTypeCount + jonas.gemTypeCount + player.shopAddedItems * 2];

        // Array.Copy(baseSkills, 0, arr, 0, baseSkills.Length);
        // int num = jonas.skillTypeCount;
        // int to = num + player.shopAddedItems;
        // for (int i = num; i < to; i++)
        // {
        //     arr[i] = GetSkill(jonas);
        // }
        //
        // Array.Copy(baseGems, 0, arr, jonas.skillTypeCount + player.shopAddedItems, baseGems.Length);
        // int num2 = jonas.skillTypeCount + player.shopAddedItems + jonas.gemTypeCount;
        // to = num2 + player.shopAddedItems;
        // for (int j = num2; j < to; j++)
        // {
        //     arr[j] = GetSkill(jonas);
        // }

        UpdateItemPrices(arr);
        jonas.merchandises[player.guid] = arr;
    }

    private static void UpdateItemPrices(MerchandiseData[] arr)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            MerchandiseData temp = arr[i];
            if (temp.type == MerchandiseType.Gem)
            {
                Gem gem = DewResources.GetByShortTypeName<Gem>(temp.itemName);
                temp.price = Cost.Gold(Gem.GetBuyGold(gem.rarity, temp.level));
            }
            else if (temp.type == MerchandiseType.Skill)
            {
                SkillTrigger skill = DewResources.GetByShortTypeName<SkillTrigger>(temp.itemName);
                temp.price = Cost.Gold(SkillTrigger.GetBuyGold(skill.rarity, temp.level));
            }
            else
            {
                temp.price = Cost.Gold(99999);
            }

            arr[i] = temp;
        }
    }


    private static MerchandiseData[] GetBaseSkills(PropEnt_Merchant_Jonas jonas)
    {
        Debug.Log("GetBaseSkills");
        MerchandiseData[] baseSkills = new MerchandiseData[jonas.skillTypeCount];
        for (int i = 0; i < jonas.skillTypeCount; i++)
        {
            baseSkills[i] = GetSkill(jonas);
        }

        Debug.Log("GetBaseSkills end");
        return baseSkills;
    }

    private static MerchandiseData GetSkill(PropEnt_Merchant_Jonas jonas)
    {
        Debug.Log("GetSkill");

        HeroSkillLocation skillType = SelectSkillType();
        SelectSkillAndLevel(skillType, out var skill, out var skillLevel);
        Debug.Log("GetSkill end");
        return new MerchandiseData
        {
            type = MerchandiseType.Skill,
            itemName = skill.GetType().Name,
            level = skillLevel,
            count = Mathf.Max(1,
                Mathf.RoundToInt(Random.Range(jonas.skillQuantity.x, jonas.skillQuantity.y)))
        };
    }

    public static void SelectSkillAndLevel(HeroSkillLocation? skillType, out SkillTrigger skill, out int level)
    {
        Debug.Log("SelectSkillAndLevel");
        if (!skillType.HasValue)
        {
            skillType = SelectSkillType();
        }

        List<string> pool = SkillsByType[skillType.Value];
        skill = DewResources.GetByShortTypeName<SkillTrigger>(pool[Random.Range(0, pool.Count)]);
        level = SelectSkillLevel(skillType.Value);
        Debug.Log("SelectSkillAndLevel end");
    }

    public static int SelectSkillLevel(HeroSkillLocation skillType)
    {
        Rarity rarity = skillType switch
        {
            HeroSkillLocation.Q => Rarity.Common,
            HeroSkillLocation.R => Rarity.Rare,
            HeroSkillLocation.Identity => Rarity.Epic,
            HeroSkillLocation.Movement => Rarity.Legendary,
            _ => Rarity.Common
        };

        var lootManager = LootManager.instance;

        float a = lootManager.skillLevelMinByZoneIndex.Get(rarity)
            .Evaluate(NetworkedManagerBase<ZoneManager>.instance.currentZoneIndex);
        float max = lootManager.skillLevelMaxByZoneIndex.Get(rarity)
            .Evaluate(NetworkedManagerBase<ZoneManager>.instance.currentZoneIndex);
        return Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Lerp(a, max, lootManager.skillLevelRandomCurve.Evaluate(Random.value))),
            1, 100);
    }

    public static HeroSkillLocation SelectSkillType(bool isHigh = false)
    {
        Debug.Log("SelectSkillType");
        return SelectSkillType(isHigh
            ? LootManager.instance.skillRarityChanceHigh
            : LootManager.instance.skillRarityChance);
    }

    public static HeroSkillLocation SelectSkillType(PerRarityData<float> chances)
    {
        float val = Random.value;
        HeroSkillLocation location = HeroSkillLocation.Q;
        if (val < chances.legendary)
        {
            location = HeroSkillLocation.Movement;
        }
        else if (val < chances.legendary + chances.epic)
        {
            location = HeroSkillLocation.Identity;
        }
        else if (val < chances.legendary + chances.epic + chances.rare)
        {
            location = HeroSkillLocation.R;
        }

        return location;
    }

    public static void Test()
    {
        foreach (var keyValuePair in SkillsByType)
        {
            Debug.Log(keyValuePair.Key + " " + keyValuePair.Value.Count);
        }
    }
}