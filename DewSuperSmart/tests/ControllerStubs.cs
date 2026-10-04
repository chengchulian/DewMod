using System;
using System.Collections.Generic;
using DewSuperSmart.config;
using UnityEngine;
using UnityEngine.AI;

namespace UnityEngine
{
    public enum KeyCode { None, Space }

    public static class Time { public static float unscaledTime; }

    public static class Input
    {
        public static bool KeyHeld;
        public static bool KeyDown;
        public static bool GetKey(KeyCode key) => KeyHeld;
        public static bool GetKeyDown(KeyCode key) => KeyDown;
    }
}

namespace UnityEngine.AI
{
    public enum NavMeshPathStatus { PathComplete, PathPartial, PathInvalid }
    public struct NavMeshHit { }
    public class NavMeshPath { public NavMeshPathStatus status = NavMeshPathStatus.PathComplete; }

    public static class NavMesh
    {
        public const int AllAreas = -1;
        public static bool IsBlocked;
        public static bool Raycast(Vector3 origin, Vector3 target, out NavMeshHit hit, int mask)
        {
            hit = default;
            return IsBlocked;
        }
    }
}

namespace DewSuperSmart.config
{
    internal enum AutoDodgeThreatLevel { Red, Yellow, Green }

    internal sealed class PluginConfig
    {
        public bool EnableAutoDodge = true;
        public bool AutoDodgeUseMovementSkill = false;
        public bool AutoDodgeReserveOneCharge;
        public AutoDodgeThreatLevel AutoDodgeLevel = AutoDodgeThreatLevel.Green;
        public float AutoDodgeCommandInterval = 0.05f;
        public KeyCode AutoDodgeKey = KeyCode.None;
        public float AutoDodgeHoldDelay => 0.12f;
        public bool AutoDodgeMoveFallback => true;
        public float AutoDodgeRiskThreshold => 0.9f;
        public float AutoDodgeSearchRadius => 7f;
        public float ThreatScanRange => 24f;
        public float ThreatPadding => 0.35f;
    }
}

namespace DewSuperSmart
{
    internal sealed class DewSuperSmart
    {
        public static DewSuperSmart Instance;
        public PluginConfig Config = new PluginConfig();
        public ThreatSnapshotProvider Threats = new ThreatSnapshotProvider();
    }

    internal sealed class ThreatSnapshotProvider
    {
        public readonly List<ThreatZone> Snapshot = new List<ThreatZone>();
        public IReadOnlyList<ThreatZone> GetThreats(Hero hero, PluginConfig config) => Snapshot;
    }

    internal sealed class DewPlayer
    {
        public static DewPlayer local;
        public Hero hero;
    }

    public class Entity : Actor
    {
        public Vector3 agentPosition;
        public bool inactive;
        public bool IsNullInactiveDeadOrKnockedOut() => inactive;
    }

    public class Hero : Entity
    {
        public EntityControl Control = new EntityControl();
        public HeroSkills Skill = new HeroSkills();
    }

    public class Monster : Entity { }

    public class EntityControl
    {
        // 与实际 EntityControl 的本地导航状态同名，供生产代码反射读取。
        private Vector3? _desiredAgentDestination;
        public bool isDisplacing;
        public float outerRadius = 0.45f;
        public float currentMaxAgentSpeed = 4f;
        public int MoveCommands;
        public int StopCommands;
        public int CastCommands;
        public Vector3 LastDestination;
        public Vector3 LastCastPoint;

        public void CmdMoveToDestination(Vector3 point, bool immediately, float speedMult)
        {
            MoveCommands++;
            LastDestination = point;
            _desiredAgentDestination = point;
        }

        public void CmdStop() { StopCommands++; _desiredAgentDestination = null; }
        public void CmdCast(SkillTrigger skill, int index, CastInfo info, bool allowMoveToCast, bool skipRangeCheck)
        {
            CastCommands++;
            LastCastPoint = info.point;
        }
        public void CmdAttack(object target, bool doChase) { }
        public void InterruptMove() => _desiredAgentDestination = null;
        public Vector3? DesiredDestination => _desiredAgentDestination;
    }

    public class AbilityInstance : Actor
    {
        public CastInfo info;
        public DewCollider[] Colliders = Array.Empty<DewCollider>();
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : Component =>
            typeof(T) == typeof(DewCollider) ? (T[])(object)Colliders : Array.Empty<T>();
    }
    public class InstantDamageInstance : AbilityInstance { public float damageDelay; }
    public class Ai_Mon_Forest_Treant_PowerBomb : InstantDamageInstance { }
    public class Ai_Mon_Forest_Hound_Charge : AbilityInstance { }
    public class DewCollider : MonoBehaviour
    {
        public enum ColliderShape { Circle, Box, Polygon }
        public ColliderShape shape;
        public float radius;
    }
    public class Ai_GenericDodge { public float speed; public float minDistance; public float uncollidableRatio; }
    public enum HeroSkillLocation { Movement }
    public class HeroSkills
    {
        public SkillTrigger Movement;
        public bool TryGetSkill(HeroSkillLocation location, out SkillTrigger trigger) { trigger = Movement; return trigger != null; }
    }

    public class SkillTrigger : AbilityTrigger
    {
        public TriggerConfig currentConfig;
        public int currentConfigIndex;
        public int currentConfigCurrentCharge;
        public bool Ready;
        public bool CanBeCast() => Ready;
        public float GetChannelDurationMultiplier() => 1f;
    }

    public class St_M_ParryMaster : SkillTrigger { }
    public class Ai_D_ParryMaster_Parry { public float duration = 1f; }

    public class TriggerConfig
    {
        public object spawnedInstance;
        public ChannelConfig channel;
        public bool postponeBasicCommand;
        public float effectiveRange;
        public CastMethod castMethod;
    }

    public class ChannelConfig { public float duration; }
    public struct CastMethod { public CastMethodType type; }
    public enum CastMethodType { Point, Arrow, Cone, Target, None }
    public struct CastInfo
    {
        public Actor caster;
        public Vector3 point;
        public CastInfo(Actor caster) : this() => this.caster = caster;
        public CastInfo(Actor caster, Vector3 point) { this.caster = caster; this.point = point; }
        public CastInfo(Actor caster, float angle) : this() => this.caster = caster;
        public static float GetAngle(Vector3 point) => 0f;
    }

    public static class ControlManager
    {
        public static Vector3 Cursor;
        public static Vector3 GetWorldPositionOnGroundOnCursor() => Cursor;
    }

    public static class Dew
    {
        public static NavMeshPathStatus PathStatus = NavMeshPathStatus.PathComplete;
        public static bool IsOkay(Vector3 value) => !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z));
        public static Vector3 GetValidAgentDestination_LinearSweep(Vector3 origin, Vector3 target) => target;
        public static Vector3 GetValidAgentDestination_Closest(Vector3 origin, Vector3 target) => target;
        public static Vector3 GetPositionOnGround(Vector3 point) => point;
        public static NavMeshPath GetNavMeshPath(Vector3 origin, Vector3 target) => new NavMeshPath { status = PathStatus };
    }
}
