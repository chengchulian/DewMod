using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DewSuperSmart.config;
using UnityEngine;

namespace DewSuperSmart;

internal sealed class ThreatAnalyzer
{
    private const float MinimumShapeSize = 0.2f;
    private const float DefaultConeAngle = 70f;
    private const float FallbackProjectileSpeed = 10f;
    private const float ColliderCacheRefreshInterval = 0.5f;
    private const float ColliderCacheCleanupInterval = 2f;
    private const float MonsterRushThreatWeight = 2.35f;

    private static readonly string[] DelayFieldNames =
    {
        "damageDelay",
        "explodeDelay",
        "impactDelay",
        "hitDelay",
        "telegraphTime",
        "castDelay",
        "initDelay",
        "delay",
        "dmgDelay",
        "explosionDelay",
        "startDelay",
        "initialDelay"
    };

    private static readonly Dictionary<Type, FieldInfo[]> ColliderFieldCache = new Dictionary<Type, FieldInfo[]>();
    private static readonly Dictionary<Type, FieldInfo[]> DelayFieldCache = new Dictionary<Type, FieldInfo[]>();
    private static readonly Dictionary<Type, BeamFields> BeamFieldCache = new Dictionary<Type, BeamFields>();
    private static readonly Dictionary<Type, ScriptedShape[]> ScriptedShapeCache = new Dictionary<Type, ScriptedShape[]>();
    private static readonly FieldInfo ProjectileEstimatedVelocityField =
        typeof(Projectile).GetField("_estimatedVelocity", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly HashSet<DewCollider> _seenColliders = new HashSet<DewCollider>();
    private readonly HashSet<Actor> _seenActors = new HashSet<Actor>();
    private readonly List<DewCollider> _colliderBuffer = new List<DewCollider>(16);
    private readonly Dictionary<int, ColliderCacheEntry> _collidersByActor = new Dictionary<int, ColliderCacheEntry>();
    private readonly Dictionary<int, float> _castStartTimes = new Dictionary<int, float>();
    private readonly List<int> _staleColliderCacheKeys = new List<int>();

    private float _nextColliderCacheCleanupTime = float.NegativeInfinity;

    private sealed class ColliderCacheEntry
    {
        public Actor Actor;
        public DewCollider[] Colliders;
        public float RefreshTime;
    }

    private sealed class BeamFields
    {
        public FieldInfo BeamRadius;
        public FieldInfo HitBoxLength;
        public FieldInfo DistanceCurve;
        public FieldInfo StartOffset;
        public FieldInfo BeamDuration;
        public FieldInfo NetworkBeamDuration;
        public FieldInfo BeamStartTime;
        public FieldInfo CastDelay;

        public bool IsUsable => BeamRadius != null && HitBoxLength != null && DistanceCurve != null;
    }

    private enum ScriptedShapeKind
    {
        Circle,
        Line
    }

    private enum ScriptedShapeAnchor
    {
        Instance,
        CastPoint,
        Caster,
        StatusVictim,
        CenteredLine
    }

    private enum ScriptedShapeTiming
    {
        Default,
        BlackholeTick,
        BlackholeExplosion,
        TrapTrigger
    }

    private sealed class ScriptedShape
    {
        public ScriptedShapeKind Kind;
        public ScriptedShapeAnchor Anchor;
        public ScriptedShapeTiming Timing;
        public FieldInfo Radius;
        public FieldInfo Length;
        public bool RadiusIsHalfWidth;
        public float Weight;
    }

    public void CollectThreats(Hero hero, PluginConfig config, List<ThreatZone> results)
    {
        results.Clear();
        _seenActors.Clear();
        _seenColliders.Clear();

        if (hero == null || hero.IsNullInactiveDeadOrKnockedOut() || config == null)
        {
            return;
        }

        ActorManager manager = NetworkedManagerBase<ActorManager>.softInstance;
        if (manager == null)
        {
            return;
        }

        float scanRange = Mathf.Max(config.ThreatScanRange, 1f);
        CleanupColliderCache();
        CollectEnemyCastPreviews(hero, config, manager, results, scanRange);

        foreach (Actor actor in manager.allActors)
        {
            CollectActorThreats(hero, config, actor, results, scanRange);
        }
    }

    private void CollectEnemyCastPreviews(
        Hero hero,
        PluginConfig config,
        ActorManager manager,
        List<ThreatZone> results,
        float scanRange)
    {
        foreach (Entity entity in manager.allEntities)
        {
            if (entity is not Monster monster ||
                monster.IsNullInactiveDeadOrKnockedOut() ||
                monster.Ability == null ||
                !monster.CheckEnemyOrNeutral(hero))
            {
                continue;
            }

            AbilityTrigger activeAttack = monster.Ability.attackAbility;
            bool activeAttackIncluded = false;
            foreach (KeyValuePair<int, AbilityTrigger> pair in monster.Ability.abilities)
            {
                AddCastPreview(hero, monster, pair.Value, config, results, scanRange);
                activeAttackIncluded |= pair.Value == activeAttack;
            }

            if (!activeAttackIncluded && activeAttack is AttackTrigger)
            {
                AddCastPreview(hero, monster, activeAttack, config, results, scanRange);
            }
        }
    }

    private void AddCastPreview(
        Hero hero,
        Monster monster,
        AbilityTrigger trigger,
        PluginConfig config,
        List<ThreatZone> results,
        float scanRange)
    {
        if (!TryGetUsableTrigger(hero, monster, trigger, out TriggerConfig triggerConfig, out bool isReady))
        {
            if (trigger != null)
            {
                _castStartTimes.Remove(trigger.GetInstanceID());
            }

            return;
        }

        bool isCasting = trigger.Network_isCasting;
        int triggerId = trigger.GetInstanceID();
        if (isCasting)
        {
            if (!_castStartTimes.ContainsKey(triggerId))
            {
                _castStartTimes[triggerId] = Time.time;
            }
        }
        else
        {
            _castStartTimes.Remove(triggerId);
        }

        ThreatActivity activity = isCasting ? ThreatActivity.Imminent : ThreatActivity.Preview;
        float timeToImpact = isCasting
            ? GetRemainingChannelTime(monster, trigger, triggerConfig, _castStartTimes[triggerId])
            : float.PositiveInfinity;
        float treantDamageRadius = 0f;
        float treantDamageDelay = 0f;
        bool isTreantPowerBomb = trigger.GetType().Name == "At_Mon_Forest_Treant_PowerBomb" &&
                                 TreantPowerBombPreview.TryGet(
                                     triggerConfig.spawnedInstance, out treantDamageRadius, out treantDamageDelay);
        if (isCasting && isTreantPowerBomb)
        {
            timeToImpact += treantDamageDelay;
        }
        float weight = (trigger is AttackTrigger ? 1f : 1.35f) + (isCasting ? 0.65f : 0f);
        CastInfo castInfo = GetThreatCastInfo(trigger, hero, config.ThreatPredictionStrength);
        CastMethodData method = triggerConfig.castMethod;
        Vector3 origin = monster.agentPosition;
        Vector3 direction = GetThreatDirection(origin, hero.agentPosition, castInfo);
        bool isDodgeable = isCasting;
        bool requiresDodgeSkill = isCasting &&
                                  method.type == CastMethodType.Target &&
                                  IsTargetingHero(monster, hero);

        ThreatZone threat;
        switch (method.type)
        {
            case CastMethodType.None:
                threat = ThreatZone.Circle(
                    monster,
                    trigger,
                    origin,
                    isTreantPowerBomb
                        ? treantDamageRadius
                        : Positive(method.noneData.radius, triggerConfig.effectiveRange, config.DefaultThreatAreaRadius),
                    ThreatSourceKind.EnemyCast,
                    activity,
                    weight,
                    timeToImpact,
                    isDodgeable);
                break;
            case CastMethodType.Cone:
                threat = ThreatZone.Cone(
                    monster,
                    trigger,
                    origin,
                    direction,
                    Positive(method.coneData.radius, triggerConfig.effectiveRange, config.DefaultThreatAreaRadius),
                    Positive(method.coneData.angle, DefaultConeAngle),
                    activity,
                    weight,
                    timeToImpact,
                    isDodgeable);
                break;
            case CastMethodType.Arrow:
                threat = ThreatZone.Line(
                    monster,
                    trigger,
                    null,
                    origin,
                    direction,
                    Positive(method.arrowData.length, triggerConfig.effectiveRange, config.AutoDodgeFallbackDistance),
                    GetLineWidth(triggerConfig, config),
                    ThreatSourceKind.EnemyCast,
                    activity,
                    weight,
                    timeToImpact,
                    isDodgeable);
                break;
            case CastMethodType.Point:
                Vector3 point = castInfo.point;
                if (!Dew.IsOkay(point) || point == Vector3.zero)
                {
                    point = origin + direction * Positive(method.pointData.range, triggerConfig.effectiveRange, config.AutoDodgeFallbackDistance);
                }

                threat = ThreatZone.Circle(
                    monster,
                    trigger,
                    Dew.GetPositionOnGround(point),
                    Positive(method.pointData.radius, config.DefaultThreatAreaRadius),
                    ThreatSourceKind.EnemyCast,
                    activity,
                    weight,
                    timeToImpact,
                    isDodgeable);
                break;
            case CastMethodType.Target:
                // Locked target damage cannot be escaped spatially, but dodge invulnerability can avoid it.
                threat = ThreatZone.Circle(
                    monster,
                    trigger,
                    origin,
                    Positive(method.targetData.range, triggerConfig.effectiveRange, config.DefaultThreatAreaRadius),
                    ThreatSourceKind.EnemyCast,
                    activity,
                    weight,
                    timeToImpact,
                    isDodgeable: requiresDodgeSkill,
                    requiresDodgeSkill);
                break;
            default:
                return;
        }

        if ((!config.DrawReadyThreatsOnly || isReady || isCasting) &&
            threat.SignedDistance(hero.agentPosition, 0f) <= scanRange)
        {
            results.Add(threat);
        }
    }

    private void CollectActorThreats(
        Hero hero,
        PluginConfig config,
        Actor actor,
        List<ThreatZone> results,
        float scanRange)
    {
        if (actor == null || !_seenActors.Add(actor) || actor.IsNullOrInactive())
        {
            return;
        }

        if (actor is Projectile projectile)
        {
            CollectProjectileThreat(hero, config, projectile, results, scanRange);
            return;
        }

        if (actor is AbilityInstance instance)
        {
            CollectAbilityInstanceThreat(hero, config, instance, results, scanRange);
            return;
        }

        CollectScriptedEnvironmentThreat(hero, actor, results, scanRange);

        if (actor is IToggleableTrap { isOn: true })
        {
            CollectColliders(actor, _colliderBuffer);
            for (int i = 0; i < _colliderBuffer.Count; i++)
            {
                AddColliderThreat(
                    actor,
                    _colliderBuffer[i],
                    ThreatSourceKind.Environment,
                    ThreatActivity.Active,
                    weight: 2f,
                    timeToImpact: 0f,
                    isDodgeable: true,
                    hero,
                    scanRange,
                    results);
            }
        }
    }

    private void CollectAbilityInstanceThreat(
        Hero hero,
        PluginConfig config,
        AbilityInstance instance,
        List<ThreatZone> results,
        float scanRange)
    {
        Entity caster = ResolveCaster(instance);
        Entity threatOwner = caster;
        if (instance is StatusEffect status && threatOwner == null)
        {
            threatOwner = status.victim;
        }

        if (!IsHostileOrEnvironmental(instance, caster, hero, config.DrawUnknownSourceThreats) ||
            !CanInstanceAffectHero(instance, caster, hero))
        {
            return;
        }

        bool isRoomHazard = IsRoomHazardAbility(instance);
        bool isMeteorRainImpact = IsMeteorRainImpact(instance);
        TryAddBeamThreat(instance, hero, scanRange, results);
        CollectScriptedAbilityThreats(instance, caster, hero, scanRange, results);
        TryAddBlackholeAttractionThreat(instance, caster, hero, scanRange, results);
        TryAddCataclysmUnsafeArea(instance, hero, scanRange, results);
        TryAddRotatingStarThreats(instance, hero, scanRange, results);
        bool isMonsterRush = IsMonsterRush(instance, caster);
        CollectColliders(instance, _colliderBuffer);
        if (isMonsterRush &&
            TryGetMonsterRushShape(instance, _colliderBuffer, out Vector3 rushOrigin, out Vector3 rushDirection, out float rushLength, out float rushWidth))
        {
            AddMonsterRushThreat(
                instance,
                hero,
                sourceKind: isRoomHazard ? ThreatSourceKind.Environment : ThreatSourceKind.ActiveEffect,
                rushOrigin,
                rushDirection,
                rushLength,
                rushWidth,
                scanRange,
                results);
        }

        if (_colliderBuffer.Count == 0)
        {
            return;
        }

        float timeToImpact = EstimateAbilityTimeToImpact(instance);
        ThreatActivity activity = timeToImpact > 0.05f ? ThreatActivity.Imminent : ThreatActivity.Active;
        float weight = isMeteorRainImpact ? 2.35f : instance is DamageInstance ? 1.9f : 1.65f;
        ThreatSourceKind sourceKind = isRoomHazard
            ? ThreatSourceKind.Environment
            : ThreatSourceKind.ActiveEffect;
        bool isDodgeable = instance is DamageInstance || threatOwner != null || IsEnvironmentalAbility(instance);

        for (int i = 0; i < _colliderBuffer.Count; i++)
        {
            if (TryGetDeferredTargetRotation(instance, _colliderBuffer[i], out Quaternion targetRotation))
            {
                if (instance.GetType().Name == "Ai_Mon_SnowMountain_BossSkoll_AuraSlice")
                {
                    // 雪山王横扫的预警角度会随机偏转；碰撞框要到伤害前才移到目标点。
                    for (int angle = -45; angle <= 45; angle += 15)
                    {
                        AddProjectedColliderThreat(
                            instance,
                            _colliderBuffer[i],
                            _colliderBuffer[i].transform,
                            instance.info.point,
                            targetRotation * Quaternion.Euler(0f, angle, 0f),
                            sourceKind,
                            activity,
                            weight,
                            timeToImpact,
                            isDodgeable,
                            hero,
                            scanRange,
                            results);
                    }
                }
                else
                {
                    AddProjectedColliderThreat(
                        instance,
                        _colliderBuffer[i],
                        _colliderBuffer[i].transform,
                        instance.info.point,
                        targetRotation,
                        sourceKind,
                        activity,
                        weight,
                        timeToImpact,
                        isDodgeable,
                        hero,
                        scanRange,
                        results);
                }
            }
            else if (instance is StatusEffect statusEffect && statusEffect.victim != null)
            {
                AddProjectedColliderThreat(
                    instance,
                    _colliderBuffer[i],
                    instance.transform,
                    statusEffect.victim.agentPosition,
                    statusEffect.victim.rotation,
                    sourceKind,
                    activity,
                    weight,
                    timeToImpact,
                    isDodgeable,
                    hero,
                    scanRange,
                    results);
            }
            else if (caster != null && (MonsterRushProjection.UsesIndependentCollider(instance) ||
                     isMonsterRush && !IsLongRangeSweepRush(instance)))
            {
                AddProjectedColliderThreat(
                    instance,
                    _colliderBuffer[i],
                    MonsterRushProjection.GetRoot(instance, _colliderBuffer[i]),
                    caster.agentPosition,
                    caster.rotation,
                    sourceKind,
                    activity,
                    weight,
                    timeToImpact,
                    isDodgeable,
                    hero,
                    scanRange,
                    results);
            }
            else
            {
                AddColliderThreat(
                    instance,
                    _colliderBuffer[i],
                    sourceKind,
                    activity,
                    weight,
                    timeToImpact,
                    isDodgeable,
                    hero,
                    scanRange,
                    results);
            }
        }
    }

    // 这些技能的碰撞框在预警期间还停留在资源默认位置，伤害触发时才移动到施法点。
    private static bool TryGetDeferredTargetRotation(
        AbilityInstance instance, DewCollider collider, out Quaternion rotation)
    {
        string name = instance.GetType().Name;
        if (name == "Ai_Mon_Special_BossErebos_AtkInstance" ||
            name == "Ai_Mon_SnowMountain_BossSkoll_AuraSlice")
        {
            FieldInfo rangeField = GetTypedField(
                instance.GetType(), "range", typeof(DewCollider),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (!ReferenceEquals(rangeField?.GetValue(instance), collider))
            {
                rotation = Quaternion.identity;
                return false;
            }

            rotation = instance.rotation;
            return Dew.IsOkay(instance.info.point) && instance.info.point != Vector3.zero;
        }

        rotation = Quaternion.identity;
        return false;
    }

    private static bool IsMonsterRush(AbilityInstance instance, Entity caster)
    {
        if (!(caster is Monster) || caster.Control == null ||
            !(caster.Control.ongoingDisplacement is DispByDestination displacement) ||
            !Dew.IsOkay(displacement.destination))
        {
            return false;
        }

        if (instance is DashAttackInstance)
        {
            return true;
        }

        string name = instance.GetType().Name;
        return name.IndexOf("Charge", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Dash", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Roll", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Pounce", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsLongRangeSweepRush(AbilityInstance instance)
    {
        return instance.GetType().Name == "Ai_Mon_Special_BossObliviax_DashAtk";
    }

    private static bool TryGetMonsterRushShape(
        AbilityInstance instance,
        List<DewCollider> colliders,
        out Vector3 origin,
        out Vector3 direction,
        out float length,
        out float width)
    {
        origin = Vector3.zero;
        direction = Vector3.forward;
        length = 0f;
        width = 0f;

        Entity caster = ResolveCaster(instance);
        if (!(caster is Monster) || caster.Control == null ||
            !(caster.Control.ongoingDisplacement is DispByDestination displacement))
        {
            return false;
        }

        origin = caster.agentPosition;
        Vector3 destination = displacement.destination;
        destination.y = origin.y;
        Vector3 delta = destination - origin;
        delta.y = 0f;
        length = delta.magnitude;
        if (length <= MinimumShapeSize)
        {
            return false;
        }

        direction = delta / length;
        Vector3 perpendicular = new Vector3(-direction.z, 0f, direction.x);
        float halfWidth = 0f;
        for (int i = 0; i < colliders.Count; i++)
        {
            DewCollider collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            Transform colliderTransform = collider.transform;
            Transform sourceRoot = MonsterRushProjection.GetRoot(instance, collider);
            Vector3 rootPosition = origin;
            Vector3 Project(Vector3 point) =>
                MonsterRushProjection.Project(sourceRoot, point, rootPosition, caster.rotation);
            switch (collider.shape)
            {
                case DewCollider.ColliderShape.Circle:
                    Vector3 center = Project(colliderTransform.TransformPoint(new Vector3(collider.offset.x, 0f, collider.offset.y)));
                    halfWidth = Mathf.Max(
                        halfWidth,
                        Mathf.Abs(Vector3.Dot(center - origin, perpendicular)) +
                        collider.radius * GetMaxFlatScale(colliderTransform));
                    break;
                case DewCollider.ColliderShape.Box:
                    Vector3[] boxPoints = BuildBoxPoints(collider, Project);
                    halfWidth = Mathf.Max(halfWidth, GetMaximumPerpendicularDistance(boxPoints, origin, perpendicular));
                    break;
                case DewCollider.ColliderShape.Polygon:
                    Vector3[] polygonPoints = BuildPolygonPoints(collider, Project);
                    halfWidth = Mathf.Max(halfWidth, GetMaximumPerpendicularDistance(polygonPoints, origin, perpendicular));
                    break;
            }
        }

        if (halfWidth <= 0.01f)
        {
            string name = instance.GetType().Name;
            if (name == "Ai_Mon_Despair_BossAzurak_Roll")
            {
                halfWidth = ReadFloat(instance, "hitRadius");
            }
            else if (name == "Ai_Mon_Sky_BossNyx_StellarDash")
            {
                halfWidth = ReadFloat(instance, "colRadius");
            }
        }

        width = Mathf.Max(halfWidth * 2f, MinimumShapeSize);
        return halfWidth > 0.01f;
    }

    private static float GetMaximumPerpendicularDistance(Vector3[] points, Vector3 origin, Vector3 perpendicular)
    {
        float maximum = 0f;
        if (points == null)
        {
            return maximum;
        }

        for (int i = 0; i < points.Length; i++)
        {
            maximum = Mathf.Max(maximum, Mathf.Abs(Vector3.Dot(points[i] - origin, perpendicular)));
        }

        return maximum;
    }

    private static void AddMonsterRushThreat(
        AbilityInstance instance,
        Hero hero,
        ThreatSourceKind sourceKind,
        Vector3 rushOrigin,
        Vector3 rushDirection,
        float rushLength,
        float rushWidth,
        float scanRange,
        List<ThreatZone> results)
    {
        Entity caster = ResolveCaster(instance);
        if (caster == null)
        {
            return;
        }

        float timeToImpact = 0f;
        if (caster.Control.ongoingDisplacement is DispByDestination displacement && displacement.duration > 0.0001f)
        {
            float remainingDuration = Mathf.Max(displacement.duration - displacement.elapsedTime, 0f);
            Vector3 toHero = hero.agentPosition - rushOrigin;
            toHero.y = 0f;
            float distanceAlongRush = Vector3.Dot(toHero, rushDirection);
            distanceAlongRush = Mathf.Clamp(distanceAlongRush, 0f, rushLength);
            float distanceUntilImpact = Mathf.Max(distanceAlongRush - rushWidth * 0.5f, 0f);
            float estimatedImpactTime = remainingDuration * Mathf.Clamp01(distanceUntilImpact / rushLength);
            timeToImpact = Mathf.Max(estimatedImpactTime, Mathf.Min(remainingDuration, 0.01f));
        }

        ThreatActivity activity = timeToImpact > 0.05f ? ThreatActivity.Imminent : ThreatActivity.Active;
        ThreatZone threat = ThreatZone.Line(
            instance,
            null,
            null,
            rushOrigin,
            rushDirection,
            rushLength,
            rushWidth,
            sourceKind,
            activity,
            MonsterRushThreatWeight,
            timeToImpact,
            isDodgeable: true);

        if (threat.SignedDistance(hero.agentPosition, 0f) <= scanRange)
        {
            results.Add(threat);
        }
    }

    private static void CollectScriptedAbilityThreats(
        AbilityInstance instance,
        Entity caster,
        Hero hero,
        float scanRange,
        List<ThreatZone> results)
    {
        ScriptedShape[] shapes = GetScriptedShapes(instance.GetType());
        if ((instance.GetType().Name == "Ai_Mon_Despair_BossAzurak_Roll" ||
             instance.GetType().Name == "Ai_Mon_Sky_BossNyx_StellarDash") &&
            (caster == null || caster.Control == null || !caster.Control.isDisplacing))
        {
            return;
        }

        for (int i = 0; i < shapes.Length; i++)
        {
            ScriptedShape shape = shapes[i];
            float radius = ReadFloat(instance, shape.Radius);
            if (!IsPositiveSize(radius) ||
                !TryGetScriptedTiming(instance, shape.Timing, out ThreatActivity activity, out float timeToImpact))
            {
                continue;
            }

            Vector3 center = GetScriptedAnchor(instance, caster, shape.Anchor);
            ThreatZone threat;
            if (shape.Kind == ScriptedShapeKind.Line)
            {
                float length = ReadFloat(instance, shape.Length);
                if (!IsPositiveSize(length))
                {
                    continue;
                }

                Vector3 direction = GetInstanceDirection(instance);
                if (shape.Anchor == ScriptedShapeAnchor.CenteredLine)
                {
                    center -= direction * (length * 0.5f);
                }

                threat = ThreatZone.Line(
                    instance,
                    null,
                    null,
                    center,
                    direction,
                    length,
                    shape.RadiusIsHalfWidth ? radius * 2f : radius,
                    ThreatSourceKind.ActiveEffect,
                    activity,
                    shape.Weight,
                    timeToImpact,
                    isDodgeable: true);
            }
            else
            {
                threat = ThreatZone.Circle(
                    instance,
                    null,
                    center,
                    radius,
                    ThreatSourceKind.ActiveEffect,
                    activity,
                    shape.Weight,
                    timeToImpact,
                    isDodgeable: true);
            }

            if (threat.SignedDistance(hero.agentPosition, 0f) <= scanRange)
            {
                results.Add(threat);
            }
        }
    }

    private static void TryAddBlackholeAttractionThreat(
        AbilityInstance instance,
        Entity caster,
        Hero hero,
        float scanRange,
        List<ThreatZone> results)
    {
        string typeName = instance.GetType().Name;
        if (typeName != "Ai_Mon_Sky_BossNyx_Blackhole" &&
            typeName != "Ai_Mon_Special_BossErebos_SpawnBlackhole_Instance")
        {
            return;
        }

        if (!TryGetScriptedTiming(
                instance,
                ScriptedShapeTiming.BlackholeTick,
                out ThreatActivity activity,
                out float timeToImpact) ||
            !TryReadVector2(instance, "distanceBounds", out Vector2 bounds))
        {
            return;
        }

        float radius = Mathf.Max(bounds.x, bounds.y);
        if (!IsPositiveSize(radius))
        {
            return;
        }

        ScriptedShapeAnchor anchor = typeName == "Ai_Mon_Sky_BossNyx_Blackhole"
            ? ScriptedShapeAnchor.Caster
            : ScriptedShapeAnchor.CastPoint;
        ThreatZone threat = ThreatZone.Circle(
            instance,
            null,
            GetScriptedAnchor(instance, caster, anchor),
            radius,
            ThreatSourceKind.ActiveEffect,
            activity,
            weight: 1.55f,
            timeToImpact,
            isDodgeable: true);
        if (threat.SignedDistance(hero.agentPosition, 0f) <= scanRange)
        {
            results.Add(threat);
        }
    }

    private static void TryAddCataclysmUnsafeArea(
        AbilityInstance instance,
        Hero hero,
        float scanRange,
        List<ThreatZone> results)
    {
        if (instance.GetType().Name != "Ai_Mon_Ink_BossWhiteNight_Cataclysm_SafeZone")
        {
            return;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo radiusField = GetTypedField(instance.GetType(), "_radius", typeof(float), flags);
        FieldInfo pointsField = instance.GetType().GetField("_points", flags);
        float radius = ReadFloat(instance, radiusField);
        if (!IsPositiveSize(radius) || pointsField == null)
        {
            return;
        }

        List<Vector3> centers = new List<Vector3>();
        try
        {
            if (pointsField.GetValue(instance) is IEnumerable values)
            {
                foreach (object value in values)
                {
                    if (value is Vector3 point && Dew.IsOkay(point))
                    {
                        centers.Add(point);
                    }
                }
            }
        }
        catch (Exception)
        {
            return;
        }

        if (centers.Count == 0)
        {
            if (!SafeZoneTracker.TryGetCenters(instance, out IReadOnlyList<Vector3> trackedCenters))
            {
                return;
            }

            for (int i = 0; i < trackedCenters.Count; i++)
            {
                centers.Add(trackedCenters[i]);
            }
        }

        ThreatZone threat = ThreatZone.OutsideCircles(
            instance,
            centers.ToArray(),
            radius,
            ThreatSourceKind.ActiveEffect,
            ThreatActivity.Active,
            weight: 2.5f,
            timeToImpact: 0f,
            isDodgeable: true);
        if (threat.SignedDistance(hero.agentPosition, 0f) <= scanRange)
        {
            results.Add(threat);
        }
    }

    private static void TryAddRotatingStarThreats(
        AbilityInstance instance,
        Hero hero,
        float scanRange,
        List<ThreatZone> results)
    {
        if (instance.GetType().Name != "Se_Mon_Sky_BossNyx_StarBuff" ||
            !TryReadVector3(instance, "_syncedPosition", out Vector3 center) ||
            !TryReadQuaternion(instance, "_syncedRotation", out Quaternion rotation))
        {
            return;
        }

        float radius = ReadFloat(instance, "range");
        float offset = ReadFloat(instance, "disFromCenter");
        if (!IsPositiveSize(radius) || !IsPositiveSize(offset))
        {
            return;
        }

        Vector3 right = rotation * Vector3.right;
        right.y = 0f;
        right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
        for (int sign = -1; sign <= 1; sign += 2)
        {
            ThreatZone threat = ThreatZone.Circle(
                instance,
                null,
                center + right * (offset * sign),
                radius,
                ThreatSourceKind.ActiveEffect,
                ThreatActivity.Active,
                weight: 2f,
                timeToImpact: 0f,
                isDodgeable: true);
            if (threat.SignedDistance(hero.agentPosition, 0f) <= scanRange)
            {
                results.Add(threat);
            }
        }
    }

    private static void CollectScriptedEnvironmentThreat(
        Hero hero,
        Actor actor,
        List<ThreatZone> results,
        float scanRange)
    {
        string typeName = actor.GetType().Name;
        if (typeName == "LavaLand_Lava")
        {
            results.Add(ThreatZone.Ground(actor, GroundHazardKind.Lava, threshold: 0f, weight: 2.4f));
            return;
        }

        if (typeName == "Sky_LightFogDamager")
        {
            if (ReadBool(actor, "doDamage"))
            {
                float threshold = ReadFloat(actor, "fogOpacityThreshold");
                results.Add(ThreatZone.Ground(actor, GroundHazardKind.LightFog, threshold, weight: 2.4f));
            }

            return;
        }

        string fieldName;
        switch (typeName)
        {
            case "Ink_BossRoomDamageGround":
            case "Forest_Fireplace":
            case "LavaLand_IcePlace":
                fieldName = "radius";
                break;
            default:
                return;
        }

        FieldInfo radiusField = GetTypedField(
            actor.GetType(),
            fieldName,
            typeof(float),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        float radius = ReadFloat(actor, radiusField);
        if (!IsPositiveSize(radius))
        {
            return;
        }

        ThreatZone threat = ThreatZone.Circle(
            actor,
            null,
            actor.position,
            radius,
            ThreatSourceKind.Environment,
            ThreatActivity.Active,
            weight: typeName == "Forest_Fireplace" ? 2.4f : 2f,
            timeToImpact: 0f,
            isDodgeable: true);
        if (threat.SignedDistance(hero.agentPosition, 0f) <= scanRange)
        {
            results.Add(threat);
        }
    }

    private static ScriptedShape[] GetScriptedShapes(Type type)
    {
        if (ScriptedShapeCache.TryGetValue(type, out ScriptedShape[] cached))
        {
            return cached;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        List<ScriptedShape> shapes = new List<ScriptedShape>();
        HashSet<string> fields = new HashSet<string>(StringComparer.Ordinal);

        void AddCircle(
            string radiusName,
            ScriptedShapeAnchor anchor,
            ScriptedShapeTiming timing = ScriptedShapeTiming.Default,
            float weight = 1.85f)
        {
            FieldInfo radius = GetTypedField(type, radiusName, typeof(float), flags);
            if (radius == null || !fields.Add(radiusName))
            {
                return;
            }

            shapes.Add(new ScriptedShape
            {
                Kind = ScriptedShapeKind.Circle,
                Anchor = anchor,
                Timing = timing,
                Radius = radius,
                Weight = weight
            });
        }

        void AddLine(
            string radiusName,
            string lengthName,
            ScriptedShapeAnchor anchor,
            ScriptedShapeTiming timing = ScriptedShapeTiming.Default,
            float weight = 2f)
        {
            FieldInfo radius = GetTypedField(type, radiusName, typeof(float), flags);
            FieldInfo length = GetTypedField(type, lengthName, typeof(float), flags);
            if (radius == null || length == null || !fields.Add(radiusName))
            {
                return;
            }

            shapes.Add(new ScriptedShape
            {
                Kind = ScriptedShapeKind.Line,
                Anchor = anchor,
                Timing = timing,
                Radius = radius,
                Length = length,
                RadiusIsHalfWidth = true,
                Weight = weight
            });
        }

        switch (type.Name)
        {
            case "Ai_Mon_Despair_BossAzurak_Roll":
                AddCircle("hitRadius", ScriptedShapeAnchor.Caster, weight: 2.1f);
                break;
            case "Ai_Mon_Despair_WretchedArtillery_BarrageAtk_AoE":
            case "Ai_Mon_Special_BossErebos_SpawnTormentor_Slow":
                AddCircle("radius", ScriptedShapeAnchor.Instance);
                break;
            case "Ai_Mon_Forest_BossDemon_AltSkill_Stomp":
                AddCircle("radius", ScriptedShapeAnchor.Caster, weight: 2f);
                break;
            case "Ai_Mon_Ink_Archer_Atk":
            case "Ai_Mon_Special_BossErebos_AntiGravity_Instance":
            case "Ai_Mon_Special_BossMaw_Purgatory_Instance":
            case "Ai_Mon_Special_BossMaw_ShadowWalk_Projectile":
                AddCircle(type.Name.Contains("AntiGravity") ? "radius" : "range", ScriptedShapeAnchor.CastPoint, weight: 2f);
                break;
            case "Ai_Mon_LavaLand_BossInfernus_PowerBomb_FlamePillar":
            case "Ai_RoomMod_InkStrikeWarning_Artillery":
                AddCircle("range", ScriptedShapeAnchor.Instance, weight: 2f);
                break;
            case "Ai_Mon_Sky_BossNyx_Blackhole":
                AddCircle("tickDamageRadius", ScriptedShapeAnchor.Caster, ScriptedShapeTiming.BlackholeTick, 2.1f);
                AddCircle("explodeDamageRadius", ScriptedShapeAnchor.Caster, ScriptedShapeTiming.BlackholeExplosion, 2.4f);
                break;
            case "Ai_Mon_Special_BossErebos_SpawnBlackhole_Instance":
                AddCircle("tickDamageRadius", ScriptedShapeAnchor.CastPoint, ScriptedShapeTiming.BlackholeTick, 2.1f);
                AddCircle("explodeDamageRadius", ScriptedShapeAnchor.CastPoint, ScriptedShapeTiming.BlackholeExplosion, 2.4f);
                break;
            case "Ai_Mon_SnowMountain_BossSkoll_SummonArrow_Instance":
                AddCircle("tickDmgRange", ScriptedShapeAnchor.CastPoint);
                break;
            case "Ai_Mon_Special_BossErebos_Gaze_Instance":
                AddLine("width", "distance", ScriptedShapeAnchor.CenteredLine, weight: 2.3f);
                break;
            case "Ai_U_WorldCracker":
                AddLine("radius", "maxDistance", ScriptedShapeAnchor.Instance, weight: 2.2f);
                break;
            case "Ai_Mon_Sky_BossNyx_StellarDash":
                AddCircle("colRadius", ScriptedShapeAnchor.Caster, weight: 2.1f);
                break;
            case "Ai_Mon_Special_BossObliviax_ChargeSequence_MissileAtk":
                AddCircle("collisionRadius", ScriptedShapeAnchor.Instance, weight: 2.1f);
                break;
            case "Ai_Mon_Special_BossMaw_Purgatory_Trap":
                AddCircle("range", ScriptedShapeAnchor.Instance, ScriptedShapeTiming.TrapTrigger, 2.2f);
                break;
            case "Se_EngulfedInFlame":
                AddCircle("immolationRadius", ScriptedShapeAnchor.StatusVictim, weight: 1.9f);
                break;
        }

        // Strong semantic names are safe fallbacks for version-specific hostile content. Generic
        // "radius" and "range" are deliberately excluded because they are commonly targeting ranges.
        if (typeof(AbilityInstance).IsAssignableFrom(type) &&
            (type.Name.StartsWith("Ai_Mon_", StringComparison.Ordinal) ||
             type.Name.StartsWith("Ai_RoomMod_", StringComparison.Ordinal)))
        {
            string[] strongRadiusNames =
            {
                "tickDamageRadius",
                "explodeDamageRadius",
                "damageRadius",
                "dmgRadius",
                "tickDmgRange",
                "collisionRadius",
                "colRadius",
                "hitRadius"
            };
            for (int i = 0; i < strongRadiusNames.Length; i++)
            {
                AddCircle(strongRadiusNames[i], ScriptedShapeAnchor.Instance);
            }
        }

        cached = shapes.ToArray();
        ScriptedShapeCache[type] = cached;
        return cached;
    }

    private static bool TryGetScriptedTiming(
        AbilityInstance instance,
        ScriptedShapeTiming timing,
        out ThreatActivity activity,
        out float timeToImpact)
    {
        float age = Mathf.Max(Time.time - instance.creationTime, 0f);
        switch (timing)
        {
            case ScriptedShapeTiming.BlackholeTick:
                bool isOn = ReadBool(instance, "_isBlackholeOn");
                float startDelay = ReadFloat(instance, "displaceDuration");
                if (!isOn && age > startDelay + 0.25f)
                {
                    activity = default;
                    timeToImpact = float.PositiveInfinity;
                    return false;
                }

                timeToImpact = isOn ? 0f : Mathf.Max(startDelay - age, 0f);
                activity = isOn ? ThreatActivity.Active : ThreatActivity.Imminent;
                return true;
            case ScriptedShapeTiming.BlackholeExplosion:
                float duration = ReadFloat(instance, "blackholeDuration");
                float delay = ReadFloat(instance, "displaceDuration");
                timeToImpact = Mathf.Max(delay + duration - age, 0f);
                activity = timeToImpact > 0.05f ? ThreatActivity.Imminent : ThreatActivity.Active;
                return true;
            case ScriptedShapeTiming.TrapTrigger:
                if (ReadBool(instance, "_canExplode"))
                {
                    timeToImpact = Mathf.Max(ReadFloat(instance, "explosionDelay"), 0f);
                    activity = ThreatActivity.Imminent;
                }
                else
                {
                    timeToImpact = 0f;
                    activity = ThreatActivity.Active;
                }

                return true;
            default:
                timeToImpact = EstimateAbilityTimeToImpact(instance);
                activity = timeToImpact > 0.05f ? ThreatActivity.Imminent : ThreatActivity.Active;
                return true;
        }
    }

    private static Vector3 GetScriptedAnchor(
        AbilityInstance instance,
        Entity caster,
        ScriptedShapeAnchor anchor)
    {
        switch (anchor)
        {
            case ScriptedShapeAnchor.CastPoint:
                return Dew.IsOkay(instance.info.point) ? instance.info.point : instance.position;
            case ScriptedShapeAnchor.Caster:
                return caster != null ? caster.agentPosition : instance.position;
            case ScriptedShapeAnchor.StatusVictim:
                return instance is StatusEffect status && status.victim != null
                    ? status.victim.agentPosition
                    : instance.position;
            default:
                return instance.position;
        }
    }

    private static Vector3 GetInstanceDirection(AbilityInstance instance)
    {
        Vector3 direction = instance.info.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = instance.transform.forward;
            direction.y = 0f;
        }

        return direction.sqrMagnitude <= 0.0001f ? Vector3.forward : direction.normalized;
    }

    private static void TryAddBeamThreat(
        AbilityInstance instance,
        Hero hero,
        float scanRange,
        List<ThreatZone> results)
    {
        BeamFields fields = GetBeamFields(instance.GetType());
        if (!fields.IsUsable ||
            !TryReadFloat(instance, fields.BeamRadius, out float radius) ||
            !TryReadFloat(instance, fields.HitBoxLength, out float hitBoxLength) ||
            fields.DistanceCurve.GetValue(instance) is not AnimationCurve distanceCurve)
        {
            return;
        }

        float duration = ReadFloat(instance, fields.NetworkBeamDuration);
        if (duration <= 0f)
        {
            duration = ReadFloat(instance, fields.BeamDuration);
        }

        float startTime = ReadFloat(instance, fields.BeamStartTime);
        if (startTime <= 0f)
        {
            startTime = instance.creationTime + ReadFloat(instance, fields.CastDelay);
        }

        float timeToImpact = Mathf.Max(startTime - Time.time, 0f);
        float normalizedTime = duration > 0.001f
            ? Mathf.Clamp01((Time.time - startTime) / duration)
            : 1f;
        float distance = Mathf.Max(distanceCurve.Evaluate(normalizedTime), 0f);
        ThreatActivity activity = timeToImpact > 0.05f ? ThreatActivity.Imminent : ThreatActivity.Active;
        if (activity == ThreatActivity.Imminent)
        {
            distance = Mathf.Max(distance, Mathf.Max(distanceCurve.Evaluate(1f), 0f));
        }

        if (distance <= MinimumShapeSize)
        {
            return;
        }

        Vector3 direction = instance.info.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = instance.transform.forward;
            direction.y = 0f;
        }

        direction = direction.sqrMagnitude <= 0.0001f ? Vector3.forward : direction.normalized;
        Vector3 origin = instance.position;
        if (fields.StartOffset != null && instance.info.caster != null &&
            TryReadVector3(instance, fields.StartOffset, out Vector3 startOffset))
        {
            origin = instance.info.caster.position + instance.info.caster.rotation * startOffset;
        }

        float startDistance = Mathf.Clamp(distance - hitBoxLength, 0f, float.PositiveInfinity);
        ThreatZone threat = ThreatZone.Line(
            instance,
            null,
            null,
            origin + direction * startDistance,
            direction,
            Mathf.Max(distance - startDistance, MinimumShapeSize),
            Mathf.Max(radius * 2f, MinimumShapeSize),
            ThreatSourceKind.ActiveEffect,
            activity,
            weight: 2f,
            timeToImpact,
            isDodgeable: true);

        if (threat.SignedDistance(hero.agentPosition, 0f) <= scanRange)
        {
            results.Add(threat);
        }
    }

    private static BeamFields GetBeamFields(Type type)
    {
        if (BeamFieldCache.TryGetValue(type, out BeamFields fields))
        {
            return fields;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        fields = new BeamFields
        {
            BeamRadius = GetTypedField(type, "beamRadius", typeof(float), flags),
            HitBoxLength = GetTypedField(type, "hitBoxLength", typeof(float), flags),
            DistanceCurve = GetTypedField(type, "distanceCurve", typeof(AnimationCurve), flags),
            StartOffset = GetTypedField(type, "startOffset", typeof(Vector3), flags),
            BeamDuration = GetTypedField(type, "beamDuration", typeof(float), flags),
            NetworkBeamDuration = GetTypedField(type, "_beamDuration", typeof(float), flags),
            BeamStartTime = GetTypedField(type, "_beamStartTime", typeof(float), flags),
            CastDelay = GetTypedField(type, "castDelay", typeof(float), flags)
        };
        BeamFieldCache[type] = fields;
        return fields;
    }

    private static FieldInfo GetTypedField(Type type, string name, Type fieldType, BindingFlags flags)
    {
        FieldInfo field = type.GetField(name, flags);
        return field != null && field.FieldType == fieldType ? field : null;
    }

    private static bool TryReadFloat(AbilityInstance instance, FieldInfo field, out float value)
    {
        value = ReadFloat(instance, field);
        return value > 0f;
    }

    private static float ReadFloat(AbilityInstance instance, FieldInfo field)
    {
        return ReadFloat((Actor)instance, field);
    }

    private static float ReadFloat(AbilityInstance instance, string fieldName)
    {
        return ReadFloat((Actor)instance, fieldName);
    }

    private static float ReadFloat(Actor actor, string fieldName)
    {
        FieldInfo field = GetTypedField(
            actor.GetType(),
            fieldName,
            typeof(float),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return ReadFloat(actor, field);
    }

    private static float ReadFloat(Actor actor, FieldInfo field)
    {
        if (field == null)
        {
            return 0f;
        }

        try
        {
            if (field.GetValue(actor) is float value &&
                value > 0f &&
                !float.IsNaN(value) &&
                !float.IsInfinity(value))
            {
                return value;
            }
        }
        catch (Exception)
        {
            // Optional fields vary by game version.
        }

        return 0f;
    }

    private static bool ReadBool(AbilityInstance instance, string fieldName)
    {
        return ReadBool((Actor)instance, fieldName);
    }

    private static bool ReadBool(Actor actor, string fieldName)
    {
        try
        {
            FieldInfo field = GetTypedField(
                actor.GetType(),
                fieldName,
                typeof(bool),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field?.GetValue(actor) is bool value && value;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TryReadVector2(AbilityInstance instance, string fieldName, out Vector2 value)
    {
        value = Vector2.zero;
        try
        {
            FieldInfo field = GetTypedField(
                instance.GetType(),
                fieldName,
                typeof(Vector2),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field?.GetValue(instance) is Vector2 fieldValue)
            {
                value = fieldValue;
                return true;
            }
        }
        catch (Exception)
        {
            // Optional fields vary by game version.
        }

        return false;
    }

    private static bool TryReadVector3(AbilityInstance instance, FieldInfo field, out Vector3 value)
    {
        value = Vector3.zero;
        try
        {
            if (field.GetValue(instance) is Vector3 fieldValue)
            {
                value = fieldValue;
                return true;
            }
        }
        catch (Exception)
        {
            // Optional fields vary by game version.
        }

        return false;
    }

    private static bool TryReadVector3(AbilityInstance instance, string fieldName, out Vector3 value)
    {
        value = Vector3.zero;
        FieldInfo field = GetTypedField(
            instance.GetType(),
            fieldName,
            typeof(Vector3),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return field != null && TryReadVector3(instance, field, out value);
    }

    private static bool TryReadQuaternion(AbilityInstance instance, string fieldName, out Quaternion value)
    {
        value = Quaternion.identity;
        try
        {
            FieldInfo field = GetTypedField(
                instance.GetType(),
                fieldName,
                typeof(Quaternion),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field?.GetValue(instance) is Quaternion fieldValue)
            {
                value = fieldValue;
                return true;
            }
        }
        catch (Exception)
        {
            // Optional fields vary by game version.
        }

        return false;
    }

    private void CollectProjectileThreat(
        Hero hero,
        PluginConfig config,
        Projectile projectile,
        List<ThreatZone> results,
        float scanRange)
    {
        if (projectile.isCompleted)
        {
            return;
        }

        Entity caster = ResolveCaster(projectile);
        if (!IsHostileSource(caster, hero, config.DrawUnknownSourceThreats) ||
            !CanProjectileHitHero(projectile, caster, hero))
        {
            return;
        }

        if (!TryGetProjectilePath(projectile, config, out Vector3 origin, out Vector3 direction, out float length, out Vector3 end))
        {
            return;
        }

        float extendedScanRange = Mathf.Max(scanRange, config.ProjectileLookAheadDistance + 12f);
        float projectileSpeed = EstimateProjectileSpeed(projectile);
        float projectileWidth = projectile.collisionRadius > 0.001f &&
                                !float.IsNaN(projectile.collisionRadius) &&
                                !float.IsInfinity(projectile.collisionRadius)
            ? projectile.collisionRadius * 2f
            : config.DefaultThreatLineWidth * 0.65f;
        ThreatZone path = ThreatZone.Line(
            projectile,
            null,
            projectile,
            origin,
            direction,
            length,
            projectileWidth,
            ThreatSourceKind.Projectile,
            ThreatActivity.Active,
            weight: 1.8f,
            EstimateProjectileTimeToImpact(projectile, hero, config, direction),
            isDodgeable: true,
            projectileSpeed);

        if (path.SignedDistance(hero.agentPosition, 0f) <= extendedScanRange)
        {
            results.Add(path);
            results.Add(ThreatZone.ProjectileWindow(path, origin, length));
        }

        float arrivalTime = EstimateProjectileArrivalTime(projectile, end);
        TryAddScriptedProjectileImpact(projectile, end, arrivalTime, hero, extendedScanRange, results);
        CollectColliders(projectile, _colliderBuffer);
        for (int i = 0; i < _colliderBuffer.Count; i++)
        {
            AddProjectedColliderThreat(
                projectile,
                _colliderBuffer[i],
                projectile.transform,
                end,
                Quaternion.LookRotation(direction),
                ThreatSourceKind.Projectile,
                ThreatActivity.Imminent,
                weight: 2.05f,
                arrivalTime,
                isDodgeable: true,
                hero,
                extendedScanRange,
                results);
        }
    }

    private static void TryAddScriptedProjectileImpact(
        Projectile projectile,
        Vector3 end,
        float arrivalTime,
        Hero hero,
        float scanRange,
        List<ThreatZone> results)
    {
        string radiusFieldName;
        switch (projectile.GetType().Name)
        {
            case "Ai_Mon_Sky_BossNyx_StarBuff_Projectile":
                radiusFieldName = "radius";
                break;
            case "Ai_Mon_Special_BossMaw_ShadowOverdrive_Projectile":
                radiusFieldName = "range";
                break;
            default:
                return;
        }

        FieldInfo radiusField = GetTypedField(
            projectile.GetType(),
            radiusFieldName,
            typeof(float),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        float radius = ReadFloat(projectile, radiusField);
        if (!IsPositiveSize(radius))
        {
            return;
        }

        ThreatZone threat = ThreatZone.Circle(
            projectile,
            null,
            end,
            radius,
            ThreatSourceKind.Projectile,
            ThreatActivity.Imminent,
            weight: 2.3f,
            arrivalTime,
            isDodgeable: true);
        if (threat.SignedDistance(hero.agentPosition, 0f) <= scanRange)
        {
            results.Add(threat);
        }
    }

    private void CollectColliders(Actor actor, List<DewCollider> destination)
    {
        destination.Clear();
        int instanceId = actor.GetInstanceID();
        float now = Time.unscaledTime;
        if (!_collidersByActor.TryGetValue(instanceId, out ColliderCacheEntry entry) ||
            !ReferenceEquals(entry.Actor, actor) ||
            now >= entry.RefreshTime ||
            HasMissingCollider(entry.Colliders))
        {
            entry = new ColliderCacheEntry
            {
                Actor = actor,
                Colliders = FindActorColliders(actor),
                RefreshTime = now + ColliderCacheRefreshInterval
            };
            _collidersByActor[instanceId] = entry;
        }

        for (int i = 0; i < entry.Colliders.Length; i++)
        {
            AddColliderOnce(entry.Colliders[i], destination);
        }
    }

    private static DewCollider[] FindActorColliders(Actor actor)
    {
        List<DewCollider> found = new List<DewCollider>(16);

        DewCollider[] childColliders = actor.GetComponentsInChildren<DewCollider>(includeInactive: true);
        for (int i = 0; i < childColliders.Length; i++)
        {
            AddUniqueCollider(childColliders[i], found);
        }

        FieldInfo[] fields = GetColliderFields(actor.GetType());
        for (int i = 0; i < fields.Length; i++)
        {
            try
            {
                object value = fields[i].GetValue(actor);
                if (value is DewCollider collider)
                {
                    AddUniqueCollider(collider, found);
                }
                else if (value is IEnumerable enumerable)
                {
                    foreach (object item in enumerable)
                    {
                        AddUniqueCollider(item as DewCollider, found);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[DewSuperSmart] Failed to inspect {actor.GetType().Name}.{fields[i].Name}: {exception.Message}");
            }
        }

        return found.ToArray();
    }

    private static void AddUniqueCollider(DewCollider collider, List<DewCollider> destination)
    {
        if (collider != null && !destination.Contains(collider))
        {
            destination.Add(collider);
        }
    }

    private static bool HasMissingCollider(DewCollider[] colliders)
    {
        if (colliders == null)
        {
            return true;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
            {
                return true;
            }
        }

        return false;
    }

    private void CleanupColliderCache()
    {
        float now = Time.unscaledTime;
        if (now < _nextColliderCacheCleanupTime)
        {
            return;
        }

        _nextColliderCacheCleanupTime = now + ColliderCacheCleanupInterval;
        _staleColliderCacheKeys.Clear();
        foreach (KeyValuePair<int, ColliderCacheEntry> pair in _collidersByActor)
        {
            if (pair.Value.Actor == null)
            {
                _staleColliderCacheKeys.Add(pair.Key);
            }
        }

        for (int i = 0; i < _staleColliderCacheKeys.Count; i++)
        {
            _collidersByActor.Remove(_staleColliderCacheKeys[i]);
        }
    }

    private void AddColliderOnce(DewCollider collider, List<DewCollider> destination)
    {
        if (collider != null && _seenColliders.Add(collider))
        {
            destination.Add(collider);
        }
    }

    private static FieldInfo[] GetColliderFields(Type type)
    {
        if (ColliderFieldCache.TryGetValue(type, out FieldInfo[] fields))
        {
            return fields;
        }

        List<FieldInfo> result = new List<FieldInfo>();
        Type cursor = type;
        while (cursor != null && cursor != typeof(Actor))
        {
            FieldInfo[] declared = cursor.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (int i = 0; i < declared.Length; i++)
            {
                Type fieldType = declared[i].FieldType;
                if (typeof(DewCollider).IsAssignableFrom(fieldType) ||
                    (fieldType.IsArray && typeof(DewCollider).IsAssignableFrom(fieldType.GetElementType())))
                {
                    result.Add(declared[i]);
                }
            }

            cursor = cursor.BaseType;
        }

        fields = result.ToArray();
        ColliderFieldCache[type] = fields;
        return fields;
    }

    private static void AddColliderThreat(
        Actor source,
        DewCollider collider,
        ThreatSourceKind sourceKind,
        ThreatActivity activity,
        float weight,
        float timeToImpact,
        bool isDodgeable,
        Hero hero,
        float scanRange,
        List<ThreatZone> results)
    {
        ThreatZone threat = BuildColliderThreat(
            source,
            collider,
            sourceKind,
            activity,
            weight,
            timeToImpact,
            isDodgeable,
            worldPoint => worldPoint);

        if (threat.SignedDistance(hero.agentPosition, 0f) <= scanRange)
        {
            results.Add(threat);
        }
    }

    private static void AddProjectedColliderThreat(
        Actor source,
        DewCollider collider,
        Transform sourceRoot,
        Vector3 rootPosition,
        Quaternion rootRotation,
        ThreatSourceKind sourceKind,
        ThreatActivity activity,
        float weight,
        float timeToImpact,
        bool isDodgeable,
        Hero hero,
        float scanRange,
        List<ThreatZone> results)
    {
        Vector3 Project(Vector3 worldPoint)
        {
            return MonsterRushProjection.Project(sourceRoot, worldPoint, rootPosition, rootRotation);
        }

        ThreatZone threat = BuildColliderThreat(
            source,
            collider,
            sourceKind,
            activity,
            weight,
            timeToImpact,
            isDodgeable,
            Project);

        if (threat.SignedDistance(hero.agentPosition, 0f) <= scanRange)
        {
            results.Add(threat);
        }
    }

    private static ThreatZone BuildColliderThreat(
        Actor source,
        DewCollider collider,
        ThreatSourceKind sourceKind,
        ThreatActivity activity,
        float weight,
        float timeToImpact,
        bool isDodgeable,
        Func<Vector3, Vector3> projectPoint)
    {
        Transform transform = collider.transform;
        switch (collider.shape)
        {
            case DewCollider.ColliderShape.Circle:
                Vector3 center = transform.TransformPoint(new Vector3(collider.offset.x, 0f, collider.offset.y));
                return ThreatZone.Circle(
                    source,
                    null,
                    projectPoint(center),
                    Mathf.Max(collider.radius * GetMaxFlatScale(transform), MinimumShapeSize),
                    sourceKind,
                    activity,
                    weight,
                    timeToImpact,
                    isDodgeable);
            case DewCollider.ColliderShape.Box:
                return ThreatZone.Polygon(
                    source,
                    BuildBoxPoints(collider, projectPoint),
                    sourceKind,
                    activity,
                    weight,
                    timeToImpact,
                    isDodgeable);
            case DewCollider.ColliderShape.Polygon:
                return ThreatZone.Polygon(
                    source,
                    BuildPolygonPoints(collider, projectPoint),
                    sourceKind,
                    activity,
                    weight,
                    timeToImpact,
                    isDodgeable);
            default:
                return default;
        }
    }

    private static Vector3[] BuildBoxPoints(DewCollider collider, Func<Vector3, Vector3> projectPoint)
    {
        Vector2 half = collider.size * 0.5f;
        Vector2 offset = collider.offset;
        Transform transform = collider.transform;
        return new[]
        {
            projectPoint(transform.TransformPoint(new Vector3(offset.x - half.x, 0f, offset.y - half.y))),
            projectPoint(transform.TransformPoint(new Vector3(offset.x + half.x, 0f, offset.y - half.y))),
            projectPoint(transform.TransformPoint(new Vector3(offset.x + half.x, 0f, offset.y + half.y))),
            projectPoint(transform.TransformPoint(new Vector3(offset.x - half.x, 0f, offset.y + half.y)))
        };
    }

    private static Vector3[] BuildPolygonPoints(DewCollider collider, Func<Vector3, Vector3> projectPoint)
    {
        if (collider.points == null || collider.points.Length < 3)
        {
            Vector3 center = projectPoint(collider.transform.position);
            float fallback = MinimumShapeSize * 0.5f;
            return new[]
            {
                center + new Vector3(-fallback, 0f, -fallback),
                center + new Vector3(fallback, 0f, -fallback),
                center + new Vector3(fallback, 0f, fallback),
                center + new Vector3(-fallback, 0f, fallback)
            };
        }

        Vector3[] points = new Vector3[collider.points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 point = collider.points[i];
            points[i] = projectPoint(collider.transform.TransformPoint(new Vector3(point.x, 0f, point.y)));
        }

        return points;
    }

    private static bool TryGetUsableTrigger(
        Hero hero,
        Monster monster,
        AbilityTrigger trigger,
        out TriggerConfig triggerConfig,
        out bool isReady)
    {
        triggerConfig = null;
        isReady = false;
        if (trigger == null || trigger.IsNullOrInactive() || trigger.owner != monster)
        {
            return false;
        }

        triggerConfig = trigger.currentConfig;
        if (triggerConfig == null || !triggerConfig.isActive || triggerConfig.castMethod == null)
        {
            return false;
        }

        try
        {
            if (triggerConfig.targetValidator != null && !triggerConfig.targetValidator.Evaluate(monster, hero))
            {
                return false;
            }
        }
        catch (Exception)
        {
            return false;
        }

        try
        {
            isReady = trigger.Network_isCasting || trigger.CanBeReserved();
        }
        catch (Exception)
        {
            isReady = trigger.Network_isCasting;
        }

        return true;
    }

    private static bool IsHostileOrEnvironmental(AbilityInstance instance, Entity caster, Hero hero, bool allowUnknown)
    {
        if (instance is StatusEffect status)
        {
            if (status.victim == null || status.victim == hero)
            {
                return false;
            }

            if (caster != null)
            {
                return IsHostileSource(caster, hero, allowUnknown);
            }

            return !status.victim.IsNullOrInactive() && status.victim.CheckEnemyOrNeutral(hero);
        }

        if (caster != null)
        {
            return IsHostileSource(caster, hero, allowUnknown);
        }

        return IsEnvironmentalAbility(instance) || allowUnknown && instance is DamageInstance;
    }

    private static bool IsHostileSource(Entity caster, Hero hero, bool allowUnknown)
    {
        if (caster == null)
        {
            return allowUnknown;
        }

        return !caster.IsNullOrInactive() && caster.CheckEnemyOrNeutral(hero);
    }

    private static bool IsEnvironmentalAbility(AbilityInstance instance)
    {
        string name = instance.GetType().Name;
        return name.IndexOf("Trap", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("RoomMod", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Punishment", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.StartsWith("Ai_Mon_", StringComparison.Ordinal);
    }

    private static bool IsRoomHazardAbility(AbilityInstance instance)
    {
        string name = instance.GetType().Name;
        return name.StartsWith("Ai_RoomMod_", StringComparison.Ordinal) ||
               name.IndexOf("Punishment", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsMeteorRainImpact(AbilityInstance instance)
    {
        string name = instance.GetType().Name;
        return name.IndexOf("Meteor", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Starfall", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("StarRain", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("RainFire_Damage", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool CanInstanceAffectHero(AbilityInstance instance, Entity caster, Hero hero)
    {
        if (caster == null || instance is not DamageInstance damage || damage.hittable == null)
        {
            return true;
        }

        try
        {
            return damage.hittable.Evaluate(caster, hero);
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static bool CanProjectileHitHero(Projectile projectile, Entity caster, Hero hero)
    {
        if (caster == null || projectile.collisionTargets == null)
        {
            return true;
        }

        try
        {
            return projectile.collisionTargets.Evaluate(caster, hero);
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static Entity ResolveCaster(AbilityInstance instance)
    {
        if (instance.info.caster != null)
        {
            return instance.info.caster;
        }

        Actor cursor = instance.parentActor;
        int depth = 0;
        while (cursor != null && depth++ < 32)
        {
            if (cursor is Entity entity)
            {
                return entity;
            }

            if (cursor is AbilityTrigger trigger && trigger.owner != null)
            {
                return trigger.owner;
            }

            if (cursor is AbilityInstance parentInstance && parentInstance.info.caster != null)
            {
                return parentInstance.info.caster;
            }

            cursor = cursor.parentActor;
        }

        return null;
    }

    private static CastInfo GetThreatCastInfo(AbilityTrigger trigger, Hero hero, float predictionStrength)
    {
        try
        {
            return trigger.GetPredictedCastInfoToTarget(hero, Mathf.Clamp01(predictionStrength));
        }
        catch (Exception)
        {
            try
            {
                return trigger.GetCastInfoToTarget(hero);
            }
            catch (Exception)
            {
                return new CastInfo(trigger.owner, CastInfo.GetAngle(hero.agentPosition - trigger.owner.agentPosition));
            }
        }
    }

    private static Vector3 GetThreatDirection(Vector3 origin, Vector3 heroPosition, CastInfo castInfo)
    {
        Vector3 direction = castInfo.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = heroPosition - origin;
            direction.y = 0f;
        }

        return direction.sqrMagnitude <= 0.0001f ? Vector3.forward : direction.normalized;
    }

    private static bool IsTargetingHero(Monster monster, Hero hero)
    {
        return monster.Control == null || monster.Control.attackTarget == null || monster.Control.attackTarget == hero;
    }

    private static float GetRemainingChannelTime(
        Monster monster,
        AbilityTrigger trigger,
        TriggerConfig triggerConfig,
        float observedCastStartTime)
    {
        float expectedDuration = triggerConfig.channel != null
            ? Mathf.Max(triggerConfig.channel.duration * trigger.GetChannelDurationMultiplier(), 0f)
            : 0f;
        float remaining = float.PositiveInfinity;
        float bestDurationDelta = float.PositiveInfinity;
        if (monster.Control != null && monster.Control.ongoingChannels != null)
        {
            for (int i = 0; i < monster.Control.ongoingChannels.Count; i++)
            {
                Channel channel = monster.Control.ongoingChannels[i];
                if (channel == null || !channel.isAlive || (trigger is AttackTrigger) != channel.isAttack)
                {
                    continue;
                }

                float durationDelta = Mathf.Abs(channel.duration - expectedDuration);
                float channelRemaining = Mathf.Max(channel.duration - channel.elapsedTime, 0f);
                if (durationDelta < bestDurationDelta - 0.001f ||
                    Mathf.Approximately(durationDelta, bestDurationDelta) && channelRemaining < remaining)
                {
                    bestDurationDelta = durationDelta;
                    remaining = channelRemaining;
                }
            }
        }

        if (!float.IsPositiveInfinity(remaining))
        {
            return remaining;
        }

        return Mathf.Max(expectedDuration - Mathf.Max(Time.time - observedCastStartTime, 0f), 0f);
    }

    private static float GetLineWidth(TriggerConfig triggerConfig, PluginConfig config)
    {
        float width = triggerConfig.castMethod.arrowData.width;
        if (triggerConfig.spawnedInstance is Projectile projectile)
        {
            width = Mathf.Max(width, projectile.collisionRadius * 2f);
        }

        return Positive(width, config.DefaultThreatLineWidth);
    }

    private static bool TryGetProjectilePath(
        Projectile projectile,
        PluginConfig config,
        out Vector3 origin,
        out Vector3 direction,
        out float length,
        out Vector3 end)
    {
        origin = projectile.position;
        end = projectile.Network_targetPosition;
        if (projectile.Network_entityMode && projectile.Network_targetEntity != null)
        {
            end = projectile.Network_targetEntity.position;
        }

        Vector3 delta = end - origin;
        delta.y = 0f;
        if (Dew.IsOkay(end) && end != Vector3.zero && delta.sqrMagnitude > 0.0001f)
        {
            direction = delta.normalized;
            length = delta.magnitude;
            return true;
        }

        direction = projectile.info.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = projectile.transform.forward;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.forward;
        }

        direction.Normalize();
        length = Mathf.Max(
            IsPositiveSize(projectile.endDistance) ? projectile.endDistance : 0f,
            Mathf.Max(config.ProjectileLookAheadDistance, 1f));
        end = origin + direction * length;
        return true;
    }

    private static float EstimateProjectileTimeToImpact(Projectile projectile, Hero hero, PluginConfig config, Vector3 direction)
    {
        Vector2 direction2 = direction.ToXY().normalized;
        Vector2 delta = hero.agentPosition.ToXY() - projectile.position.ToXY();
        float along = Vector2.Dot(delta, direction2);
        float hitRadius = Mathf.Max(projectile.collisionRadius, 0f) +
                          (hero.Control != null ? hero.Control.outerRadius : 0.45f) +
                          Mathf.Max(config.ThreatPadding, 0f);
        float perpendicularSqr = (delta - direction2 * along).sqrMagnitude;

        if (perpendicularSqr > hitRadius * hitRadius || along < -hitRadius)
        {
            return float.PositiveInfinity;
        }

        float entryDistance = Mathf.Max(along - Mathf.Sqrt(Mathf.Max(hitRadius * hitRadius - perpendicularSqr, 0f)), 0f);
        float speed = EstimateProjectileSpeed(projectile);
        return speed > 0.001f ? entryDistance / speed : float.PositiveInfinity;
    }

    private static float EstimateProjectileArrivalTime(Projectile projectile, Vector3 target)
    {
        float distance = Vector2.Distance(projectile.position.ToXY(), target.ToXY());
        float speed = EstimateProjectileSpeed(projectile);
        return speed > 0.001f ? distance / speed : float.PositiveInfinity;
    }

    private static float EstimateProjectileSpeed(Projectile projectile)
    {
        try
        {
            if (ProjectileEstimatedVelocityField?.GetValue(projectile) is Vector3 velocity)
            {
                velocity.y = 0f;
                if (velocity.magnitude > 0.01f)
                {
                    return velocity.magnitude;
                }
            }
        }
        catch (Exception)
        {
            // Use position-based estimation below.
        }

        float age = Time.time - projectile.creationTime;
        if (age > 0.05f)
        {
            float travelled = Vector2.Distance(projectile.position.ToXY(), projectile.Network_startPosition.ToXY());
            if (travelled > 0.05f)
            {
                return travelled / age;
            }
        }

        return FallbackProjectileSpeed;
    }

    private static float EstimateAbilityTimeToImpact(AbilityInstance instance)
    {
        float age = Mathf.Max(Time.time - instance.creationTime, 0f);
        if (instance is InstantDamageInstance instant)
        {
            return RemainingDelay(instant.damageDelay, age);
        }

        if (instance is TickDamageInstance tick)
        {
            return tick.doneTicks <= 0 ? RemainingDelay(tick.delay, age) : 0f;
        }

        FieldInfo[] fields = GetDelayFields(instance.GetType());
        float delay = 0f;
        for (int i = 0; i < fields.Length; i++)
        {
            try
            {
                if (fields[i].GetValue(instance) is float value && IsPositiveSize(value))
                {
                    delay = Mathf.Max(delay, value);
                }
            }
            catch (Exception)
            {
                // Ignore optional version-specific fields.
            }
        }

        return RemainingDelay(delay, age);
    }

    private static FieldInfo[] GetDelayFields(Type type)
    {
        if (DelayFieldCache.TryGetValue(type, out FieldInfo[] fields))
        {
            return fields;
        }

        List<FieldInfo> result = new List<FieldInfo>();
        for (int i = 0; i < DelayFieldNames.Length; i++)
        {
            FieldInfo field = type.GetField(DelayFieldNames[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(float))
            {
                result.Add(field);
            }
        }

        fields = result.ToArray();
        DelayFieldCache[type] = fields;
        return fields;
    }

    private static float RemainingDelay(float delay, float age)
    {
        return IsPositiveSize(delay) ? Mathf.Max(delay - age, 0f) : 0f;
    }

    private static float GetMaxFlatScale(Transform transform)
    {
        Vector3 scale = transform.lossyScale;
        return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z), 0.01f);
    }

    private static float Positive(float first, float second)
    {
        if (IsPositiveSize(first))
        {
            return first;
        }

        return IsPositiveSize(second) ? second : MinimumShapeSize;
    }

    private static float Positive(float first, float second, float third)
    {
        if (IsPositiveSize(first))
        {
            return first;
        }

        if (IsPositiveSize(second))
        {
            return second;
        }

        return IsPositiveSize(third) ? third : MinimumShapeSize;
    }

    private static bool IsPositiveSize(float value)
    {
        return value > MinimumShapeSize && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
