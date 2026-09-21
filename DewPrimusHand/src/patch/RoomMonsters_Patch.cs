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
        // 每个房间独立维护 Boss 预留槽位，避免多个生成请求互相覆盖计数。
        private static readonly ConditionalWeakTable<RoomMonsters, BossRoomCoordinator> RoomCoordinators = new();
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

            int extraBossCount = CalculateExtraBossCount();
            if (extraBossCount <= 0)
                return;

            var coordinator = RoomCoordinators.GetValue(__instance, room => new BossRoomCoordinator(room));
            var state = coordinator.CreateRequest(settings, extraBossCount);

            // 原始 Boss 由游戏原生协程生成，这里只包装回调，不改变原始生成时序。
            settings.afterSpawn = spawnedEntity =>
            {
                state.OriginalAfterSpawn?.Invoke(spawnedEntity);
                state.OnOriginalSpawned(spawnedEntity);
            };
            settings.onFinish = () =>
            {
                state.OnOriginalGenerationFinished();
                state.TryFinishEncounter();
            };

            __instance.StartCoroutine(SpawnExtraBosses(state));
        }

        // 按房间可用槽位逐个补充 Boss；AllOnce 只跳过间隔，不跳过上限。
        private static IEnumerator SpawnExtraBosses(BossSpawnRequest state)
        {
            while (state.RemainingExtra > 0)
            {
                if (!state.Coordinator.TryReserveSlot())
                {
                    KeepBossEncounterRunning();
                    yield return new WaitForSeconds(0.25f);
                    continue;
                }

                state.RemainingExtra--;
                SpawnExtraBoss(state);

                if (!DewPrimusHand.Instance.Config.BossSpawnAllOnce && state.RemainingExtra > 0)
                {
                    yield return new WaitForSeconds(UnityEngine.Random.Range(2f, 5f));
                }
                else
                {
                    yield return null;
                }
            }

            state.TryFinishEncounter();
        }

        private static void SpawnExtraBoss(BossSpawnRequest state)
        {
            var clone = CloneSettings(state);
            ExtraSpawnMarker.Add(clone, new object());
            state.PendingExtraRequests++;
            try
            {
                state.Room.SpawnMonsters(clone);
            }
            catch
            {
                // 原生入口异常时立即归还预留槽位，避免队列永久卡住。
                state.Coordinator.ReleaseSlot();
                state.PendingExtraRequests = Mathf.Max(0, state.PendingExtraRequests - 1);
                state.TryFinishEncounter();
                throw;
            }
        }

        private static SpawnMonsterSettings CloneSettings(BossSpawnRequest state)
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

            bool reservationReleased = false;
            bool didSpawn = false;
            clone.afterSpawn += spawnedEntity =>
            {
                if (!IsCountedBoss(spawnedEntity))
                    return;

                didSpawn = true;
                if (!reservationReleased)
                {
                    reservationReleased = true;
                    state.Coordinator.ReleaseSlot();
                }

                state.ActiveExtraBosses++;
                ApplyAfterSpawn(spawnedEntity);

                spawnedEntity.EntityEvent_OnDeath += _ =>
                {
                    state.ActiveExtraBosses = Mathf.Max(0, state.ActiveExtraBosses - 1);
                    state.TryFinishEncounter();
                };
            };
            clone.onFinish = () =>
            {
                if (!reservationReleased)
                {
                    reservationReleased = true;
                    state.Coordinator.ReleaseSlot();
                }

                state.PendingExtraRequests = Mathf.Max(0, state.PendingExtraRequests - 1);
                if (!didSpawn)
                {
                    KeepBossEncounterRunning();
                }

                state.TryFinishEncounter();
            };

            return clone;
        }

        // BossMonster 在最后一个 Boss 死亡时会暂停游戏时间；队列仍有任务时必须解除暂停。
        private static void KeepBossEncounterRunning()
        {
            var gameManager = NetworkedManagerBase<GameManager>.instance;
            if (gameManager != null)
                gameManager.isGameTimePausedByGame = false;
        }

        private static int CountAliveBosses()
        {
            var actorManager = NetworkedManagerBase<ActorManager>.instance;
            if (actorManager == null)
                return 0;

            int count = 0;
            foreach (var entity in actorManager.allEntities)
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
            // 配置值本身就是房间内允许同时存活的 Boss 数量，不再额外减一。
            return Mathf.Max(1, DewPrimusHand.Instance.Config.BossCountInRoom);
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

        private sealed class BossRoomCoordinator
        {
            public readonly RoomMonsters Room;
            private int _reservedBosses;

            public BossRoomCoordinator(RoomMonsters room)
            {
                Room = room;
            }

            // 原始请求永远由游戏触发，先登记一个预留槽位用于和额外请求统一计数。
            public BossSpawnRequest CreateRequest(SpawnMonsterSettings origin, int extraBossCount)
            {
                ReserveSlot();
                return new BossSpawnRequest(this, origin, extraBossCount);
            }

            public bool TryReserveSlot()
            {
                if (CountAliveBosses() + _reservedBosses >= GetMaxBossCountInRoom())
                    return false;

                ReserveSlot();
                return true;
            }

            public void ReserveSlot()
            {
                _reservedBosses++;
            }

            public void ReleaseSlot()
            {
                _reservedBosses = Mathf.Max(0, _reservedBosses - 1);
            }
        }

        private sealed class BossSpawnRequest
        {
            public readonly BossRoomCoordinator Coordinator;
            public readonly RoomMonsters Room;
            public readonly SpawnMonsterSettings Origin;
            public readonly Action<Entity> OriginalAfterSpawn;
            private readonly Action _originalOnFinish;

            public int RemainingExtra;
            public int PendingExtraRequests;
            public int ActiveExtraBosses;
            private bool _originalSpawned;
            private bool _originalReservationReleased;
            private bool _originalGenerationFinished;
            private bool _encounterFinished;

            public BossSpawnRequest(BossRoomCoordinator coordinator, SpawnMonsterSettings origin, int extraBossCount)
            {
                Coordinator = coordinator;
                Room = coordinator.Room;
                Origin = origin;
                OriginalAfterSpawn = origin.afterSpawn;
                _originalOnFinish = origin.onFinish;
                RemainingExtra = extraBossCount;
            }

            // 原始 Boss 生成后将预留转为 ActorManager 中的实际存活实体。
            public void OnOriginalSpawned(Entity entity)
            {
                if (!IsCountedBoss(entity))
                    return;

                _originalSpawned = true;
                ReleaseOriginalReservation();
            }

            public void OnOriginalGenerationFinished()
            {
                _originalGenerationFinished = true;
                ReleaseOriginalReservation();
            }

            // 生成协程和额外 Boss 都结束后，才交还原始 onFinish，避免提前结束 Boss 房流程。
            public void TryFinishEncounter()
            {
                if (_encounterFinished || !_originalGenerationFinished)
                    return;

                if (RemainingExtra > 0 || PendingExtraRequests > 0 || ActiveExtraBosses > 0)
                {
                    KeepBossEncounterRunning();
                    return;
                }

                _encounterFinished = true;
                _originalOnFinish?.Invoke();
            }

            private void ReleaseOriginalReservation()
            {
                if (_originalReservationReleased)
                    return;

                _originalReservationReleased = true;
                Coordinator.ReleaseSlot();
            }
        }
    }
}
