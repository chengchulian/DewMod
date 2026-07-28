using System;
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
    private const float GlobalObjectScanInterval = 0.5f;
    private static readonly string[] DelayFieldNames =
    {
        "damageDelay",
        "explodeDelay",
        "impactDelay",
        "hitDelay",
        "telegraphTime",
        "delay",
        "startDelay",
        "initialDelay"
    };

    private static readonly string[] PendingDamageTypeSuffixes =
    {
        "_Damage",
        "_Instance",
        "_Explosion",
        "_AoE",
        "_Aoe",
        "_Attack",
        "_Atk",
        "_DelayedExplosion",
        "_DelayedAtk",
        "_SubDamage"
    };

    private static readonly Type DewColliderType = typeof(DewCollider);
    private static readonly Dictionary<Type, FieldInfo> RangeFieldCache = new Dictionary<Type, FieldInfo>();
    private static readonly Dictionary<Type, FieldInfo[]> DelayFieldCache = new Dictionary<Type, FieldInfo[]>();
    private static readonly Dictionary<Type, BeamFieldSet> BeamFieldCache = new Dictionary<Type, BeamFieldSet>();
    private static readonly Dictionary<Type, DamageInstance> PendingDamagePrefabCache = new Dictionary<Type, DamageInstance>();
    private static readonly FieldInfo ProjectileEstimatedVelocityField = typeof(Projectile).GetField("_estimatedVelocity", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly HashSet<Projectile> _seenProjectiles = new HashSet<Projectile>();
    private readonly HashSet<AbilityInstance> _seenAbilityInstances = new HashSet<AbilityInstance>();

    private float _nextProjectileGlobalObjectScanTime = float.NegativeInfinity;
    private float _nextAbilityInstanceGlobalObjectScanTime = float.NegativeInfinity;

    private sealed class BeamFieldSet
    {
        public FieldInfo BeamRadius;
        public FieldInfo HitBoxLength;
        public FieldInfo DistanceCurve;
        public FieldInfo StartOffset;
        public FieldInfo BeamDuration;
        public FieldInfo NetworkBeamDuration;
        public FieldInfo BeamStartTime;

        public bool IsUsable => BeamRadius != null && DistanceCurve != null;
    }

    private readonly struct BeamThreatGeometry
    {
        public readonly Vector3 Origin;
        public readonly Vector3 Direction;
        public readonly float Length;
        public readonly float Width;
        public readonly float TimeToImpact;

        public BeamThreatGeometry(Vector3 origin, Vector3 direction, float length, float width, float timeToImpact)
        {
            Origin = origin;
            Direction = direction;
            Length = length;
            Width = width;
            TimeToImpact = timeToImpact;
        }
    }

    public void CollectThreats(Hero hero, PluginConfig config, List<ThreatZone> results, bool forAutoDodge)
    {
        results.Clear();

        if (hero == null || hero.IsNullInactiveDeadOrKnockedOut() || config == null)
        {
            return;
        }

        ActorManager manager = NetworkedManagerBase<ActorManager>.softInstance;
        if (manager == null)
        {
            return;
        }

        bool includeMonsterThreats = forAutoDodge || config.ShowMonsterThreatRanges;
        bool includeProjectiles = forAutoDodge || config.ShowProjectileThreatRanges;
        bool includeReleasedInstances = forAutoDodge || config.ShowMonsterThreatRanges;
        bool readyOnly = forAutoDodge ? config.AutoDodgeReadyThreatsOnly : config.DrawReadyThreatsOnly;
        bool allowUnknownThreats = forAutoDodge || config.DrawUnknownSourceThreats;
        float scanRange = Mathf.Max(config.ThreatScanRange, 1f);

        if (includeMonsterThreats)
        {
            CollectMonsterThreats(hero, config, manager, results, readyOnly, scanRange);
        }

        if (includeProjectiles)
        {
            CollectProjectileThreats(hero, config, manager, results, scanRange, allowUnknownThreats);
        }

        if (includeReleasedInstances)
        {
            CollectReleasedAbilityInstanceThreats(hero, config, manager, results, scanRange, allowUnknownThreats);
        }
    }

    private static void CollectMonsterThreats(
        Hero hero,
        PluginConfig config,
        ActorManager manager,
        List<ThreatZone> results,
        bool readyOnly,
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

            foreach (KeyValuePair<int, AbilityTrigger> pair in monster.Ability.abilities)
            {
                AddTriggerThreat(hero, monster, pair.Value, config, results, readyOnly, scanRange);
            }
        }
    }

    private static void AddTriggerThreat(
        Hero hero,
        Monster monster,
        AbilityTrigger trigger,
        PluginConfig config,
        List<ThreatZone> results,
        bool readyOnly,
        float scanRange)
    {
        if (!CanUseThreatTrigger(hero, monster, trigger, readyOnly, out TriggerConfig triggerConfig, out bool isReady))
        {
            return;
        }

        CastInfo castInfo = GetThreatCastInfo(trigger, hero, config.ThreatPredictionStrength);
        CastMethodData method = triggerConfig.castMethod;
        Vector3 origin = monster.agentPosition;
        Vector3 direction = GetThreatDirection(origin, hero.agentPosition, castInfo);
        float weight = trigger is AttackTrigger ? 1f : 1.35f;
        if (trigger.Network_isCasting)
        {
            weight += 0.65f;
        }

        float timeToImpact = EstimateTriggerTimeToImpact(monster, trigger, triggerConfig, isReady);

        switch (method.type)
        {
            case CastMethodType.None:
                AddThreatIfInScanRange(results, ThreatZone.Circle(
                    monster,
                    trigger,
                    origin,
                    Positive(method.noneData.radius, triggerConfig.effectiveRange, config.DefaultThreatAreaRadius),
                    isReady,
                    weight,
                    timeToImpact), hero.agentPosition, scanRange);
                break;
            case CastMethodType.Cone:
                AddThreatIfInScanRange(results, ThreatZone.Cone(
                    monster,
                    trigger,
                    origin,
                    direction,
                    Positive(method.coneData.radius, triggerConfig.effectiveRange, config.DefaultThreatAreaRadius),
                    Positive(method.coneData.angle, DefaultConeAngle),
                    isReady,
                    weight,
                    timeToImpact), hero.agentPosition, scanRange);
                break;
            case CastMethodType.Arrow:
                AddThreatIfInScanRange(results, ThreatZone.Line(
                    monster,
                    trigger,
                    origin,
                    direction,
                    Positive(method.arrowData.length, triggerConfig.effectiveRange, config.AutoDodgeFallbackDistance),
                    GetLineWidth(triggerConfig, config),
                    isReady,
                    weight,
                    timeToImpact), hero.agentPosition, scanRange);
                break;
            case CastMethodType.Target:
                AddThreatIfInScanRange(results, ThreatZone.Circle(
                    monster,
                    trigger,
                    origin,
                    Positive(method.targetData.range, triggerConfig.effectiveRange, config.DefaultThreatAreaRadius),
                    isReady,
                    weight,
                    timeToImpact), hero.agentPosition, scanRange);
                break;
            case CastMethodType.Point:
                Vector3 point = castInfo.point;
                if (!Dew.IsOkay(point) || point == Vector3.zero)
                {
                    point = origin + direction * Positive(method.pointData.range, triggerConfig.effectiveRange, config.AutoDodgeFallbackDistance);
                }

                AddThreatIfInScanRange(results, ThreatZone.Circle(
                    monster,
                    trigger,
                    Dew.GetPositionOnGround(point),
                    Positive(method.pointData.radius, config.DefaultThreatAreaRadius),
                    isReady,
                    weight,
                    timeToImpact), hero.agentPosition, scanRange);
                break;
        }
    }

    private static bool CanUseThreatTrigger(
        Hero hero,
        Monster monster,
        AbilityTrigger trigger,
        bool readyOnly,
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
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }

        try
        {
            isReady = trigger.Network_isCasting || trigger.CanBeReserved();
        }
        catch (Exception)
        {
            isReady = false;
        }

        return !readyOnly || isReady;
    }

    private static void AddThreatIfInScanRange(List<ThreatZone> results, ThreatZone threat, Vector3 heroPosition, float scanRange)
    {
        if (threat.SignedDistance(heroPosition, 0f) <= scanRange)
        {
            results.Add(threat);
        }
    }

    private void CollectProjectileThreats(
        Hero hero,
        PluginConfig config,
        ActorManager manager,
        List<ThreatZone> results,
        float scanRange,
        bool allowUnknownThreats)
    {
        float projectileScanRange = Mathf.Max(Mathf.Max(scanRange, config.ProjectileLookAheadDistance + 24f), 48f);
        float sqrScanRange = projectileScanRange * projectileScanRange;
        _seenProjectiles.Clear();

        foreach (Actor actor in manager.allActors)
        {
            TryAddProjectileThreat(hero, config, actor as Projectile, results, sqrScanRange, _seenProjectiles, allowUnknownThreats);
        }

        foreach (Actor actor in manager.allActorsBeingDestroyed)
        {
            TryAddProjectileThreat(hero, config, actor as Projectile, results, sqrScanRange, _seenProjectiles, allowUnknownThreats);
        }

        if (!ShouldRunGlobalObjectScan(ref _nextProjectileGlobalObjectScanTime))
        {
            return;
        }

        foreach (Projectile projectile in UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            TryAddProjectileThreat(hero, config, projectile, results, sqrScanRange, _seenProjectiles, allowUnknownThreats);
        }
    }

    private static void TryAddProjectileThreat(
        Hero hero,
        PluginConfig config,
        Projectile projectile,
        List<ThreatZone> results,
        float sqrScanRange,
        HashSet<Projectile> seen,
        bool allowUnknownThreats)
    {
        if (projectile == null ||
            !seen.Add(projectile) ||
            projectile.IsNullOrInactive() ||
            projectile.isCompleted ||
            SqrFlatDistance(projectile.position, hero.agentPosition) > sqrScanRange)
        {
            return;
        }

        Entity caster = ResolveCaster(projectile);
        if (!IsThreatSourceForHero(caster, hero, allowUnknownThreats) ||
            !CanProjectileHitHero(projectile, caster, hero, allowUnknownThreats))
        {
            return;
        }

        if (!TryGetProjectilePath(projectile, hero, config, out Vector3 origin, out Vector3 direction, out float length))
        {
            return;
        }

        float width = Mathf.Max(projectile.collisionRadius * 2f, config.DefaultThreatLineWidth * 0.65f);
        float timeToImpact = EstimateProjectileTimeToImpact(projectile, hero, config, direction);
        results.Add(ThreatZone.ProjectileLine(projectile, caster, origin, direction, length, width, 1.6f, timeToImpact));

        if (TryGetInstanceRange(projectile, out DewCollider impactRange) &&
            TryGetProjectileTargetPosition(projectile, out Vector3 impactPoint))
        {
            float impactTime = EstimateProjectileArrivalTime(projectile, impactPoint);
            AddProjectedColliderThreat(caster, impactRange, projectile.transform, impactPoint, projectile.transform.rotation, config, results, impactTime, 1.85f);
        }
    }

    private void CollectReleasedAbilityInstanceThreats(
        Hero hero,
        PluginConfig config,
        ActorManager manager,
        List<ThreatZone> results,
        float scanRange,
        bool allowUnknownThreats)
    {
        float releasedScanRange = Mathf.Max(Mathf.Max(scanRange, config.ProjectileLookAheadDistance + 12f), 36f);
        _seenAbilityInstances.Clear();

        foreach (Actor actor in manager.allActors)
        {
            TryAddReleasedAbilityInstanceThreat(hero, config, actor as AbilityInstance, results, releasedScanRange, _seenAbilityInstances, allowUnknownThreats);
        }

        foreach (Actor actor in manager.allActorsBeingDestroyed)
        {
            TryAddReleasedAbilityInstanceThreat(hero, config, actor as AbilityInstance, results, releasedScanRange, _seenAbilityInstances, allowUnknownThreats);
        }

        if (!ShouldRunGlobalObjectScan(ref _nextAbilityInstanceGlobalObjectScanTime))
        {
            return;
        }

        foreach (AbilityInstance instance in UnityEngine.Object.FindObjectsByType<AbilityInstance>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            TryAddReleasedAbilityInstanceThreat(hero, config, instance, results, releasedScanRange, _seenAbilityInstances, allowUnknownThreats);
        }
    }

    private static bool ShouldRunGlobalObjectScan(ref float nextScanTime)
    {
        float now = Time.unscaledTime;
        if (now < nextScanTime)
        {
            return false;
        }

        nextScanTime = now + GlobalObjectScanInterval;
        return true;
    }

    private static void TryAddReleasedAbilityInstanceThreat(
        Hero hero,
        PluginConfig config,
        AbilityInstance instance,
        List<ThreatZone> results,
        float scanRange,
        HashSet<AbilityInstance> seen,
        bool allowUnknownThreats)
    {
        if (instance == null ||
            !seen.Add(instance) ||
            instance is Projectile ||
            instance.IsNullOrInactive())
        {
            return;
        }

        bool hasRange = TryGetInstanceRange(instance, out DewCollider range);
        bool isDamageInstance = instance is DamageInstance;
        bool hasBeamThreat = TryGetSweepingBeamThreat(instance, out BeamThreatGeometry beamThreat);
        float timeToImpact = EstimateAbilityInstanceTimeToImpact(instance, out bool hasConfiguredDelay);
        DamageInstance pendingDamage = null;
        DewCollider pendingRange = null;
        bool hasPendingDamageRange = hasConfiguredDelay &&
                                     !hasRange &&
                                     !isDamageInstance &&
                                     TryGetPendingDamagePrefab(instance, out pendingDamage) &&
                                     TryGetInstanceRange(pendingDamage, out pendingRange);

        Entity caster = ResolveCaster(instance);
        if (!IsThreatSourceForHero(caster, hero, allowUnknownThreats) ||
            !CanReleasedInstanceHitHero(instance, caster, hero))
        {
            return;
        }

        if (hasBeamThreat)
        {
            ThreatZone threat = ThreatZone.Line(caster, null, beamThreat.Origin, beamThreat.Direction, beamThreat.Length, beamThreat.Width, isReady: true, weight: 1.85f, timeToImpact: beamThreat.TimeToImpact);
            if (threat.SignedDistance(hero.agentPosition, 0f) <= scanRange)
            {
                results.Add(threat);
            }
        }

        if (hasRange)
        {
            if (IsColliderInScanRange(range, hero, scanRange))
            {
                AddColliderThreat(caster, range, config, results, timeToImpact);
            }
            return;
        }

        if (isDamageInstance)
        {
            if (SqrFlatDistance(instance.position, hero.agentPosition) > scanRange * scanRange)
            {
                return;
            }

            results.Add(ThreatZone.Circle(
                caster,
                null,
                    instance.position,
                    Mathf.Max(config.DefaultThreatAreaRadius, 0.25f),
                    isReady: true,
                    weight: 1.8f,
                    timeToImpact: timeToImpact));
            return;
        }

        if (hasPendingDamageRange)
        {
            float projectedScanRange = scanRange + GetColliderScanRadius(pendingRange);
            if (SqrFlatDistance(instance.position, hero.agentPosition) <= projectedScanRange * projectedScanRange)
            {
                AddProjectedColliderThreat(caster, pendingRange, pendingDamage.transform, instance.position, instance.transform.rotation, config, results, timeToImpact, 1.85f);
            }

            return;
        }

        if (!hasBeamThreat)
        {
            return;
        }
    }

    private static bool TryGetInstanceRange(AbilityInstance instance, out DewCollider range)
    {
        range = null;

        if (instance is DamageInstance damageInstance && damageInstance.range != null)
        {
            range = damageInstance.range;
            return true;
        }

        Type type = instance.GetType();
        if (!RangeFieldCache.TryGetValue(type, out FieldInfo field))
        {
            field = type.GetField("range", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null || !DewColliderType.IsAssignableFrom(field.FieldType))
            {
                field = null;
            }

            RangeFieldCache[type] = field;
        }

        if (field == null)
        {
            return false;
        }

        range = field.GetValue(instance) as DewCollider;
        return range != null;
    }

    private static Entity ResolveCaster(AbilityInstance instance)
    {
        if (instance.info.caster != null)
        {
            return instance.info.caster;
        }

        Actor cursor = instance.parentActor;
        while (cursor != null)
        {
            switch (cursor)
            {
                case Entity entity:
                    return entity;
                case AbilityTrigger trigger when trigger.owner != null:
                    return trigger.owner;
                case AbilityInstance parentInstance when parentInstance.info.caster != null:
                    return parentInstance.info.caster;
            }

            cursor = cursor.parentActor;
        }

        return null;
    }

    private static void AddColliderThreat(Entity source, DewCollider range, PluginConfig config, List<ThreatZone> results, float timeToImpact = float.PositiveInfinity)
    {
        Vector3 center = range.transform.position;
        switch (range.shape)
        {
            case DewCollider.ColliderShape.Circle:
                results.Add(ThreatZone.Circle(
                    source,
                    null,
                    center,
                    Mathf.Max(range.radius * GetMaxFlatScale(range.transform), config.DefaultThreatAreaRadius * 0.5f),
                    isReady: true,
                    weight: 1.85f,
                    timeToImpact: timeToImpact));
                break;
            case DewCollider.ColliderShape.Box:
                AddBoxColliderThreat(source, range, config, results, timeToImpact);
                break;
            case DewCollider.ColliderShape.Polygon:
                results.Add(ThreatZone.Circle(
                    source,
                    null,
                    center,
                    Mathf.Max(GetPolygonRadius(range), config.DefaultThreatAreaRadius * 0.5f),
                    isReady: true,
                    weight: 1.85f,
                    timeToImpact: timeToImpact));
                break;
        }
    }

    private static void AddProjectedColliderThreat(
        Entity source,
        DewCollider range,
        Transform rangeRoot,
        Vector3 rootPosition,
        Quaternion rootRotation,
        PluginConfig config,
        List<ThreatZone> results,
        float timeToImpact,
        float weight)
    {
        Transform rangeTransform = range.transform;
        Vector3 localCenter = rangeRoot != null
            ? rangeRoot.InverseTransformPoint(rangeTransform.position)
            : rangeTransform.localPosition;
        Quaternion localRotation = rangeRoot != null
            ? Quaternion.Inverse(rangeRoot.rotation) * rangeTransform.rotation
            : rangeTransform.localRotation;
        Vector3 center = rootPosition + rootRotation * localCenter;

        switch (range.shape)
        {
            case DewCollider.ColliderShape.Circle:
                results.Add(ThreatZone.Circle(
                    source,
                    null,
                    center,
                    Mathf.Max(range.radius * GetMaxFlatScale(rangeTransform), config.DefaultThreatAreaRadius * 0.5f),
                    isReady: true,
                    weight: weight,
                    timeToImpact: timeToImpact));
                break;
            case DewCollider.ColliderShape.Box:
                AddProjectedBoxColliderThreat(source, range, center, rootRotation * localRotation, config, results, timeToImpact, weight);
                break;
            case DewCollider.ColliderShape.Polygon:
                results.Add(ThreatZone.Circle(
                    source,
                    null,
                    center,
                    Mathf.Max(GetPolygonRadius(range), config.DefaultThreatAreaRadius * 0.5f),
                    isReady: true,
                    weight: weight,
                    timeToImpact: timeToImpact));
                break;
        }
    }

    private static void AddProjectedBoxColliderThreat(
        Entity source,
        DewCollider range,
        Vector3 center,
        Quaternion rotation,
        PluginConfig config,
        List<ThreatZone> results,
        float timeToImpact,
        float weight)
    {
        Vector3 scale = range.transform.lossyScale;
        float width = Mathf.Max(Mathf.Abs(range.size.x * scale.x), config.DefaultThreatLineWidth);
        float length = Mathf.Max(Mathf.Abs(range.size.y * scale.z), config.DefaultThreatAreaRadius);
        Vector3 direction = rotation * Vector3.forward;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = source != null ? source.transform.forward : Vector3.forward;
        }

        Vector3 origin = center - direction.normalized * (length * 0.5f);
        results.Add(ThreatZone.Line(source, null, origin, direction, length, width, isReady: true, weight: weight, timeToImpact: timeToImpact));
    }

    private static bool TryGetSweepingBeamThreat(AbilityInstance instance, out BeamThreatGeometry threat)
    {
        threat = default;
        BeamFieldSet fields = GetBeamFields(instance.GetType());
        if (!fields.IsUsable ||
            !TryReadPositiveFloatField(instance, fields.BeamRadius, out float beamRadius) ||
            fields.DistanceCurve.GetValue(instance) is not AnimationCurve distanceCurve)
        {
            return false;
        }

        float duration = ReadPositiveFloatField(instance, fields.NetworkBeamDuration);
        if (duration <= 0f)
        {
            duration = ReadPositiveFloatField(instance, fields.BeamDuration);
        }

        float startTime = ReadPositiveFloatField(instance, fields.BeamStartTime);
        if (startTime <= 0f)
        {
            startTime = instance.creationTime;
        }

        float timeToImpact = Mathf.Max(startTime - Time.time, 0f);
        float normalizedTime = duration > 0.001f
            ? Mathf.Clamp01((Time.time - startTime) / duration)
            : 1f;
        float beamDistance = Mathf.Max(distanceCurve.Evaluate(normalizedTime), 0f);
        bool isFutureBeam = timeToImpact > 0.001f;
        if (isFutureBeam)
        {
            beamDistance = Mathf.Max(beamDistance, Mathf.Max(distanceCurve.Evaluate(1f), 0f));
        }

        if (beamDistance <= MinimumShapeSize)
        {
            return false;
        }

        float hitBoxLength = ReadPositiveFloatField(instance, fields.HitBoxLength);
        if (hitBoxLength <= 0f || isFutureBeam)
        {
            hitBoxLength = beamDistance;
        }

        Vector3 direction = NormalizeFlat(instance.info.forward);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = NormalizeFlat(instance.transform.forward);
        }

        Vector3 startPosition = GetBeamStartPosition(instance, fields);
        float startDistance = Mathf.Clamp(beamDistance - hitBoxLength, 0f, float.PositiveInfinity);
        float length = Mathf.Max(beamDistance - startDistance, MinimumShapeSize);
        threat = new BeamThreatGeometry(
            startPosition + direction * startDistance,
            direction,
            length,
            Mathf.Max(beamRadius * 2f, MinimumShapeSize),
            timeToImpact);
        return true;
    }

    private static BeamFieldSet GetBeamFields(Type type)
    {
        if (BeamFieldCache.TryGetValue(type, out BeamFieldSet fields))
        {
            return fields;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        fields = new BeamFieldSet
        {
            BeamRadius = GetTypedField(type, "beamRadius", typeof(float), flags),
            HitBoxLength = GetTypedField(type, "hitBoxLength", typeof(float), flags),
            DistanceCurve = GetTypedField(type, "distanceCurve", typeof(AnimationCurve), flags),
            StartOffset = GetTypedField(type, "startOffset", typeof(Vector3), flags),
            BeamDuration = GetTypedField(type, "beamDuration", typeof(float), flags),
            NetworkBeamDuration = GetTypedField(type, "_beamDuration", typeof(float), flags),
            BeamStartTime = GetTypedField(type, "_beamStartTime", typeof(float), flags)
        };
        BeamFieldCache[type] = fields;
        return fields;
    }

    private static FieldInfo GetTypedField(Type type, string name, Type fieldType, BindingFlags flags)
    {
        FieldInfo field = type.GetField(name, flags);
        return field != null && field.FieldType == fieldType ? field : null;
    }

    private static Vector3 GetBeamStartPosition(AbilityInstance instance, BeamFieldSet fields)
    {
        if (fields.StartOffset != null &&
            TryReadVector3Field(instance, fields.StartOffset, out Vector3 startOffset) &&
            instance.info.caster != null)
        {
            return instance.info.caster.position + instance.info.caster.rotation * startOffset;
        }

        return instance.position;
    }

    private static bool TryReadPositiveFloatField(AbilityInstance instance, FieldInfo field, out float value)
    {
        value = ReadPositiveFloatField(instance, field);
        return value > 0f;
    }

    private static float ReadPositiveFloatField(AbilityInstance instance, FieldInfo field)
    {
        if (field == null)
        {
            return 0f;
        }

        try
        {
            if (field.GetValue(instance) is float value &&
                value > 0f &&
                !float.IsNaN(value) &&
                !float.IsInfinity(value))
            {
                return value;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        return 0f;
    }

    private static bool TryReadVector3Field(AbilityInstance instance, FieldInfo field, out Vector3 value)
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
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        return false;
    }

    private static bool IsColliderInScanRange(DewCollider range, Hero hero, float scanRange)
    {
        float radius = GetColliderScanRadius(range);
        return Vector2.Distance(GetColliderWorldCenter(range).ToXY(), hero.agentPosition.ToXY()) <= scanRange + radius;
    }

    private static Vector3 GetColliderWorldCenter(DewCollider range)
    {
        Vector3 scale = range.transform.lossyScale;
        return range.transform.position + range.transform.rotation * new Vector3(range.offset.x * scale.x, 0f, range.offset.y * scale.z);
    }

    private static float GetColliderScanRadius(DewCollider range)
    {
        Vector3 scale = range.transform.lossyScale;
        switch (range.shape)
        {
            case DewCollider.ColliderShape.Circle:
                return Mathf.Max(range.radius * GetMaxFlatScale(range.transform), 0f);
            case DewCollider.ColliderShape.Box:
                float width = Mathf.Abs(range.size.x * scale.x);
                float length = Mathf.Abs(range.size.y * scale.z);
                return Mathf.Sqrt(width * width + length * length) * 0.5f;
            case DewCollider.ColliderShape.Polygon:
                return Mathf.Max(GetPolygonRadius(range), 0f);
            default:
                return 0f;
        }
    }

    private static void AddBoxColliderThreat(Entity source, DewCollider range, PluginConfig config, List<ThreatZone> results, float timeToImpact)
    {
        Transform transform = range.transform;
        Vector3 scale = transform.lossyScale;
        float width = Mathf.Max(Mathf.Abs(range.size.x * scale.x), config.DefaultThreatLineWidth);
        float length = Mathf.Max(Mathf.Abs(range.size.y * scale.z), config.DefaultThreatAreaRadius);
        Vector3 direction = transform.forward;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = source != null ? source.transform.forward : Vector3.forward;
        }

        Vector3 center = transform.position + transform.rotation * new Vector3(range.offset.x * scale.x, 0f, range.offset.y * scale.z);
        Vector3 origin = center - direction.normalized * (length * 0.5f);
        results.Add(ThreatZone.Line(source, null, origin, direction, length, width, isReady: true, weight: 1.85f, timeToImpact: timeToImpact));
    }

    private static float GetPolygonRadius(DewCollider range)
    {
        if (range.points == null || range.points.Length == 0)
        {
            return 0f;
        }

        Vector3 scale = range.transform.lossyScale;
        float max = 0f;
        for (int i = 0; i < range.points.Length; i++)
        {
            Vector2 point = range.points[i] + range.offset;
            float x = point.x * scale.x;
            float z = point.y * scale.z;
            max = Mathf.Max(max, Mathf.Sqrt(x * x + z * z));
        }

        return max;
    }

    private static float GetMaxFlatScale(Transform transform)
    {
        Vector3 scale = transform.lossyScale;
        return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z), 0.01f);
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
                Vector3 direction = hero.agentPosition - trigger.owner.agentPosition;
                return new CastInfo(trigger.owner, CastInfo.GetAngle(direction));
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

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return Vector3.forward;
        }

        return direction.normalized;
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

    private static float EstimateTriggerTimeToImpact(Monster monster, AbilityTrigger trigger, TriggerConfig triggerConfig, bool isReady)
    {
        if (trigger.Network_isCasting)
        {
            if (TryGetRemainingChannelTime(monster, out float remainingTime))
            {
                return remainingTime;
            }

            return triggerConfig.channel != null
                ? Mathf.Max(triggerConfig.channel.duration, 0f)
                : 0f;
        }

        return isReady ? float.PositiveInfinity : float.NaN;
    }

    private static bool TryGetRemainingChannelTime(Monster monster, out float remainingTime)
    {
        remainingTime = 0f;
        if (monster.Control == null || monster.Control.ongoingChannels == null || monster.Control.ongoingChannels.Count == 0)
        {
            return false;
        }

        remainingTime = float.PositiveInfinity;
        for (int i = 0; i < monster.Control.ongoingChannels.Count; i++)
        {
            Channel channel = monster.Control.ongoingChannels[i];
            if (channel == null || !channel.isAlive)
            {
                continue;
            }

            remainingTime = Mathf.Min(remainingTime, Mathf.Max(channel.duration - channel.elapsedTime, 0f));
        }

        return !float.IsPositiveInfinity(remainingTime);
    }

    private static float EstimateProjectileTimeToImpact(Projectile projectile, Hero hero, PluginConfig config, Vector3 direction)
    {
        Vector2 direction2 = NormalizeFlat(direction).ToXY();
        Vector2 delta = hero.agentPosition.ToXY() - projectile.position.ToXY();
        float along = Vector2.Dot(delta, direction2);

        float hitRadius = Mathf.Max(projectile.collisionRadius, 0f) +
                          (hero.Control != null ? hero.Control.outerRadius : 0.45f) +
                          Mathf.Max(config.ThreatPadding, 0f);
        float hitRadiusSqr = hitRadius * hitRadius;
        float perpendicularSqr = (delta - direction2 * along).sqrMagnitude;

        if (perpendicularSqr > hitRadiusSqr || along < -hitRadius)
        {
            return float.PositiveInfinity;
        }

        float entryDistance = Mathf.Max(along - Mathf.Sqrt(Mathf.Max(hitRadiusSqr - perpendicularSqr, 0f)), 0f);
        if (entryDistance <= 0.001f)
        {
            return 0f;
        }

        float speed = EstimateProjectileSpeed(projectile);
        return speed > 0.001f ? entryDistance / speed : float.PositiveInfinity;
    }

    private static float EstimateProjectileArrivalTime(Projectile projectile, Vector3 target)
    {
        float remainingDistance = Vector2.Distance(projectile.position.ToXY(), target.ToXY());
        if (remainingDistance <= 0.001f)
        {
            return 0f;
        }

        float speed = EstimateProjectileSpeed(projectile);
        return speed > 0.001f ? remainingDistance / speed : float.PositiveInfinity;
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
            // Fall through to public-position estimation.
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

    private static bool IsThreatSourceForHero(Entity source, Hero hero, bool allowUnknown)
    {
        if (source == null)
        {
            return allowUnknown;
        }

        return !source.IsNullOrInactive() && source.CheckEnemyOrNeutral(hero);
    }

    private static bool CanProjectileHitHero(Projectile projectile, Entity caster, Hero hero, bool allowUnknown)
    {
        if (caster == null)
        {
            return allowUnknown;
        }

        AbilityTargetValidator collisionTargets = projectile.collisionTargets;
        if (collisionTargets == null)
        {
            return true;
        }

        try
        {
            return collisionTargets.Evaluate(caster, hero);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return true;
        }
    }

    private static bool CanReleasedInstanceHitHero(AbilityInstance instance, Entity caster, Hero hero)
    {
        if (caster == null || instance is not DamageInstance damageInstance || damageInstance.hittable == null)
        {
            return true;
        }

        try
        {
            return damageInstance.hittable.Evaluate(caster, hero);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return true;
        }
    }

    private static bool TryGetProjectilePath(
        Projectile projectile,
        Hero hero,
        PluginConfig config,
        out Vector3 origin,
        out Vector3 direction,
        out float length)
    {
        origin = projectile.position;
        Vector3 target = projectile.Network_targetPosition;

        if (projectile.Network_entityMode && projectile.Network_targetEntity != null)
        {
            target = projectile.Network_targetEntity.position;
        }

        Vector3 fullDelta = target - projectile.Network_startPosition;
        fullDelta.y = 0f;
        if (fullDelta.sqrMagnitude > 0.0001f)
        {
            direction = fullDelta.normalized;
            Vector3 remainingDelta = target - origin;
            remainingDelta.y = 0f;
            length = Mathf.Max(Vector3.Dot(remainingDelta, direction), config.DefaultThreatAreaRadius);
            return true;
        }

        Vector3 delta = target - origin;
        delta.y = 0f;
        if (delta.sqrMagnitude > 0.0001f)
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
            direction = hero.agentPosition - projectile.position;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.forward;
        }

        direction = direction.normalized;
        float configuredDistance = Mathf.Max(config.ProjectileLookAheadDistance, 1f);
        float endDistance = projectile.endDistance;
        length = endDistance > MinimumShapeSize && !float.IsNaN(endDistance) && !float.IsInfinity(endDistance)
            ? Mathf.Max(configuredDistance, endDistance)
            : configuredDistance;
        return true;
    }

    private static bool TryGetProjectileTargetPosition(Projectile projectile, out Vector3 target)
    {
        target = projectile.Network_targetPosition;
        if (projectile.Network_entityMode && projectile.Network_targetEntity != null)
        {
            target = projectile.Network_targetEntity.position;
        }

        return Dew.IsOkay(target) && target != Vector3.zero;
    }

    private static float EstimateAbilityInstanceTimeToImpact(AbilityInstance instance, out bool hasConfiguredDelay)
    {
        hasConfiguredDelay = false;
        float age = Mathf.Max(Time.time - instance.creationTime, 0f);

        if (instance is InstantDamageInstance instantDamage)
        {
            hasConfiguredDelay = IsPositiveDelay(instantDamage.damageDelay);
            return RemainingDelay(instantDamage.damageDelay, age);
        }

        if (instance is TickDamageInstance tickDamage)
        {
            hasConfiguredDelay = IsPositiveDelay(tickDamage.delay);
            return tickDamage.doneTicks <= 0
                ? RemainingDelay(tickDamage.delay, age)
                : 0f;
        }

        if (TryGetConfiguredDelay(instance, out float configuredDelay))
        {
            hasConfiguredDelay = true;
            return RemainingDelay(configuredDelay, age);
        }

        return 0f;
    }

    private static float RemainingDelay(float delay, float age)
    {
        if (delay <= 0f || float.IsNaN(delay) || float.IsInfinity(delay))
        {
            return 0f;
        }

        return Mathf.Max(delay - age, 0f);
    }

    private static bool IsPositiveDelay(float delay)
    {
        return delay > 0f && !float.IsNaN(delay) && !float.IsInfinity(delay);
    }

    private static bool TryGetConfiguredDelay(AbilityInstance instance, out float delay)
    {
        delay = 0f;
        Type type = instance.GetType();
        FieldInfo[] fields = GetDelayFields(type);
        if (fields.Length == 0)
        {
            return false;
        }

        float initialDelay = 0f;
        float telegraphTime = 0f;
        float largestDelay = 0f;

        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            if (!TryGetPositiveFloatField(instance, field, out float value))
            {
                continue;
            }

            if (field.Name == "initialDelay" || field.Name == "startDelay")
            {
                initialDelay = Mathf.Max(initialDelay, value);
                continue;
            }

            if (field.Name == "telegraphTime")
            {
                telegraphTime = Mathf.Max(telegraphTime, value);
                continue;
            }

            largestDelay = Mathf.Max(largestDelay, value);
        }

        if (initialDelay > 0f || telegraphTime > 0f)
        {
            delay = initialDelay + telegraphTime;
            return delay > 0f;
        }

        delay = largestDelay;
        return delay > 0f;
    }

    private static FieldInfo[] GetDelayFields(Type type)
    {
        if (DelayFieldCache.TryGetValue(type, out FieldInfo[] fields))
        {
            return fields;
        }

        List<FieldInfo> found = new List<FieldInfo>(DelayFieldNames.Length);
        for (int i = 0; i < DelayFieldNames.Length; i++)
        {
            FieldInfo field = type.GetField(DelayFieldNames[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(float))
            {
                found.Add(field);
            }
        }

        fields = found.ToArray();
        DelayFieldCache[type] = fields;
        return fields;
    }

    private static bool TryGetPositiveFloatField(AbilityInstance instance, FieldInfo field, out float value)
    {
        value = 0f;
        try
        {
            if (field.GetValue(instance) is not float fieldValue ||
                fieldValue <= 0f ||
                float.IsNaN(fieldValue) ||
                float.IsInfinity(fieldValue))
            {
                return false;
            }

            value = fieldValue;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
    }

    private static bool TryGetPendingDamagePrefab(AbilityInstance instance, out DamageInstance damagePrefab)
    {
        Type sourceType = instance.GetType();
        if (PendingDamagePrefabCache.TryGetValue(sourceType, out damagePrefab))
        {
            return damagePrefab != null;
        }

        damagePrefab = null;
        string sourceName = sourceType.Name;
        for (int i = 0; i < PendingDamageTypeSuffixes.Length; i++)
        {
            string candidateName = sourceName + PendingDamageTypeSuffixes[i];
            if (!TryGetRegisteredAbilityType(candidateName, typeof(DamageInstance), out Type candidateType))
            {
                continue;
            }

            try
            {
                damagePrefab = DewResources.GetByType<AbilityInstance>(candidateType, ResourceLoadSettings.Light) as DamageInstance;
                if (damagePrefab != null)
                {
                    break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        PendingDamagePrefabCache[sourceType] = damagePrefab;
        return damagePrefab != null;
    }

    private static bool TryGetRegisteredAbilityType(string shortTypeName, Type expectedBaseType, out Type type)
    {
        type = null;
        try
        {
            if (!DewResources.database.typeNameToType.TryGetValue(shortTypeName, out Type candidate) ||
                !expectedBaseType.IsAssignableFrom(candidate) ||
                !DewResources.database.typeToGuid.ContainsKey(candidate))
            {
                return false;
            }

            type = candidate;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
    }

    private static float Positive(float first, float second)
    {
        if (IsPositiveThreatSize(first))
        {
            return first;
        }

        if (IsPositiveThreatSize(second))
        {
            return second;
        }

        return MinimumShapeSize;
    }

    private static float Positive(float first, float second, float third)
    {
        if (IsPositiveThreatSize(first))
        {
            return first;
        }

        if (IsPositiveThreatSize(second))
        {
            return second;
        }

        if (IsPositiveThreatSize(third))
        {
            return third;
        }

        return MinimumShapeSize;
    }

    private static bool IsPositiveThreatSize(float value)
    {
        return value > MinimumShapeSize && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static float SqrFlatDistance(Vector3 a, Vector3 b)
    {
        Vector2 delta = a.ToXY() - b.ToXY();
        return delta.sqrMagnitude;
    }

    private static Vector3 NormalizeFlat(Vector3 value)
    {
        value.y = 0f;
        if (value.sqrMagnitude <= 0.0001f)
        {
            return Vector3.forward;
        }

        return value.normalized;
    }
}
