using System.Collections;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace DewPrimusHand.patch
{
    [HarmonyPatch(typeof(RoomMonsters))]
    public class RoomMonsters_Patch
    {
        private static readonly ConditionalWeakTable<SpawnMonsterSettings, object> ExtraSpawnMarker = new();

        [HarmonyPostfix]
        [HarmonyPatch(nameof(RoomMonsters.SpawnMonsters))]
        public static void SpawnMonsters_Postfix(RoomMonsters __instance, SpawnMonsterSettings settings)
        {
            if (!settings.rule.isBossSpawn)
            { 
                return;
            }

            if (!NetworkedManagerBase<GameManager>.instance.isServer)
                return;

            // 检查BossSpawnAllOnce标志
            if (DewPrimusHand.Instance.Config.BossSpawnAllOnce)
            {
                // 一次性生成所有 Boss
                if (ExtraSpawnMarker.TryGetValue(settings, out _))
                    return;

                __instance.StartCoroutine(
                    GenerateAllBosses(__instance, settings)
                );
            }
            else
            {
                // 按原逻辑生成 Boss，并且改为随机延时生成
                if (ExtraSpawnMarker.TryGetValue(settings, out _))
                    return;

                __instance.StartCoroutine(
                    ExtraSpawnAfterComplete(__instance, settings)
                );
            }
        }

        private static IEnumerator GenerateAllBosses(
            RoomMonsters room,
            SpawnMonsterSettings origin
        )
        {
            // 等待当前怪物生成完成
            yield return new WaitUntil(() => !room.ongoingSpawns.ContainsKey(origin));

            // 计算需要生成的额外Boss数量
            int extra = CalculateExtraBossCount();

            // 克隆并生成所有Boss
            for (int i = 0; i < extra; i++)
            {
                var clone = CloneSettings(origin);
                ExtraSpawnMarker.Add(clone, null);

                room.SpawnMonsters(clone);

                // 在怪物生成后处理猎手化和MirageSkin状态
                ApplyAfterSpawn(clone);
            }

            // 确保所有生成的怪物都完成
            foreach (var clone in ExtraSpawnMarker)
            {
                yield return new WaitUntil(() => !room.ongoingSpawns.ContainsKey(clone.Key));
            }
        }

        private static IEnumerator ExtraSpawnAfterComplete(
            RoomMonsters room,
            SpawnMonsterSettings origin
        )
        {
            int extra = CalculateExtraBossCount();

            // 生成所有额外的 Boss，每个 Boss 的生成之间有 2 到 5 秒的随机延时
            for (int i = 0; i < extra; i++)
            {
                // 等待随机延时（2 到 5 秒）
                float delay = Random.Range(2f, 5f);
                yield return new WaitForSeconds(delay);

                var clone = CloneSettings(origin);
                ExtraSpawnMarker.Add(clone, null);

                room.SpawnMonsters(clone);

                // 在怪物生成后处理猎手化和MirageSkin状态
                ApplyAfterSpawn(clone);
            }
        }

        private static void ApplyAfterSpawn(SpawnMonsterSettings clone)
        {
            // 使用 afterSpawn 回调来执行后续操作
            clone.afterSpawn += spawnedEntity =>
            {
                // 检查是否触发猎手化几率
                if (DewPrimusHand.Instance.Config.BossHunterChance > 0)
                {
                    if (DewRandom.instance.NextFloat(0,1) < DewPrimusHand.Instance.Config.BossHunterChance)
                    {
                        // 为生成的 Boss 添加猎手化状态效果
                        if (spawnedEntity != null && !spawnedEntity.Status.HasStatusEffect<Se_HunterBuff>())
                        {
                            spawnedEntity.CreateStatusEffect<Se_HunterBuff>(spawnedEntity, new CastInfo(spawnedEntity));
                        }
                    }
                }

                // 检查是否触发MirageSkin几率
                if (DewPrimusHand.Instance.Config.BossMirageChance > 0)
                {
                    if (DewRandom.instance.NextFloat(0,1) < DewPrimusHand.Instance.Config.BossMirageChance)
                    {
                        // 为生成的 Boss 添加MirageSkin状态效果
                        if (spawnedEntity != null && !spawnedEntity.Status.HasStatusEffect<MirageSkinEffect>())
                        {
                            var currentZonePool = GameMod_MirageSkin.instance.currentZonePool;
                            spawnedEntity.CreateStatusEffect(currentZonePool[Random.Range(0, currentZonePool.Count)].asset, spawnedEntity, new CastInfo(spawnedEntity));
                        }
                    }
                }
            };
        }

        private static SpawnMonsterSettings CloneSettings(SpawnMonsterSettings origin)
        {
            var clone = origin.Clone();

            // 关键：随机数必须独立
            if (clone.random != null)
            {
                clone.random = new DewRandom(clone.random.NextUInt32());
            }

            return clone;
        }

        private static int CalculateExtraBossCount()
        {
            var zone = NetworkedManagerBase<ZoneManager>.instance.currentZoneIndex;
            var loop = NetworkedManagerBase<ZoneManager>.instance.loopIndex;

            var baseCount = DewPrimusHand.Instance.Config.BossCount;
            var zoneAdd = zone * DewPrimusHand.Instance.Config.BossCountAddByZone;
            var loopAdd = loop * DewPrimusHand.Instance.Config.BossCountAddByLoop;

            var total = baseCount + zoneAdd + loopAdd;

            // 原 SpawnMonsters 已经刷过 1 次
            return Mathf.Max(0, total - 1);
        }
    }
}
