using System.Collections.Generic;
using DewSuperSmart.config;
using UnityEngine;

namespace DewSuperSmart;

internal sealed class AutoDodgeController : MonoBehaviour
{
    private const int RingCount = 3;
    private const int BaseDirectionSamples = 12;
    private const float MinimumCommandInterval = 0.03f;
    private const float ThreatCollectInterval = 0.05f;
    private const float RedDistanceToHero = 0.8f;
    private const float YellowDistanceToHero = 1.8f;
    private const float RedTimeToImpact = 0.8f;
    private const float YellowTimeToImpact = 1.8f;
    private const int PathSafetySampleCount = 3;
    private const float MinimumCandidateDistance = 0.25f;
    private const float MinimumSafeThreatDistance = 0.1f;
    private const float CurrentThreatPathIgnoreDistance = 0.75f;
    private const float MinimumTravelSpeed = 2f;
    private const float DodgeSkillTravelSpeed = 12f;
    private const float MinimumTimedThreatWindow = 0.05f;
    private const float ProjectilePredictionStep = 0.075f;
    private const float ProjectileArrivalSafetyWindow = 0.45f;
    private const float DenseBarrageArrivalSafetyWindow = 1.15f;
    private const string PrimusMeteorProjectileTypeName =
        "Ai_Mon_Primus_BossPrimusAeron_Adapt_Doom_Meteor_SubFireball";

    private readonly List<ThreatZone> _threats = new List<ThreatZone>(128);

    private float _keyDownTime = float.NegativeInfinity;
    private float _lastCommandTime = float.NegativeInfinity;
    private float _nextThreatCollectTime = float.NegativeInfinity;
    private AutoDodgeThreatLevel _lastAutoDodgeLevel;
    private bool _hasThreatSnapshot;

    private void Update()
    {
        DewSuperSmart instance = DewSuperSmart.Instance;
        if (instance == null)
        {
            return;
        }

        PluginConfig config = instance.Config;
        if (!config.EnableAutoDodge || !IsAutoDodgeActive(config))
        {
            InvalidateThreatSnapshot();
            return;
        }

        if (!TryGetLocalHero(out Hero hero))
        {
            InvalidateThreatSnapshot();
            return;
        }

        float heroRadius = GetHeroThreatRadius(hero, config);

        RefreshThreatSnapshot(hero, config, heroRadius);
        if (_threats.Count == 0)
        {
            return;
        }

        float currentRisk = CalculateRisk(hero.agentPosition, heroRadius);
        if (currentRisk < Mathf.Max(config.AutoDodgeRiskThreshold, 0.01f))
        {
            return;
        }

        float commandInterval = Mathf.Max(config.AutoDodgeCommandInterval, MinimumCommandInterval);
        if (Time.unscaledTime - _lastCommandTime < commandInterval)
        {
            return;
        }

        if (!TryFindSafePoint(hero, config, heroRadius, out Vector3 safePoint))
        {
            return;
        }

        if (TryCastMovementSkill(hero, safePoint, config) || TryMoveToSafePoint(hero, safePoint, config))
        {
            _lastCommandTime = Time.unscaledTime;
        }
    }

    private static bool TryGetLocalHero(out Hero hero)
    {
        hero = DewPlayer.local?.hero;
        return hero != null && !hero.IsNullInactiveDeadOrKnockedOut();
    }

    private bool IsAutoDodgeActive(PluginConfig config)
    {
        KeyCode key = config.AutoDodgeKey;
        if (key == KeyCode.None)
        {
            _keyDownTime = float.NegativeInfinity;
            return true;
        }

        if (Input.GetKeyDown(key))
        {
            _keyDownTime = Time.unscaledTime;
        }

        if (!Input.GetKey(key))
        {
            _keyDownTime = float.NegativeInfinity;
            return false;
        }

        if (float.IsNegativeInfinity(_keyDownTime))
        {
            _keyDownTime = Time.unscaledTime;
        }

        return Time.unscaledTime - _keyDownTime >= Mathf.Max(config.AutoDodgeHoldDelay, 0f);
    }

