using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace DewPrimusHand.patch
{
    [HarmonyPatch(typeof(RoomMonsters))]
    public class RoomMonsters_Patch
    {
        private const float InkDarkMoonEclipseStuckSeconds = 45f;
        private const float InkDarkMoonEclipseDeadSequenceSeconds = 1f;

        private static readonly ConditionalWeakTable<SpawnMonsterSettings, object> ExtraSpawnMarker = new();
        private static readonly Dictionary<Ai_Mon_Ink_BossDarkMoon_Eclipse, float> InkDarkMoonEclipseStartTimes = new();
        private static readonly Dictionary<Ai_Mon_Ink_BossDarkMoon_Eclipse, float> InkDarkMoonEclipseDeadSequenceStartTimes = new();
        private static readonly List<Ai_Mon_Ink_BossDarkMoon_Eclipse> StaleInkDarkMoonEclipses = new();
        private static readonly List<Ai_Mon_Ink_BossDarkMoon_Eclipse> InkDarkMoonEclipsesToRecover = new();
        private static bool _isWatchingInkDarkMoonEclipse;

        [HarmonyPrefix]
        [HarmonyPatch(nameof(RoomMonsters.SpawnMonsters))]
        public static void SpawnMonsters_Prefix(RoomMonsters __instance, SpawnMonsterSettings settings)
        {
            if (settings?.rule == null || !settings.rule.isBossSpawn)
                return;

            if (!NetworkedManagerBase<GameManager>.instance.isServer)
                return;

            EnsureInkDarkMoonEclipseFailsafe();

            if (ExtraSpawnMarker.TryGetValue(settings, out _))
                return;

            int extra = CalculateExtraBossCount();
            if (extra <= 0)
                return;

            var state = new ExtraBossQueueState
            {
                Room = __instance,
                Origin = settings,
                OriginalAfterSpawn = settings.afterSpawn,
                OriginalOnFinish = settings.onFinish,
                Remaining = extra,
                PendingOriginal = 1
            };

            settings.afterSpawn += spawnedEntity =>
            {
                if (!IsCountedBoss(spawnedEntity))
                    return;

                state.PendingOriginal = 0;
            };
            settings.onFinish = () =>
            {
                state.PendingOriginal = 0;
                state.OriginalFinished = true;
                TryFinishOriginalSpawn(state);
            };

            __instance.StartCoroutine(SpawnExtraBosses(state));
        }

        private static IEnumerator SpawnExtraBosses(ExtraBossQueueState state)
        {
            while (state.Remaining > 0)
            {
                if (!HasOpenBossSlot(state))
                {
                    yield return new WaitForSeconds(0.25f);
                    continue;
                }

                SpawnExtraBoss(state);
                state.Remaining--;

                if (!DewPrimusHand.Instance.Config.BossSpawnAllOnce && state.Remaining > 0)
                {
                    yield return new WaitForSeconds(UnityEngine.Random.Range(2f, 5f));
                }
                else
                {
                    yield return null;
                }
            }

            TryFinishOriginalSpawn(state);
        }

        private static void SpawnExtraBoss(ExtraBossQueueState state)
        {
            var clone = CloneSettings(state);
            ExtraSpawnMarker.Add(clone, new object());

            state.PendingExtras++;
            state.Room.SpawnMonsters(clone);
        }

        private static SpawnMonsterSettings CloneSettings(ExtraBossQueueState state)
        {
            var clone = state.Origin.Clone();

            if (clone.random != null)
            {
                clone.random = new DewRandom(clone.random.NextUInt32());
            }

            clone.monsterSpawnData = state.Origin.monsterSpawnData;
            clone.afterSpawn = state.OriginalAfterSpawn;
            clone.initDelayFlat = 0f;
            clone.initDelayMultiplier = 0f;

            bool didSpawn = false;
            clone.afterSpawn += spawnedEntity =>
            {
                if (!IsCountedBoss(spawnedEntity))
                    return;

                didSpawn = true;
                state.PendingExtras = Mathf.Max(0, state.PendingExtras - 1);
                state.ActiveExtras++;
                ApplyAfterSpawn(spawnedEntity);

                spawnedEntity.EntityEvent_OnDeath += _ =>
                {
                    state.ActiveExtras = Mathf.Max(0, state.ActiveExtras - 1);
                    TryFinishOriginalSpawn(state);
                };
            };
            clone.onFinish = () =>
            {
                if (!didSpawn)
                {
                    state.PendingExtras = Mathf.Max(0, state.PendingExtras - 1);
                }

                TryFinishOriginalSpawn(state);
            };

            return clone;
        }

        private static void TryFinishOriginalSpawn(ExtraBossQueueState state)
        {
            if (!state.OriginalFinished || state.InvokedOriginalOnFinish)
                return;

            if (state.Remaining > 0 || state.PendingExtras > 0 || state.ActiveExtras > 0)
            {
                state.Room.StartCoroutine(KeepBossEncounterRunning(state));
                return;
            }

            state.InvokedOriginalOnFinish = true;
            state.OriginalOnFinish?.Invoke();
        }

        private static IEnumerator KeepBossEncounterRunning(ExtraBossQueueState state)
        {
            yield return null;

            if (!state.InvokedOriginalOnFinish)
            {
                NetworkedManagerBase<GameManager>.instance.isGameTimePausedByGame = false;
            }
        }

        private static bool HasOpenBossSlot(ExtraBossQueueState state)
        {
            return CountAliveBosses() + state.PendingOriginal + state.PendingExtras < GetMaxBossCountInRoom();
        }

        private static int CountAliveBosses()
        {
            int count = 0;
            foreach (var entity in NetworkedManagerBase<ActorManager>.instance.allEntities)
            {
                if (IsCountedBoss(entity))
                {
                    count++;
                }
            }

            return count;
        }

        private static int GetMaxBossCountInRoom()
        {
            return Mathf.Max(1, DewPrimusHand.Instance.Config.BossCountInRoom - 1);
        }

        private static bool IsCountedBoss(Entity entity)
        {
            if (entity.IsNullInactiveDeadOrKnockedOut())
                return false;

            if (entity is BossMonster)
                return true;

            if (entity is Monster monster)
                return monster.type == Monster.MonsterType.Boss || monster.type == Monster.MonsterType.MiniBoss;

            return false;
        }

        private static void ApplyAfterSpawn(Entity spawnedEntity)
        {
            if (spawnedEntity == null)
                return;

            if (DewPrimusHand.Instance.Config.BossHunterChance > 0)
            {
                if (DewRandom.instance.NextFloat(0, 1) < DewPrimusHand.Instance.Config.BossHunterChance)
                {
                    if (!spawnedEntity.Status.HasStatusEffect<Se_HunterBuff>())
                    {
                        spawnedEntity.CreateStatusEffect<Se_HunterBuff>(spawnedEntity, new CastInfo(spawnedEntity));
                    }
                }
            }

            if (DewPrimusHand.Instance.Config.BossMirageChance > 0)
            {
                if (DewRandom.instance.NextFloat(0, 1) < DewPrimusHand.Instance.Config.BossMirageChance)
                {
                    if (!spawnedEntity.Status.HasStatusEffect<MirageSkinEffect>())
                    {
                        var currentZonePool = GameMod_MirageSkin.instance.currentZonePool;
                        spawnedEntity.CreateStatusEffect(currentZonePool[UnityEngine.Random.Range(0, currentZonePool.Count)].asset, spawnedEntity, new CastInfo(spawnedEntity));
                    }
                }
            }
        }

        private static int CalculateExtraBossCount()
        {
            return Mathf.Max(0, CalculateBossCount() - 1);
        }

        private static int CalculateBossCount()
        {
            var zone = NetworkedManagerBase<ZoneManager>.instance.currentZoneIndex;
            var loop = NetworkedManagerBase<ZoneManager>.instance.loopIndex;

            var baseCount = DewPrimusHand.Instance.Config.BossCount;
            var zoneAdd = zone * DewPrimusHand.Instance.Config.BossCountAddByZone;
            var loopAdd = loop * DewPrimusHand.Instance.Config.BossCountAddByLoop;

            return Mathf.Max(1, baseCount + zoneAdd + loopAdd);
        }

        private static void EnsureInkDarkMoonEclipseFailsafe()
        {
            if (_isWatchingInkDarkMoonEclipse)
                return;

            var runner = DewPrimusHand.Instance;
            if (runner == null)
                return;

            _isWatchingInkDarkMoonEclipse = true;
            runner.StartCoroutine(WatchInkDarkMoonEclipseFailsafe());
        }

        private static IEnumerator WatchInkDarkMoonEclipseFailsafe()
        {
            while (NetworkedManagerBase<GameManager>.instance != null &&
                   NetworkedManagerBase<GameManager>.instance.isServer)
            {
                RecoverStuckInkDarkMoonEclipses();
                yield return new WaitForSeconds(0.25f);
            }

            _isWatchingInkDarkMoonEclipse = false;
            CleanupInkDarkMoonEclipseTracking();
        }

        private static void RecoverStuckInkDarkMoonEclipses()
        {
            CleanupInkDarkMoonEclipseTracking();
            InkDarkMoonEclipsesToRecover.Clear();

            foreach (var actor in NetworkedManagerBase<ActorManager>.instance.allActors)
            {
                if (actor is not Ai_Mon_Ink_BossDarkMoon_Eclipse eclipse || !eclipse.isActive)
                    continue;

                if (!InkDarkMoonEclipseStartTimes.TryGetValue(eclipse, out var startTime))
                {
                    InkDarkMoonEclipseStartTimes[eclipse] = Time.time;
                    startTime = Time.time;
                }

                if (!eclipse.hasOngoingSequences)
                {
                    if (!InkDarkMoonEclipseDeadSequenceStartTimes.TryGetValue(eclipse, out var deadSequenceStartTime))
                    {
                        InkDarkMoonEclipseDeadSequenceStartTimes[eclipse] = Time.time;
                        continue;
                    }

                    if (Time.time - deadSequenceStartTime >= InkDarkMoonEclipseDeadSequenceSeconds)
                    {
                        InkDarkMoonEclipsesToRecover.Add(eclipse);
                    }

                    continue;
                }

                InkDarkMoonEclipseDeadSequenceStartTimes.Remove(eclipse);
                if (Time.time - startTime >= InkDarkMoonEclipseStuckSeconds)
                {
                    InkDarkMoonEclipsesToRecover.Add(eclipse);
                }
            }

            foreach (var eclipse in InkDarkMoonEclipsesToRecover)
            {
                RecoverInkDarkMoonEclipse(eclipse);
            }

            InkDarkMoonEclipsesToRecover.Clear();
        }

        private static void CleanupInkDarkMoonEclipseTracking()
        {
            StaleInkDarkMoonEclipses.Clear();

            foreach (var pair in InkDarkMoonEclipseStartTimes)
            {
                var eclipse = pair.Key;
                if (eclipse == null || !eclipse.isActive)
                {
                    StaleInkDarkMoonEclipses.Add(eclipse);
                }
            }

            foreach (var eclipse in StaleInkDarkMoonEclipses)
            {
                InkDarkMoonEclipseStartTimes.Remove(eclipse);
                InkDarkMoonEclipseDeadSequenceStartTimes.Remove(eclipse);
            }

            StaleInkDarkMoonEclipses.Clear();
        }

        private static void RecoverInkDarkMoonEclipse(Ai_Mon_Ink_BossDarkMoon_Eclipse eclipse)
        {
            if (eclipse == null || !eclipse.isActive)
                return;

            if (eclipse.info.caster is Mon_Ink_BossDarkMoon darkMoon &&
                !darkMoon.IsNullInactiveDeadOrKnockedOut())
            {
                if (darkMoon.Status.TryGetStatusEffect<Se_Mon_Ink_BossDarkMoon_Eclipse>(out var effect))
                {
                    effect.Destroy();
                }

                darkMoon.Control.CancelOngoingChannels();
                darkMoon.Control.CancelOngoingDisplacement();
                darkMoon.Control.ClearActionQueue();
                darkMoon.AI.disableAI = false;
                darkMoon.Visual.EnableRenderers();
                darkMoon.Visual.ShowGroundMarker();
            }

            InkDarkMoonEclipseStartTimes.Remove(eclipse);
            InkDarkMoonEclipseDeadSequenceStartTimes.Remove(eclipse);
            eclipse.DestroyIfActive();
        }

        private sealed class ExtraBossQueueState
        {
            public RoomMonsters Room;
            public SpawnMonsterSettings Origin;
            public Action<Entity> OriginalAfterSpawn;
            public Action OriginalOnFinish;
            public int Remaining;
            public int PendingOriginal;
            public int PendingExtras;
            public int ActiveExtras;
            public bool OriginalFinished;
            public bool InvokedOriginalOnFinish;
        }
    }
}
