using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DewDifficultyCustomizeMod.config;
using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DewDifficultyCustomizeMod.patch;

[HarmonyPatch(typeof(RoomMonsters))]
public static class RoomMonsters_Patch
{
    [HarmonyPatch(nameof(RoomMonsters.SpawnMonsters))]
    [HarmonyPrefix]
    public static bool SpawnMonsters_Prefix(RoomMonsters __instance, SpawnMonsterSettings settings)
    {
	    if (settings.random == null)
	    {
		    settings.random = new DewRandom(__instance.room.GetRoomRandom(-8162).NextUInt32());
	    }
        // 替代原始方法逻辑，启动你自己的协程
        __instance.ongoingSpawns.Add(settings, __instance.StartCoroutine(RoomMonstersCustomImpl.SpawnMonstersRoutine(__instance, settings)));

        return false; // 阻止原方法执行
    }
}


public static class RoomMonstersCustomImpl
{
    
    private static readonly MethodInfo spawnMonsterImpMethod = AccessTools.Method(
        typeof(RoomMonsters),
        "SpawnMonsterImp",
        new[] { typeof(SpawnMonsterSettings), typeof(RoomMonsters.MonsterSpawnData), typeof(Entity), typeof(float) }
    );
    private static readonly MethodInfo waitForPopulationRoutineMethod = AccessTools.Method(
        typeof(RoomMonsters),
        "WaitForPopulationRoutine",
        new[] { typeof(MonsterSpawnRule), typeof(RoomSection), typeof(float), typeof(RefValue<bool>) }
    );
    private static readonly FieldInfo isCutsceneSkippedField = AccessTools.Field(typeof(SpawnMonsterSettings), "isCutsceneSkipped");

    
    static RoomMonstersCustomImpl()
    {
        if (spawnMonsterImpMethod == null || waitForPopulationRoutineMethod == null || isCutsceneSkippedField == null)
        {
            Debug.LogError("[RoomMonstersCustomImpl] Failed to bind internal methods/fields. Check game version or Harmony patching.");
        }
    }

