using System;
using System.Collections.Generic;
using System.Reflection;
using DewSuperSmart.config;
using UnityEngine;
using UnityEngine.AI;

namespace DewSuperSmart
{
    internal static class ControllerTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        public static void Register(Action<string, Action> run)
        {
            run("controller sends the same owned destination only once", SameDestinationIsNotRepeated);
            run("controller Update keeps a progressing escape after the stall timeout", ProgressingEscapeIsNotRepeated);
            run("controller Update keeps a progressing held cursor move after the stall timeout", ProgressingCursorMoveIsNotRepeated);
            run("controller Update can resend an interrupted destination", InterruptedMoveCanResume);
            run("controller Update stays still beside multiple harmless near misses", NearMissesDoNotTriggerDodge);
            run("controller Update stops an owned route blocked by a new obstacle only once", NewNavigationObstacleInvalidatesRoute);
            run("controller projectile safety includes threats below the trigger level", ProjectileSafetyUsesNavigationThreats);
            run("dodge levels use separate impact time thresholds", DodgeLevelImpactTimeThresholds);
            run("long line attacks use an earlier dodge window", LongLineAttacksUseEarlierWindow);
            run("delayed area damage triggers an early escape and locks its footprint", DelayedAreaEscapesAndLocksFootprint);
            run("delayed area escape can search beyond the default radius", DelayedAreaExpandsSearchToBoundary);
            run("emergency movement cannot land inside a delayed damage area", EmergencyMovementRejectsDelayedAreaLanding);
            run("controller chooses the closest compensated circle edge", CircleDestinationUsesExactBoundary);
            run("controller favors a safe destination toward the cursor", CursorDirectionSelectsSafeDestination);
            run("imminent basic attacks follow the safe cursor direction", BasicAttackFollowsSafeCursorDirection);
            run("controller rejects impossible minimum movement distance", ImpossibleMinimumDistanceHasNoCandidate);
            run("controller Update replans when a new threat covers its active destination", NewThreatInvalidatesDestination);
            run("projectile area footprints block paths and landing points", ProjectileAreaFootprintsBlockNavigation);
            run("projectile red window blocks movement and dodge landing", ProjectileWindowBlocksNavigation);
            run("controller holds its safe arrived position despite a dangerous cursor", SafeArrivedPositionIsStable);
            run("controller rejects a partial navigation path", PartialNavigationPathIsRejected);
            run("controller Update escapes two overlapping circles", TwoOverlappingCirclesProduceSafeEscape);
            run("controller moves out of enclosing red threats without a safe point in range", EnclosingRedThreatsKeepMoving);
            run("controller improves its position in an enclosing projectile barrage", EnclosingProjectileBarrageKeepsMoving);
            run("walkable emergency escape does not spend the movement skill", WalkableEmergencyEscapePrefersMovement);
            run("controller only predicts movement skills with an explicit destination", DirectionOnlySkillIsNotUsed);
            run("emergency dodge casts toward a nonstationary safe point", EmergencyDodgeUsesSafePoint);
            run("emergency dodge waits until the 0.05-second impact window", EmergencyDodgeUsesShorterWindow);
            run("replacement parry blocks an imminent unavoidable hit", ReplacementParryBlocksIncomingHit);
            run("replacement parry ignores a threat away from the hero", ReplacementParryIgnoresRemoteThreat);
        }

        // 使用实际控制器 Update，桩仅提供输入/快照及记录发送到游戏控制层的命令。
        private static Fixture CreateFixture()
        {
            Time.unscaledTime = 0f;
            Input.KeyHeld = false;
            Input.KeyDown = false;
            NavMesh.IsBlocked = false;
            Dew.PathStatus = NavMeshPathStatus.PathComplete;
            ControlManager.Cursor = Vector3.zero;
            var instance = new DewSuperSmart();
            instance.Config.AutoDodgeReserveOneCharge = false;
            DewSuperSmart.Instance = instance;
            var hero = new Hero();
            DewPlayer.local = new DewPlayer { hero = hero };
            return new Fixture(new AutoDodgeController(), hero, instance);
        }

        private static void SameDestinationIsNotRepeated()
        {
            Fixture fixture = CreateFixture();
            Vector3 target = new Vector3(3f, 0f, 0f);
            Assert((bool)Call(fixture.Controller, "TryMoveToSafePoint", fixture.Hero, target, fixture.Config, true, false), "first target must send a command");
            Time.unscaledTime = 0.2f;
            Assert(!(bool)Call(fixture.Controller, "TryMoveToSafePoint", fixture.Hero, target, fixture.Config, true, false), "same active target must be deduplicated");
            Assert(fixture.Hero.Control.MoveCommands == 1, "duplicate request must not reach CmdMoveToDestination");
        }