    private void FilterThreatsByDodgeLevel(AutoDodgeThreatLevel level, Vector3 heroPosition, float heroRadius)
    {
        for (int i = _threats.Count - 1; i >= 0; i--)
        {
            if (!ShouldDodgeThreat(_threats[i], level, heroPosition, heroRadius))
            {
                _threats.RemoveAt(i);
            }
        }
    }

    private void RefreshThreatSnapshot(Hero hero, PluginConfig config, float heroRadius)
    {
        float now = Time.unscaledTime;
        AutoDodgeThreatLevel level = config.AutoDodgeLevel;
        if (_hasThreatSnapshot && now < _nextThreatCollectTime && _lastAutoDodgeLevel == level)
        {
            return;
        }

        IReadOnlyList<ThreatZone> snapshot = DewSuperSmart.Instance.Threats.GetThreats(hero, config);
        _threats.Clear();
        for (int i = 0; i < snapshot.Count; i++)
        {
            _threats.Add(snapshot[i]);
        }

        FilterThreatsByDodgeLevel(level, hero.agentPosition, heroRadius);
        _lastAutoDodgeLevel = level;
        _hasThreatSnapshot = true;
        _nextThreatCollectTime = now + ThreatCollectInterval;
    }

    private void InvalidateThreatSnapshot()
    {
        _threats.Clear();
        _hasThreatSnapshot = false;
        _nextThreatCollectTime = float.NegativeInfinity;
    }

    private static bool ShouldDodgeThreat(ThreatZone threat, AutoDodgeThreatLevel level, Vector3 heroPosition, float heroRadius)
    {
        if (!threat.IsDodgeable || !threat.IsActive)
        {
            return false;
        }

        if (level == AutoDodgeThreatLevel.Green)
        {
            return true;
        }

        if (level == AutoDodgeThreatLevel.Red)
        {
            return IsThreatUrgent(threat, heroPosition, heroRadius, RedDistanceToHero, RedTimeToImpact);
        }

        return IsThreatUrgent(threat, heroPosition, heroRadius, YellowDistanceToHero, YellowTimeToImpact);
    }

    private static bool IsThreatUrgent(
        ThreatZone threat,
        Vector3 heroPosition,
        float heroRadius,
        float distanceThreshold,
        float timeThreshold)
    {
        if (IsDenseBarrageProjectile(threat))
        {
            return true;
        }

        if (threat.IsProjectile && !float.IsNaN(threat.TimeToImpact) && !float.IsInfinity(threat.TimeToImpact))
        {
            return Mathf.Max(threat.TimeToImpact, 0f) <= timeThreshold;
        }

        return IsWithinThreatLevel(threat.SignedDistance(heroPosition, heroRadius), distanceThreshold) ||
               (threat.Activity == ThreatActivity.Imminent && IsWithinThreatLevel(threat.TimeToImpact, timeThreshold));
    }