	public static IEnumerator SpawnMonstersRoutine(RoomMonsters __instance ,SpawnMonsterSettings s)
	{
		if (__instance.ongoingSpawns.ContainsKey(s))
		{
			throw new InvalidOperationException();
		}
		yield return null;
		while (NetworkedManagerBase<ZoneManager>.instance.isInRoomTransition)
		{
			yield return null;
		}
		RoomMonsters.MonsterSpawnData monsterSpawnData = s.monsterSpawnData;
		MonsterSpawnRule rule = s.rule;
		float waitStartTime = Time.time;
		float timeToWait = Random.Range(rule.initialDelay.x, rule.initialDelay.y) * s.initDelayMultiplier + s.initDelayFlat;
		yield return new WaitWhile(() => Time.time - waitStartTime < timeToWait && !(bool)isCutsceneSkippedField.GetValue(s));
		int waves = Random.Range(rule.wavesMin, rule.wavesMax + 1);
		waves = NetworkedManagerBase<GameManager>.instance.GetAdjustedMonsterWaves(waves);
		float hunterChance = __instance.addedHunterChance;
		float[,] matrix = NetworkedManagerBase<GameManager>.instance.gss.mirageSkinChanceByZoneIndexAndPlayerCount;
		float mirageChance = matrix[Mathf.Clamp(NetworkedManagerBase<ZoneManager>.instance.currentZoneIndex, 0, matrix.GetLength(0) - 1), Mathf.Clamp(Dew.GetAliveHeroCount() - 1, 0, matrix.GetLength(1) - 1)];
		if (mirageChance > 0.0001f)
		{
			mirageChance += __instance.addedMirageChance * AttrCustomizeResources.Config.monsterMirageChanceMultiple;
		}
		if (rule.isBossSpawn)
		{
			int zoneAddCount = (NetworkedManagerBase<ZoneManager>.instance.currentZoneIndex) * AttrCustomizeResources.Config.bossCountAddByZone;
			int loopAddCount = (NetworkedManagerBase<ZoneManager>.instance.loopIndex) * AttrCustomizeResources.Config.bossCountAddByLoop;
			int bossCount = AttrCustomizeResources.Config.bossCount;
			waves = bossCount + loopAddCount + zoneAddCount;
			hunterChance = AttrCustomizeResources.Config.bossHunterChance;
			mirageChance = AttrCustomizeResources.Config.bossMirageChance;
		}
		int waveIndex = 0;
		while (true)
		{
			float waveStartTime;
			float waveTimeout;
			float nextWaveThreshold;
			if (waveIndex < waves)
			{
				waveStartTime = Time.time;
				waveTimeout = Random.Range(rule.waveTimeoutMin, rule.waveTimeoutMax);
				if (rule.isBossSpawn && AttrCustomizeResources.Config.enableBossSpawnAllOnce)
				{
					waveTimeout = 0f;
				}
				float population = Random.Range(rule.populationPerWave.x, rule.populationPerWave.y);
				population = NetworkedManagerBase<GameManager>.instance.GetAdjustedMonsterSpawnPopulation(population, s.ignoreTurnPopMultiplier, s.ignoreCoopPopMultiplier) * s.spawnPopulationMultiplier * __instance.spawnedPopMultiplier;
				nextWaveThreshold = Random.Range(rule.nextWavePopulationThreshold.x, rule.nextWavePopulationThreshold.y);
				nextWaveThreshold = NetworkedManagerBase<GameManager>.instance.GetAdjustedMonsterSpawnPopulation(nextWaveThreshold, s.ignoreTurnPopMultiplier, s.ignoreCoopPopMultiplier);
				nextWaveThreshold = Mathf.Clamp(nextWaveThreshold, 0.0001f, population - 0.1f);
				IEnumerator<Monster> enumerator = rule.pool.GetMonsters(int.MaxValue);
				enumerator.MoveNext();
				float spawnedPop = 0f;
				bool isFirstSpawn = true;
				while (true)
				{
					if (isFirstSpawn)
					{
						isFirstSpawn = false;
					}
					else
					{
						yield return new WaitForSeconds(Random.Range(0.1f, 0.5f));
					}
					float popCost = enumerator.Current.populationCost;
					if (!rule.isBossSpawn)
					{
						RefValue<bool> didFail = new RefValue<bool>(v: false);
						IEnumerator waitRoutine = (IEnumerator)waitForPopulationRoutineMethod.Invoke(__instance, new object[]
						{
							rule, s.section, popCost, didFail
						});
						yield return waitRoutine;

						if ((bool)didFail)
						{
							break;
						}
					}
					if (s.earlyFinishCondition != null && s.earlyFinishCondition())
					{
						goto end_IL_067a;
					}

					Entity spawned = (Entity)spawnMonsterImpMethod.Invoke(__instance,
						new object[] { s, monsterSpawnData, enumerator.Current, popCost });
					if (spawned != null)
					{
						spawnedPop += popCost;
						if (Random.value < hunterChance && !spawned.Status.HasStatusEffect<Se_HunterBuff>())
						{
							spawned.CreateStatusEffect<Se_HunterBuff>(spawned, new CastInfo(spawned));
						}
						if (Random.value < mirageChance && spawned is Monster { type: not Monster.MonsterType.Lesser } && !spawned.Status.HasStatusEffect<Se_MirageSkin_Delusion>())
						{
							spawned.CreateStatusEffect<Se_MirageSkin_Delusion>(spawned, new CastInfo(spawned));
						}
					}
					if (!rule.isBossSpawn && !(spawnedPop > population))
					{
						enumerator.MoveNext();
						continue;
					}
					goto IL_063a;
				}
				if (rule.onOverPopulation == OverpopulationBehavior.Stall)
				{
					Debug.Log(rule.name + " timed out due to overpopulation");
				}
				else
				{
					Debug.Log(rule.name + " canceled due to overpopulation");
				}
			}
			while (monsterSpawnData.remainingPopulation > 0.05f)
			{
				yield return new WaitForSeconds(0.25f);
			}
			break;
			IL_063a:
			while (monsterSpawnData.remainingPopulation > nextWaveThreshold && Time.time - waveStartTime < waveTimeout)
			{
				yield return new WaitForSeconds(0.25f);
			}
			waveIndex++;
			continue;
			end_IL_067a:
			break;
		}
		s.onFinish?.Invoke();
		__instance.ongoingSpawns.Remove(s);
	}
}