        private static void ProgressingEscapeIsNotRepeated()
        {
            Fixture fixture = CreateFixture();
            fixture.Instance.Threats.Snapshot.Add(Circle(Vector3.zero, 1f));
            fixture.Update(0f);
            Assert(fixture.Hero.Control.MoveCommands == 1, "initial overlap should issue one escape command");
            Vector3 direction = fixture.Hero.Control.LastDestination.normalized;
            for (int step = 1; step <= 8; step++)
            {
                fixture.Hero.agentPosition = direction * (step * 0.08f);
                fixture.Update(step * 0.2f);
            }

            Assert(fixture.Hero.Control.MoveCommands == 1, "1.6 seconds of measurable progress must not resend or switch the safe target");
        }

        private static void ProgressingCursorMoveIsNotRepeated()
        {
            Fixture fixture = CreateFixture();
            fixture.Config.AutoDodgeKey = KeyCode.Space;
            Input.KeyHeld = true;
            ControlManager.Cursor = new Vector3(5f, 0f, 0f);
            fixture.Update(0f);
            fixture.Update(0.2f);
            Assert(fixture.Hero.Control.MoveCommands == 1, "held cursor should issue its first safe move after the activation delay");
            for (int step = 1; step <= 8; step++)
            {
                fixture.Hero.agentPosition = new Vector3(step * 0.08f, 0f, 0f);
                fixture.Update(0.2f + step * 0.2f);
            }

            Assert(fixture.Hero.Control.MoveCommands == 1, "progressing held movement must refresh progress even without an active dodge target");
        }

        private static void InterruptedMoveCanResume()
        {
            Fixture fixture = CreateFixture();
            fixture.Instance.Threats.Snapshot.Add(Circle(Vector3.zero, 1f));
            fixture.Update(0f);
            Vector3 firstTarget = fixture.Hero.Control.LastDestination;
            fixture.Hero.Control.InterruptMove();
            fixture.Update(0.2f);
            Assert(fixture.Hero.Control.MoveCommands == 2, "interrupted owned movement must allow a new command");
            Assert((fixture.Hero.Control.LastDestination - firstTarget).magnitude < 0.001f, "the same safe target must be eligible after interruption");
        }

        private static void NearMissesDoNotTriggerDodge()
        {
            Fixture fixture = CreateFixture();
            for (int i = 0; i < 5; i++)
            {
                fixture.Instance.Threats.Snapshot.Add(Circle(new Vector3(1.9f, 0f, 0f), 1f));
            }

            fixture.Update(0f);
            fixture.Update(0.8f);
            Assert(ReadList(fixture.Controller, "_threats").Count == 5, "all close threats must enter the active trigger set");
            Assert(fixture.Hero.Control.MoveCommands == 0, "outside every padded collider boundary, aggregate near-miss scores must not move the hero");
            Assert(fixture.Hero.Control.StopCommands == 0, "standing safely must not send stop spam");
        }

        private static void NewNavigationObstacleInvalidatesRoute()
        {
            Fixture fixture = CreateFixture();
            fixture.Instance.Threats.Snapshot.Add(Circle(Vector3.zero, 1f));
            fixture.Update(0f);
            Assert(fixture.Hero.Control.MoveCommands == 1, "initial route must be generated");
            NavMesh.IsBlocked = true;
            fixture.Update(0.2f);
            fixture.Update(0.4f);
            Assert(fixture.Hero.Control.MoveCommands == 1, "new obstruction must reject cached and replacement direct routes");
            Assert(fixture.Hero.Control.StopCommands == 1, "unsafe owned movement must be stopped once and forgotten");
        }

        private static void ProjectileSafetyUsesNavigationThreats()
        {
            Fixture fixture = CreateFixture();
            fixture.Config.AutoDodgeLevel = AutoDodgeThreatLevel.Red;
            fixture.Instance.Threats.Snapshot.Add(ThreatZone.Line(
                null, null, null, new Vector3(0f, 0f, -0.1f), Vector3.forward,
                10f, 0.2f, ThreatSourceKind.Projectile, ThreatActivity.Active,
                1f, 2f, true, 1f));

            Call(fixture.Controller, "RefreshThreatSnapshot", fixture.Hero, fixture.Config, 0.8f);
            Assert(ReadList(fixture.Controller, "_threats").Count == 0, "2-second projectile must be absent from the red trigger set");
            Assert(ReadList(fixture.Controller, "_navigationThreats").Count == 1, "projectile must remain in navigation threats");
            Assert(!(bool)Call(fixture.Controller, "IsCurrentPositionSafe", Vector3.zero, 0.8f, fixture.Config), "collision prediction must include a projectile excluded by trigger filtering");
        }