    private static bool IsWithinThreatLevel(float value, float threshold)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && Mathf.Max(value, 0f) <= threshold;
    }

    private bool TryFindSafePoint(
        Hero hero,
        PluginConfig config,
        float heroRadius,
        out Vector3 safePoint)
    {
        Vector3 heroPosition = hero.agentPosition;
        safePoint = heroPosition;
        float safeRiskThreshold = Mathf.Max(config.AutoDodgeRiskThreshold, 0.01f);
        float searchRadius = GetSearchRadius(hero, config);
        float travelSpeed = EstimateDodgeTravelSpeed(hero, config);
        Vector3 desiredDirection = GetDesiredDirection(heroPosition);
        CandidateEvaluation best = default;
        bool hasBest = false;
        CandidateEvaluation bestProgress = default;
        bool hasProgress = false;
        float currentRisk = CalculateRisk(heroPosition, heroRadius, out float currentMinimumDistance);

        for (int ring = 1; ring <= RingCount; ring++)
        {
            float distance = searchRadius * ring / RingCount;
            int samples = BaseDirectionSamples + ring * 8;

            for (int i = 0; i < samples; i++)
            {
                float angle = i * 360f / samples;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 rawPoint = heroPosition + direction * distance;
                Vector3 candidate = Dew.GetValidAgentDestination_LinearSweep(heroPosition, rawPoint);

                if (!Dew.IsOkay(candidate) || Vector2.Distance(candidate.ToXY(), heroPosition.ToXY()) < MinimumCandidateDistance)
                {
                    continue;
                }

                CandidateEvaluation evaluation = EvaluateCandidate(
                    candidate,
                    heroPosition,
                    desiredDirection,
                    heroRadius,
                    travelSpeed);
                bool isSafe = evaluation.EndpointRisk <= safeRiskThreshold &&
                              evaluation.EndpointMinimumDistance >= MinimumSafeThreatDistance &&
                              evaluation.AvoidablePathRisk <= safeRiskThreshold &&
                              evaluation.TimedImpactRisk <= safeRiskThreshold;
                if (isSafe)
                {
                    if (!hasBest || evaluation.Score < best.Score)
                    {
                        best = evaluation;
                        hasBest = true;
                    }
                }
                else if (IsProgressCandidate(evaluation, currentRisk, currentMinimumDistance) &&
                         (!hasProgress || evaluation.Score < bestProgress.Score))
                {
                    bestProgress = evaluation;
                    hasProgress = true;
                }
            }
        }

        if (hasBest)
        {
            safePoint = best.Point;
            return true;
        }

        if (!hasProgress)
        {
            return false;
        }

        safePoint = bestProgress.Point;
        return true;
    }

    private CandidateEvaluation EvaluateCandidate(
        Vector3 candidate,
        Vector3 heroPosition,
        Vector3 desiredDirection,
        float heroRadius,
        float travelSpeed)
    {
        float endpointRisk = CalculateNonProjectileRisk(candidate, heroRadius, out float endpointMinimumDistance);
        float avoidablePathRisk = CalculateAvoidablePathRisk(candidate, heroPosition, heroRadius);
        float timedImpactRisk = CalculateTimedImpactRisk(candidate, heroPosition, heroRadius, travelSpeed);
        float escapeMargin = 0f;

        for (int i = 0; i < _threats.Count; i++)
        {
            ThreatZone threat = _threats[i];
            float currentDistance = threat.SignedDistance(heroPosition, heroRadius);
            if (currentDistance <= 0.75f)
            {
                escapeMargin += Mathf.Clamp(threat.SignedDistance(candidate, heroRadius), -2f, 6f);
            }
        }

        float travelDistance = Vector2.Distance(candidate.ToXY(), heroPosition.ToXY());
        Vector3 travelDirection = candidate - heroPosition;
        travelDirection.y = 0f;
        float intentPenalty = desiredDirection.sqrMagnitude > 0.0001f && travelDirection.sqrMagnitude > 0.0001f
            ? (1f - Vector3.Dot(desiredDirection, travelDirection.normalized)) * 1.5f
            : 0f;
        float score = endpointRisk * 1000f +
                      avoidablePathRisk * 750f +
                      timedImpactRisk * 900f -
                      escapeMargin * 8f +
                      intentPenalty +
                      travelDistance * 0.15f -
                      Mathf.Clamp(endpointMinimumDistance, 0f, 6f) * 2f;

        return new CandidateEvaluation(candidate, score, endpointRisk, endpointMinimumDistance, avoidablePathRisk, timedImpactRisk);
    }

    private static bool IsProgressCandidate(
        CandidateEvaluation evaluation,
        float currentRisk,
        float currentMinimumDistance)
    {
        return evaluation.EndpointRisk < currentRisk - 0.01f ||
               evaluation.EndpointMinimumDistance > currentMinimumDistance + 0.1f;
    }

    private float CalculateAvoidablePathRisk(Vector3 candidate, Vector3 heroPosition, float heroRadius)
    {
        float pathRisk = 0f;
        for (int sample = 1; sample <= PathSafetySampleCount; sample++)
        {
            float t = sample / (PathSafetySampleCount + 1f);
            Vector3 point = Vector3.Lerp(heroPosition, candidate, t);
            float sampleRisk = 0f;

            for (int i = 0; i < _threats.Count; i++)
            {
                ThreatZone threat = _threats[i];
                if (threat.IsProjectile)
                {
                    continue;
                }

                if (threat.SignedDistance(heroPosition, heroRadius) <= CurrentThreatPathIgnoreDistance)
                {
                    continue;
                }

                sampleRisk += threat.RiskAt(point, heroRadius);
            }

            pathRisk = Mathf.Max(pathRisk, sampleRisk);
        }

        return pathRisk;
    }

    private float CalculateTimedImpactRisk(Vector3 candidate, Vector3 heroPosition, float heroRadius, float travelSpeed)
    {
        float travelDistance = Vector2.Distance(candidate.ToXY(), heroPosition.ToXY());
        if (travelDistance <= 0.01f || travelSpeed <= 0.01f)
        {
            return 0f;
        }

        float travelTime = travelDistance / travelSpeed;
        if (travelTime <= 0.01f)
        {
            return 0f;
        }

        float impactRisk = CalculateMovingProjectileRisk(
            candidate,
            heroPosition,
            heroRadius,
            travelTime);
        for (int i = 0; i < _threats.Count; i++)
        {
            ThreatZone threat = _threats[i];
            if (threat.IsProjectile)
            {
                continue;
            }

            float timeToImpact = threat.TimeToImpact;
            if (float.IsNaN(timeToImpact) ||
                float.IsInfinity(timeToImpact) ||
                timeToImpact <= MinimumTimedThreatWindow ||
                timeToImpact >= travelTime)
            {
                continue;
            }

            if (threat.SignedDistance(heroPosition, heroRadius) > YellowDistanceToHero)
            {
                continue;
            }

            Vector3 pointAtImpact = Vector3.Lerp(heroPosition, candidate, Mathf.Clamp01(timeToImpact / travelTime));
            impactRisk += threat.RiskAt(pointAtImpact, heroRadius);
        }

        return impactRisk;
    }

    private float CalculateMovingProjectileRisk(
        Vector3 candidate,
        Vector3 heroPosition,
        float heroRadius,
        float travelTime)
    {
        float maximumRisk = 0f;
        float accumulatedRisk = 0f;

        for (int i = 0; i < _threats.Count; i++)
        {
            ThreatZone threat = _threats[i];
            if (!threat.IsProjectile ||
                threat.Kind != ThreatZoneKind.Line ||
                threat.ProjectileSpeed <= 0.01f)
            {
                continue;
            }

            float arrivalWindow = IsDenseBarrageProjectile(threat)
                ? DenseBarrageArrivalSafetyWindow
                : ProjectileArrivalSafetyWindow;
            float projectileLifetime = threat.Length / threat.ProjectileSpeed;
            float horizon = Mathf.Min(travelTime + arrivalWindow, projectileLifetime);
            if (horizon <= 0f)
            {
                continue;
            }

            float collisionRadius = threat.Width * 0.5f + heroRadius;
            float nearMissRadius = collisionRadius + (IsDenseBarrageProjectile(threat) ? 1.15f : 0.65f);
            float threatRisk = 0f;
            int samples = Mathf.Max(Mathf.CeilToInt(horizon / ProjectilePredictionStep), 1);

            for (int sample = 0; sample <= samples; sample++)
            {
                float time = horizon * sample / samples;
                float heroProgress = travelTime > 0.001f ? Mathf.Clamp01(time / travelTime) : 1f;
                Vector3 heroAtTime = Vector3.Lerp(heroPosition, candidate, heroProgress);
                Vector3 projectileAtTime =
                    threat.Origin + threat.Direction * Mathf.Min(threat.ProjectileSpeed * time, threat.Length);
                float clearance = Vector2.Distance(heroAtTime.ToXY(), projectileAtTime.ToXY()) - collisionRadius;

                if (clearance <= 0f)
                {
                    threatRisk = Mathf.Max(threatRisk, threat.Weight + 1f + Mathf.Clamp01(-clearance));
                }
                else if (clearance < nearMissRadius - collisionRadius)
                {
                    float proximity = 1f - clearance / (nearMissRadius - collisionRadius);
                    threatRisk = Mathf.Max(threatRisk, threat.Weight * 0.45f * proximity);
                }
            }

            maximumRisk = Mathf.Max(maximumRisk, threatRisk);
            accumulatedRisk += threatRisk;
        }

        return maximumRisk + accumulatedRisk * 0.35f;
    }

    private static bool IsDenseBarrageProjectile(ThreatZone threat)
    {
        return threat.Projectile != null &&
               threat.Projectile.GetType().Name == PrimusMeteorProjectileTypeName;
    }

    private float CalculateRisk(Vector3 point, float heroRadius)
    {
        return CalculateRisk(point, heroRadius, out _);
    }

    private float CalculateRisk(Vector3 point, float heroRadius, out float minimumSignedDistance)
    {
        float risk = 0f;
        minimumSignedDistance = float.PositiveInfinity;
        for (int i = 0; i < _threats.Count; i++)
        {
            ThreatZone threat = _threats[i];
            minimumSignedDistance = Mathf.Min(minimumSignedDistance, threat.SignedDistance(point, heroRadius));
            risk += threat.RiskAt(point, heroRadius);
        }

        return risk;
    }

    private float CalculateNonProjectileRisk(
        Vector3 point,
        float heroRadius,
        out float minimumSignedDistance)
    {
        float risk = 0f;
        minimumSignedDistance = float.PositiveInfinity;
        for (int i = 0; i < _threats.Count; i++)
        {
            ThreatZone threat = _threats[i];
            if (threat.IsProjectile)
            {
                continue;
            }

            minimumSignedDistance = Mathf.Min(
                minimumSignedDistance,
                threat.SignedDistance(point, heroRadius));
            risk += threat.RiskAt(point, heroRadius);
        }

        return risk;
    }

    private static float GetHeroThreatRadius(Hero hero, PluginConfig config)
    {
        float radius = hero.Control != null ? hero.Control.outerRadius : 0.45f;
        return Mathf.Max(radius + Mathf.Max(config.ThreatPadding, 0f), 0.1f);
    }

    private static float EstimateDodgeTravelSpeed(Hero hero, PluginConfig config)
    {
        float speed = hero.Control != null ? hero.Control.currentMaxAgentSpeed : 0f;
        if (TryGetAutoDodgeMovementSkill(hero, config, out _))
        {
            speed = Mathf.Max(speed, DodgeSkillTravelSpeed);
        }

        return Mathf.Max(speed, MinimumTravelSpeed);
    }

    private static float GetSearchRadius(Hero hero, PluginConfig config)
    {
        float maxSearch = Mathf.Max(config.AutoDodgeSearchRadius, 1f);
        float fallback = Mathf.Clamp(config.AutoDodgeFallbackDistance, 1f, maxSearch);

        if (TryGetAutoDodgeMovementSkill(hero, config, out SkillTrigger movementSkill) &&
            TryGetSkillRange(movementSkill, out float skillRange))
        {
            return Mathf.Clamp(skillRange, 1f, maxSearch);
        }

        return fallback;
    }

    private static bool TryCastMovementSkill(Hero hero, Vector3 destination, PluginConfig config)
    {
        if (hero.Control == null ||
            hero.Control.isDisplacing ||
            !TryGetAutoDodgeMovementSkill(hero, config, out SkillTrigger movementSkill))
        {
            return false;
        }

        CastInfo info = BuildMovementCastInfo(hero, movementSkill, destination);
        hero.Control.CmdCast(movementSkill, movementSkill.currentConfigIndex, info, allowMoveToCast: false, skipRangeCheck: false);

        if (!movementSkill.currentConfig.postponeBasicCommand)
        {
            hero.Control.CmdAttack(null, doChase: false);
        }

        return true;
    }

    private static bool TryMoveToSafePoint(Hero hero, Vector3 destination, PluginConfig config)
    {
        if (!config.AutoDodgeMoveFallback || hero.Control == null || hero.Control.isDisplacing)
        {
            return false;
        }

        hero.Control.CmdMoveToDestination(destination, immediately: true, speedMult: 1f);
        return true;
    }

    private static Vector3 GetDesiredDirection(Vector3 heroPosition)
    {
        Vector3 cursorPoint = ControlManager.GetWorldPositionOnGroundOnCursor();
        if (!Dew.IsOkay(cursorPoint))
        {
            return Vector3.zero;
        }

        Vector3 direction = cursorPoint - heroPosition;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0625f ? direction.normalized : Vector3.zero;
    }

    private static CastInfo BuildMovementCastInfo(Hero hero, SkillTrigger movementSkill, Vector3 destination)
    {
        TriggerConfig config = movementSkill.currentConfig;
        Vector3 point = ClampPointToSkillRange(hero.agentPosition, destination, config);

        switch (config.castMethod.type)
        {
            case CastMethodType.Point:
                return new CastInfo(hero, point);
            case CastMethodType.Arrow:
            case CastMethodType.Cone:
                return new CastInfo(hero, CastInfo.GetAngle(point - hero.agentPosition));
            case CastMethodType.Target:
            case CastMethodType.None:
            default:
                return new CastInfo(hero);
        }
    }

    private static Vector3 ClampPointToSkillRange(Vector3 origin, Vector3 point, TriggerConfig config)
    {
        float range = config.effectiveRange;
        if (range <= 0.25f || float.IsNaN(range) || float.IsInfinity(range))
        {
            return point;
        }

        Vector3 delta = point - origin;
        delta.y = 0f;
        if (delta.magnitude <= range)
        {
            return point;
        }

        return Dew.GetPositionOnGround(origin + delta.normalized * range);
    }

    private static bool TryGetReadyMovementSkill(Hero hero, out SkillTrigger movementSkill)
    {
        movementSkill = null;
        if (hero.Skill == null || !hero.Skill.TryGetSkill(HeroSkillLocation.Movement, out movementSkill))
        {
            return false;
        }

        return movementSkill != null && movementSkill.CanBeCast();
    }

    private static bool TryGetAutoDodgeMovementSkill(
        Hero hero,
        PluginConfig config,
        out SkillTrigger movementSkill)
    {
        movementSkill = null;
        if (!config.AutoDodgeUseMovementSkill || !TryGetReadyMovementSkill(hero, out movementSkill))
        {
            return false;
        }

        return !config.AutoDodgeReserveOneCharge || movementSkill.currentConfigCurrentCharge > 1;
    }

    private static bool TryGetSkillRange(SkillTrigger skill, out float range)
    {
        range = 0f;
        TriggerConfig config = skill.currentConfig;
        if (config == null)
        {
            return false;
        }

        range = config.effectiveRange;
        return range > 0.25f && !float.IsNaN(range) && !float.IsInfinity(range);
    }

    private readonly struct CandidateEvaluation
    {
        public readonly Vector3 Point;
        public readonly float Score;
        public readonly float EndpointRisk;
        public readonly float EndpointMinimumDistance;
        public readonly float AvoidablePathRisk;
        public readonly float TimedImpactRisk;

        public CandidateEvaluation(
            Vector3 point,
            float score,
            float endpointRisk,
            float endpointMinimumDistance,
            float avoidablePathRisk,
            float timedImpactRisk)
        {
            Point = point;
            Score = score;
            EndpointRisk = endpointRisk;
            EndpointMinimumDistance = endpointMinimumDistance;
            AvoidablePathRisk = avoidablePathRisk;
            TimedImpactRisk = timedImpactRisk;
        }
    }
}