        private static void CircleDestinationUsesExactBoundary()
        {
            Fixture fixture = CreateFixture();
            fixture.Instance.Threats.Snapshot.Add(Circle(Vector3.zero, 2f));
            fixture.Update(0f);
            Vector3 target = fixture.Hero.Control.LastDestination;
            Assert(fixture.Hero.Control.MoveCommands == 1, "an overlapping circle must produce a move");
            Assert(Math.Abs(target.magnitude - 3.231f) < 0.003f, "target must stop at radius 2 + hero 0.8 + clearance 0.1 + stop distance 0.33 + epsilon");
        }

        private static void ImpossibleMinimumDistanceHasNoCandidate()
        {
            Fixture fixture = CreateFixture();
            object[] parameters =
            {
                fixture.Hero, fixture.Config, 0.8f, 1f, 4f, 0.04f,
                2f, 0f, false, false, Vector3.zero, Vector3.zero
            };
            Assert(!(bool)Call(fixture.Controller, "TryFindSafePoint", parameters), "minimum travel distance 2 cannot fit within search radius 1");
        }

        private static void NewThreatInvalidatesDestination()
        {
            Fixture fixture = CreateFixture();
            fixture.Instance.Threats.Snapshot.Add(Circle(Vector3.zero, 1f));
            fixture.Update(0f);
            Vector3 firstTarget = fixture.Hero.Control.LastDestination;
            ThreatZone newThreat = Circle(firstTarget, 0.8f);
            fixture.Instance.Threats.Snapshot.Add(newThreat);
            fixture.Update(0.2f);
            Assert(fixture.Hero.Control.MoveCommands == 2 || fixture.Hero.Control.StopCommands == 1,
                "a newly covered target must be replaced or its owned route stopped");
            if (fixture.Hero.Control.MoveCommands == 2)
            {
                Vector3 replacement = fixture.Hero.Control.LastDestination;
                Assert((replacement - firstTarget).magnitude > 0.35f, "new command must not reuse the covered destination");
                Assert((replacement - firstTarget).magnitude >= 0.8f + 0.8f + 0.43f,
                    "replacement must keep the hero and arrival compensation outside the new circle");
            }
        }

        private static void ProjectileAreaFootprintsBlockNavigation()
        {
            Fixture fixture = CreateFixture();
            ThreatZone landing = ThreatZone.Circle(null, null, Vector3.zero, 1f,
                ThreatSourceKind.Projectile, ThreatActivity.Imminent, 1f, 0.2f, true);
            fixture.Instance.Threats.Snapshot.Add(landing);
            fixture.Hero.agentPosition = new Vector3(-3f, 0f, 0f);
            Call(fixture.Controller, "RefreshThreatSnapshot", fixture.Hero, fixture.Config, 0.8f);
            Assert(!ThreatPathSafety.IsSafe(new[] { landing }, new Vector3(-3f, 0f, 0f), new Vector3(3f, 0f, 0f), 0.8f),
                "projectile circle is a landing footprint, so walking through it must be rejected");
            Assert(!(bool)Call(fixture.Controller, "IsNavigationCandidateSafe", Vector3.zero,
                fixture.Hero.agentPosition, 0.8f, 0.43f), "a landing point inside a projectile circle must be rejected");
        }

        private static void SafeArrivedPositionIsStable()
        {
            Fixture fixture = CreateFixture();
            fixture.Config.AutoDodgeKey = KeyCode.Space;
            Input.KeyHeld = true;
            fixture.Instance.Threats.Snapshot.Add(Circle(Vector3.zero, 2f));
            fixture.Update(0f);
            fixture.Update(0.2f);
            Vector3 target = fixture.Hero.Control.LastDestination;
            Assert(fixture.Hero.Control.MoveCommands == 1, "initial circle escape must issue one command");
            fixture.Hero.agentPosition = target - target.normalized * 0.316f;
            fixture.Hero.Control.InterruptMove();
            ControlManager.Cursor = Vector3.zero;
            for (int step = 1; step <= 8; step++) fixture.Update(0.2f + step * 0.2f);
            Assert(fixture.Hero.Control.MoveCommands == 1,
                "the actual stop point inside the arrival tolerance must remain still when the held cursor points back into danger");
            Assert(fixture.Hero.Control.StopCommands == 0, "a safely arrived hero must not receive redundant stop commands");

            ControlManager.Cursor = target + target.normalized * 4f;
            fixture.Update(2.1f);
            Assert(fixture.Hero.Control.MoveCommands == 2,
                "after reaching safety, a held dodge key must continue toward a safe cursor path");
        }

        private static void ProjectileWindowBlocksNavigation()
        {
            Fixture fixture = CreateFixture();
            ThreatZone path = ThreatZone.Line(null, null, null, Vector3.zero, new Vector3(1f, 0f, 0f),
                12f, 0.5f, ThreatSourceKind.Projectile, ThreatActivity.Active,
                1.8f, float.PositiveInfinity, true, 10f);
            fixture.Instance.Threats.Snapshot.Add(path);
            fixture.Instance.Threats.Snapshot.Add(ThreatZone.ProjectileWindow(path, Vector3.zero, 12f));
            fixture.Hero.agentPosition = new Vector3(2f, 0f, 2f);
            Call(fixture.Controller, "RefreshThreatSnapshot", fixture.Hero, fixture.Config, 0.4f);

            Assert(!(bool)Call(fixture.Controller, "IsNavigationCandidateSafe",
                new Vector3(2f, 0f, -2f), fixture.Hero.agentPosition, 0.4f, 0.1f),
                "normal movement and point-dodge routes must not cross the red window");
            Assert(!(bool)Call(fixture.Controller, "IsNavigationCandidateSafe",
                new Vector3(2f, 0f, 0f), fixture.Hero.agentPosition, 0.4f, 0.1f),
                "normal movement and point-dodge landing must not enter the red window");
            Assert((bool)Call(fixture.Controller, "IsNavigationCandidateSafe",
                new Vector3(8f, 0f, -2f), new Vector3(8f, 0f, 2f), 0.4f, 0.1f),
                "the distant part of the full trajectory is not a geometric no-go zone");
        }

        private static void DodgeLevelImpactTimeThresholds()
        {
            AssertThreatTime(AutoDodgeThreatLevel.Green, 0.9f, true);
            AssertThreatTime(AutoDodgeThreatLevel.Green, 1.1f, false);
            AssertThreatTime(AutoDodgeThreatLevel.Yellow, 0.4f, true);
            AssertThreatTime(AutoDodgeThreatLevel.Yellow, 0.6f, false);
            AssertThreatTime(AutoDodgeThreatLevel.Red, 0.15f, true);
            AssertThreatTime(AutoDodgeThreatLevel.Red, 0.3f, false);
        }

        private static void LongLineAttacksUseEarlierWindow()
        {
            Fixture fixture = CreateFixture();
            List<ThreatZone> threats = ReadList(fixture.Controller, "_threats");
            threats.Add(ThreatZone.Line(null, null, null, Vector3.zero, Vector3.forward,
                12f, 0.5f, ThreatSourceKind.ActiveEffect, ThreatActivity.Imminent,
                2f, 0.6f, true));
            Call(fixture.Controller, "FilterThreatsByDodgeLevel", AutoDodgeThreatLevel.Red, Vector3.zero, 0.8f);
            Assert(threats.Count == 1,
                "a long telegraphed line should trigger early enough to start moving before impact");
        }

        private static void DelayedAreaEscapesAndLocksFootprint()
        {
            Fixture fixture = CreateFixture();
            fixture.Config.AutoDodgeLevel = AutoDodgeThreatLevel.Red;
            fixture.Config.AutoDodgeKey = KeyCode.Space;
            Input.KeyHeld = true;
            ControlManager.Cursor = new Vector3(10f, 0f, 0f);
            fixture.Instance.Threats.Snapshot.Add(ThreatZone.Circle(null, null, Vector3.zero, 5f,
                ThreatSourceKind.EnemyCast, ThreatActivity.Imminent, 1f, 2.5f, true));
            fixture.Update(0f);
            fixture.Update(0.2f);
            Assert(fixture.Hero.Control.MoveCommands == 1,
                "standing in an admitted delayed area must trigger an escape well before the fixed red timer");
            Assert(fixture.Hero.Control.LastDestination.x > 6f,
                "the commanded destination must clear the delayed damage circle");

            Vector3 safePoint = fixture.Hero.Control.LastDestination;
            fixture.Hero.agentPosition = safePoint;
            fixture.Hero.Control.InterruptMove();
            ControlManager.Cursor = Vector3.zero;
            fixture.Update(0.5f);
            Assert(fixture.Hero.Control.MoveCommands == 1,
                "the delayed area remains forbidden after reaching safety");
            Assert(!(bool)Call(fixture.Controller, "IsNavigationCandidateSafe", Vector3.zero,
                safePoint, 0.8f, 0.43f), "movement and point-dodge landing inside the area must be rejected");
            Assert(!(bool)Call(fixture.Controller, "IsNavigationCandidateSafe", -safePoint,
                safePoint, 0.8f, 0.43f), "a route between safe endpoints must not cross the warning area");

            fixture.Instance.Threats.Snapshot.Clear();
            fixture.Update(0.7f);
            Assert((bool)Call(fixture.Controller, "IsNavigationCandidateSafe", Vector3.zero,
                safePoint, 0.8f, 0.43f), "the former footprint must unlock once its threat disappears");
            fixture.Update(1f);
            Assert(fixture.Hero.Control.MoveCommands == 2,
                "the former area should unlock after its threat disappears");
        }

        private static void EmergencyMovementRejectsDelayedAreaLanding()
        {
            Fixture fixture = CreateFixture();
            fixture.Config.AutoDodgeLevel = AutoDodgeThreatLevel.Red;
            fixture.Config.AutoDodgeUseMovementSkill = true;
            fixture.Hero.Skill.Movement = new SkillTrigger
            {
                Ready = true,
                currentConfig = new TriggerConfig
                {
                    effectiveRange = 5f,
                    castMethod = new CastMethod { type = CastMethodType.Point },
                    spawnedInstance = new Ai_GenericDodge { speed = 12f, minDistance = 2f, uncollidableRatio = 0.7f }
                }
            };
            fixture.Instance.Threats.Snapshot.Add(ThreatZone.Circle(null, null, Vector3.zero, 30f,
                ThreatSourceKind.ActiveEffect, ThreatActivity.Imminent, 1f, 0.08f, true));
            fixture.Update(0f);
            Assert(fixture.Hero.Control.MoveCommands == 0,
                "emergency steps still inside an admitted delayed area must not be issued");
            Assert(fixture.Hero.Control.CastCommands == 0,
                "the dodge skill must not land inside the delayed damage area");
        }

        private static void DelayedAreaExpandsSearchToBoundary()
        {
            Fixture fixture = CreateFixture();
            fixture.Config.AutoDodgeLevel = AutoDodgeThreatLevel.Red;
            ControlManager.Cursor = new Vector3(20f, 0f, 0f);
            fixture.Instance.Threats.Snapshot.Add(ThreatZone.Circle(null, null, Vector3.zero, 9f,
                ThreatSourceKind.ActiveEffect, ThreatActivity.Imminent, 1f, 3f, true));
            fixture.Update(0f);
            Assert(fixture.Hero.Control.MoveCommands == 1,
                "the search must reach beyond the default 7-unit radius to escape a larger warning area");
            Assert(fixture.Hero.Control.LastDestination.x > 10f,
                "the expanded destination must clear the warning area's padded boundary");
        }

        private static void AssertThreatTime(AutoDodgeThreatLevel level, float timeToImpact, bool expected)
        {
            Fixture fixture = CreateFixture();
            List<ThreatZone> threats = ReadList(fixture.Controller, "_threats");
            threats.Add(ThreatZone.Circle(null, null, new Vector3(10f, 0f, 0f), 1f,
                ThreatSourceKind.EnemyCast, ThreatActivity.Imminent, 1f, timeToImpact, true));
            Call(fixture.Controller, "FilterThreatsByDodgeLevel", level, Vector3.zero, 0.8f);
            Assert((threats.Count == 1) == expected,
                level + " impact at " + timeToImpact + " seconds has the wrong trigger state");
        }

        private static void CursorDirectionSelectsSafeDestination()
        {
            Fixture fixture = CreateFixture();
            ControlManager.Cursor = new Vector3(10f, 0f, 0f);
            fixture.Instance.Threats.Snapshot.Add(Circle(Vector3.zero, 2f));
            fixture.Update(0f);
            Assert(fixture.Hero.Control.MoveCommands == 1, "a safe escape must be issued");
            Assert(fixture.Hero.Control.LastDestination.x > 2.5f,
                "a cursor on the right must favor the safe right edge over closer points in other directions");
        }

        private static void BasicAttackFollowsSafeCursorDirection()
        {
            Fixture fixture = CreateFixture();
            var monster = new Monster { agentPosition = Vector3.zero };
            fixture.Hero.agentPosition = new Vector3(1.5f, 0f, 0f);
            ControlManager.Cursor = new Vector3(1.5f, 0f, 8f);
            fixture.Instance.Threats.Snapshot.Add(ThreatZone.Line(
                monster, new AttackTrigger { owner = monster }, null,
                Vector3.zero, new Vector3(1f, 0f, 0f), 2.5f, 1f,
                ThreatSourceKind.EnemyCast, ThreatActivity.Imminent, 1f, 0.9f, true));
            fixture.Update(0f);
            Assert(fixture.Hero.Control.MoveCommands == 1,
                "an imminent basic attack covering the hero should issue a movement command");
            Assert(fixture.Hero.Control.LastDestination.z > fixture.Hero.Control.LastDestination.x,
                "movement should leave the attack range along the safe direction toward the cursor");

            fixture.Hero.agentPosition = fixture.Hero.Control.LastDestination;
            fixture.Hero.Control.InterruptMove();
            fixture.Update(0.2f);
            Assert(fixture.Hero.Control.MoveCommands == 2,
                "after reaching safety, dodging should continue along a safe route toward the cursor");
        }

        private static void EmergencyDodgeUsesShorterWindow()
        {
            Fixture fixture = CreateFixture();
            fixture.Config.AutoDodgeUseMovementSkill = true;
            fixture.Hero.Skill.Movement = new SkillTrigger
            {
                Ready = true,
                currentConfig = new TriggerConfig
                {
                    effectiveRange = 5f,
                    castMethod = new CastMethod { type = CastMethodType.Point },
                    spawnedInstance = new Ai_GenericDodge { speed = 12f, minDistance = 2f, uncollidableRatio = 0.7f }
                }
            };
            fixture.Instance.Threats.Snapshot.Add(ThreatZone.Circle(null, null, Vector3.zero, 2f,
                ThreatSourceKind.EnemyCast, ThreatActivity.Imminent, 1f, 0.10f, true));
            fixture.Update(0f);
            Assert(fixture.Hero.Control.CastCommands == 0,
                "the old 0.1-second dodge window must no longer spend the skill");

            fixture.Instance.Threats.Snapshot.Clear();
            fixture.Instance.Threats.Snapshot.Add(ThreatZone.Circle(null, null, Vector3.zero, 2f,
                ThreatSourceKind.EnemyCast, ThreatActivity.Imminent, 1f, 0.08f, true));
            fixture.Update(0.03f);
            Assert(fixture.Hero.Control.CastCommands == 1,
                "the skill should cast inside 0.05 seconds plus its 0.04-second command lead");
        }

        private static St_M_ParryMaster EquipReplacementParry(Fixture fixture)
        {
            var skill = new St_M_ParryMaster
            {
                Ready = true,
                currentConfig = new TriggerConfig
                {
                    castMethod = new CastMethod { type = CastMethodType.None },
                    spawnedInstance = new Ai_D_ParryMaster_Parry()
                }
            };
            fixture.Config.AutoDodgeUseMovementSkill = true;
            fixture.Hero.Skill.Movement = skill;
            return skill;
        }

        private static void EmergencyDodgeUsesSafePoint()
        {
            Fixture fixture = CreateFixture();
            fixture.Config.AutoDodgeUseMovementSkill = true;
            ControlManager.Cursor = new Vector3(10f, 0f, 0f);
            fixture.Hero.Skill.Movement = new SkillTrigger
            {
                Ready = true,
                currentConfig = new TriggerConfig
                {
                    effectiveRange = 5f,
                    castMethod = new CastMethod { type = CastMethodType.Point },
                    spawnedInstance = new Ai_GenericDodge { speed = 12f, minDistance = 2f, uncollidableRatio = 0.7f }
                }
            };
            fixture.Instance.Threats.Snapshot.Add(ThreatZone.Circle(null, null, Vector3.zero, 2f,
                ThreatSourceKind.EnemyCast, ThreatActivity.Imminent, 1f, 0.08f, true));
            fixture.Update(0f);
            Assert(fixture.Hero.Control.CastCommands == 1, "an imminent hit must use the ready dodge");
            Assert(fixture.Hero.Control.MoveCommands == 0, "normal movement must not be treated as safe before impact");
            Assert(fixture.Hero.Control.LastCastPoint.x > 3.2f,
                "the point cast must target a safe destination toward the cursor rather than the current position");
        }

        private static void ReplacementParryBlocksIncomingHit()
        {
            Fixture fixture = CreateFixture();
            EquipReplacementParry(fixture);
            fixture.Instance.Threats.Snapshot.Add(ThreatZone.Circle(null, null, Vector3.zero, 8f,
                ThreatSourceKind.EnemyCast, ThreatActivity.Imminent, 1f, 0.08f, true));
            fixture.Update(0f);
            Assert(fixture.Hero.Control.CastCommands == 1, "imminent unavoidable damage must trigger parry");
            Assert(fixture.Hero.Control.MoveCommands == 0, "parry must not issue a stationary dodge move");
            fixture.Update(0.1f);
            Assert(fixture.Hero.Control.CastCommands == 1, "the same parry must not be spammed while active");
        }

        private static void ReplacementParryIgnoresRemoteThreat()
        {
            Fixture fixture = CreateFixture();
            EquipReplacementParry(fixture);
            fixture.Instance.Threats.Snapshot.Add(ThreatZone.Circle(null, null, new Vector3(6f, 0f, 0f), 1f,
                ThreatSourceKind.Projectile, ThreatActivity.Imminent, 1f, 0.08f, true));
            fixture.Update(0f);
            Assert(fixture.Hero.Control.CastCommands == 0, "a distant impact must not consume parry");
        }

        private static void PartialNavigationPathIsRejected()
        {
            Fixture fixture = CreateFixture();
            Dew.PathStatus = NavMeshPathStatus.PathPartial;
            Assert(!(bool)Call(fixture.Controller, "IsNavigationCandidateSafe", new Vector3(3f, 0f, 0f),
                Vector3.zero, 0.8f, 0.43f), "a partial route must be rejected even when the direct ray is unblocked");
        }

        private static void TwoOverlappingCirclesProduceSafeEscape()
        {
            Fixture fixture = CreateFixture();
            Vector3 leftCenter = new Vector3(-0.5f, 0f, 0f);
            Vector3 rightCenter = new Vector3(0.5f, 0f, 0f);
            fixture.Instance.Threats.Snapshot.Add(Circle(leftCenter, 2f));
            fixture.Instance.Threats.Snapshot.Add(Circle(rightCenter, 2f));
            fixture.Update(0f);
            Assert(fixture.Hero.Control.MoveCommands == 1, "overlapping threats must permit a shared outward escape");
            Vector3 target = fixture.Hero.Control.LastDestination;
            Assert((target - leftCenter).magnitude >= 3.23f && (target - rightCenter).magnitude >= 3.23f,
                "chosen target must clear each radius 2 + hero 0.8 + clearance 0.1 + stop distance 0.33");
        }

        // 仅方向施法无法传递被验证的距离，不能把同一个候选点当作实际落点。
        private static void DirectionOnlySkillIsNotUsed()
        {
            Fixture fixture = CreateFixture();
            var skill = new SkillTrigger
            {
                currentConfig = new TriggerConfig
                {
                    effectiveRange = 5f,
                    castMethod = new CastMethod { type = CastMethodType.Arrow },
                    spawnedInstance = new Ai_GenericDodge { speed = 12f, minDistance = 2f, uncollidableRatio = 0.7f }
                }
            };
            MethodInfo method = typeof(AutoDodgeController).GetMethod("TryGetDodgeSkillProfile", BindingFlags.Static | BindingFlags.NonPublic);
            object[] arguments = { skill, fixture.Config, null };
            Assert(!(bool)method.Invoke(null, arguments), "direction-only casting must not pretend to specify a landing distance");
            skill.currentConfig.castMethod = new CastMethod { type = CastMethodType.Point };
            Assert((bool)method.Invoke(null, arguments), "a point-targeted generic dodge must remain usable");
        }

        private static void EnclosingRedThreatsKeepMoving()
        {
            Fixture fixture = CreateFixture();
            fixture.Config.AutoDodgeLevel = AutoDodgeThreatLevel.Red;
            ControlManager.Cursor = new Vector3(20f, 0f, 0f);
            fixture.Instance.Threats.Snapshot.Add(Circle(new Vector3(-1f, 0f, -1f), 8f));
            fixture.Instance.Threats.Snapshot.Add(Circle(new Vector3(1f, 0f, -1f), 8f));
            fixture.Instance.Threats.Snapshot.Add(Circle(new Vector3(-1f, 0f, 1f), 8f));
            fixture.Instance.Threats.Snapshot.Add(Circle(new Vector3(1f, 0f, 1f), 8f));

            fixture.Update(0f);
            Assert(fixture.Hero.Control.MoveCommands == 1,
                "overlapping red zones with no fully safe point in range must start an escape");
            Assert(fixture.Hero.Control.LastDestination.x > 1f,
                "the emergency step should reduce danger toward the cursor");
            Vector3 firstStep = fixture.Hero.Control.LastDestination;
            fixture.Hero.agentPosition = firstStep * 0.5f;
            fixture.Update(0.2f);
            Assert(fixture.Hero.Control.MoveCommands == 1,
                "a progressing emergency route should not be replaced each frame");
            fixture.Hero.agentPosition = firstStep;
            fixture.Hero.Control.InterruptMove();
            fixture.Update(0.4f);
            Assert(fixture.Hero.Control.MoveCommands == 2,
                "after completing an emergency step, the controller should continue escaping");
            Assert(fixture.Hero.Control.LastDestination.x > firstStep.x,
                "the next step should keep reducing the enclosing danger");
        }

        private static void EnclosingProjectileBarrageKeepsMoving()
        {
            Fixture fixture = CreateFixture();
            fixture.Config.AutoDodgeLevel = AutoDodgeThreatLevel.Red;
            ControlManager.Cursor = new Vector3(8f, 0f, 0f);
            for (int i = -2; i <= 2; i++)
            {
                fixture.Instance.Threats.Snapshot.Add(ThreatZone.Line(
                    null, null, null, new Vector3(-1.5f, 0f, -3f + i * 0.2f), Vector3.forward,
                    8f, 2.5f, ThreatSourceKind.Projectile, ThreatActivity.Active,
                    1f, 0.1f, true, 8f));
            }

            fixture.Update(0f);
            Assert(fixture.Hero.Control.MoveCommands == 1,
                "when a fully safe point cannot be reached through a barrage, movement should reduce predicted impact risk");
        }

        private static void WalkableEmergencyEscapePrefersMovement()
        {
            Fixture fixture = CreateFixture();
            fixture.Config.AutoDodgeLevel = AutoDodgeThreatLevel.Red;
            fixture.Config.AutoDodgeUseMovementSkill = true;
            ControlManager.Cursor = new Vector3(20f, 0f, 0f);
            fixture.Hero.Skill.Movement = new SkillTrigger
            {
                Ready = true,
                currentConfig = new TriggerConfig
                {
                    effectiveRange = 5f,
                    castMethod = new CastMethod { type = CastMethodType.Point },
                    spawnedInstance = new Ai_GenericDodge { speed = 12f, minDistance = 2f, uncollidableRatio = 0.7f }
                }
            };
            fixture.Instance.Threats.Snapshot.Add(Circle(new Vector3(-1f, 0f, -1f), 8f));
            fixture.Instance.Threats.Snapshot.Add(Circle(new Vector3(1f, 0f, -1f), 8f));
            fixture.Instance.Threats.Snapshot.Add(Circle(new Vector3(-1f, 0f, 1f), 8f));
            fixture.Instance.Threats.Snapshot.Add(Circle(new Vector3(1f, 0f, 1f), 8f));
            fixture.Update(0f);
            Assert(fixture.Hero.Control.MoveCommands == 1,
                "a walkable emergency route should issue ordinary movement first");
            Assert(fixture.Hero.Control.CastCommands == 0,
                "a walkable emergency route should not spend the movement skill");
        }

        private static ThreatZone Circle(Vector3 center, float radius)
        {
            return ThreatZone.Circle(null, null, center, radius, ThreatSourceKind.EnemyCast, ThreatActivity.Active, 1f, 0f, true);
        }

        private static object Call(AutoDodgeController controller, string name, params object[] arguments)
        {
            MethodInfo method = typeof(AutoDodgeController).GetMethod(name, PrivateInstance);
            if (method == null) throw new InvalidOperationException("Missing production method: " + name);
            try { return method.Invoke(controller, arguments); }
            catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
        }

        private static List<ThreatZone> ReadList(AutoDodgeController controller, string name)
        {
            return (List<ThreatZone>)typeof(AutoDodgeController).GetField(name, PrivateInstance).GetValue(controller);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class Fixture
        {
            public readonly AutoDodgeController Controller;
            public readonly Hero Hero;
            public readonly DewSuperSmart Instance;
            public PluginConfig Config => Instance.Config;
            public Fixture(AutoDodgeController controller, Hero hero, DewSuperSmart instance)
            {
                Controller = controller;
                Hero = hero;
                Instance = instance;
            }

            public void Update(float time)
            {
                Time.unscaledTime = time;
                Call(Controller, "Update");
            }
        }
    }
}
