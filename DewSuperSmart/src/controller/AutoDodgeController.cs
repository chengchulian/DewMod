using System;
using System.Collections.Generic;
using System.Reflection;
using DewSuperSmart.config;
using UnityEngine;

namespace DewSuperSmart;

internal sealed class AutoDodgeController : MonoBehaviour
{
    private const int RingCount = 5;
    private const int BaseDirectionSamples = 16;
    private const float MinimumCommandInterval = 0.05f;
    private const float ThreatCollectInterval = 0.02f;
    private const float RedThreatThreshold = 0.5f;
    private const float YellowThreatThreshold = 1f;
    private const float GreenThreatThreshold = 1.5f;
    private const int PathSafetySampleCount = 7;
    private const float MinimumCandidateDistance = 0.25f;
    private const float MinimumSafeThreatDistance = 0.1f;
    private const float ThreatNearMissFalloff = 1.75f;
    private const float CurrentThreatPathIgnoreDistance = 0.75f;
    private const float MinimumTravelSpeed = 2f;
    private const float FallbackDodgeSkillTravelSpeed = 12f;
    private const float MovementResponseDelay = 0.04f;
    private const float MovementSkillCommandLeadTime = 0.04f;
    private const float FallbackDodgeSkillUncollidableRatio = 0.7f;
    private const float EmergencyDodgeMaxTimeToImpact = 0.1f;
    private const float MovementSkillCommandLockoutPadding = 0.1f;
    private const float MovementSkillLandingClearance = 0.35f;
    private const float ProjectilePredictionStep = 0.025f;
    private const float ProjectileArrivalSafetyWindow = 0.45f;
    private const float DenseBarrageArrivalSafetyWindow = 1.15f;
    private const float SideDodgePreferenceWeight = 5f;
    private const float EmergencySearchRadiusBonus = 1.5f;
    private const string PrimusMeteorProjectileTypeName =
        "Ai_Mon_Primus_BossPrimusAeron_Adapt_Doom_Meteor_SubFireball";

    private readonly List<ThreatZone> _threats = new List<ThreatZone>(128);
    private readonly List<float> _currentThreatDistances = new List<float>(128);

    private float _keyDownTime = float.NegativeInfinity;
    private float _lastCommandTime = float.NegativeInfinity;
    private float _nextThreatCollectTime = float.NegativeInfinity;
    private float _threatSnapshotTime = float.NegativeInfinity;
    private float _movementSkillLockUntil = float.NegativeInfinity;
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
            TryMoveTowardCursorWhileHeld(hero, config);
            return;
        }

        float currentRisk = CalculateRisk(
            hero.agentPosition,
            heroRadius,
            out float currentMinimumDistance,
            _currentThreatDistances);
        if (currentRisk < Mathf.Max(config.AutoDodgeRiskThreshold, 0.01f))
        {
            TryMoveTowardCursorWhileHeld(hero, config);
            return;
        }

        if (Time.unscaledTime < _movementSkillLockUntil)
        {
            return;
        }

        // 命中越近，扩大候选搜索半径，避免在局部区域找不到真正安全点。
        bool hasIncomingImpact = TryGetEarliestIncomingImpact(hero.agentPosition, heroRadius, out float earliestImpact);
        float movementSearchRadius = GetMovementSearchRadius(config, earliestImpact);
        float movementSpeed = EstimateMovementSpeed(hero);
        bool hasMonsterThreat = TryGetSideDodgeAxis(hero.agentPosition, heroRadius, out Vector3 sideDodgeAxis);
        bool hasMovementPoint = TryFindSafePoint(
            hero,
            config,
            heroRadius,
            currentRisk,
            currentMinimumDistance,
            movementSearchRadius,
            movementSpeed,
            MovementResponseDelay,
            minimumTravelDistance: 0f,
            uncollidableRatio: 0f,
            isMovementSkill: false,
            hasMonsterThreat,
            sideDodgeAxis,
            out Vector3 movementPoint,
            out bool isMovementPointSafe);

        float timeSinceLastCommand = Time.unscaledTime - _lastCommandTime;
        float commandInterval = Mathf.Max(config.AutoDodgeCommandInterval, MinimumCommandInterval);
        bool canIssueMovementCommand = timeSinceLastCommand >= commandInterval ||
                                       hasIncomingImpact &&
                                       earliestImpact <= 0.25f &&
                                       timeSinceLastCommand >= MinimumCommandInterval;

        if (hasMovementPoint &&
            isMovementPointSafe &&
            canIssueMovementCommand &&
            TryMoveToSafePoint(hero, movementPoint, config))
        {
            _lastCommandTime = Time.unscaledTime;
            return;
        }

        if (!isMovementPointSafe &&
            hasIncomingImpact &&
            TryGetAutoDodgeMovementSkill(hero, config, out SkillTrigger movementSkill) &&
            TryGetDodgeSkillProfile(movementSkill, config, out DodgeSkillProfile skillProfile) &&
            earliestImpact <= EmergencyDodgeMaxTimeToImpact + skillProfile.ActivationDelay &&
            TryFindSafePoint(
                hero,
                config,
                heroRadius,
                currentRisk,
                currentMinimumDistance,
                skillProfile.SearchRadius,
                skillProfile.TravelSpeed,
                skillProfile.ActivationDelay,
                skillProfile.MinimumDistance,
                skillProfile.UncollidableRatio,
                isMovementSkill: true,
                hasMonsterThreat,
                sideDodgeAxis,
                out Vector3 skillPoint,
                out bool isSkillPointSafe) &&
            isSkillPointSafe &&
            TryCastMovementSkill(hero, skillPoint, config, skillProfile))
        {
            _lastCommandTime = Time.unscaledTime;
            return;
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
        _threatSnapshotTime = now;
        _nextThreatCollectTime = now + ThreatCollectInterval;
    }

    private void InvalidateThreatSnapshot()
    {
        _threats.Clear();
        _hasThreatSnapshot = false;
        _nextThreatCollectTime = float.NegativeInfinity;
        _threatSnapshotTime = float.NegativeInfinity;
        _movementSkillLockUntil = float.NegativeInfinity;
    }

    private static bool ShouldDodgeThreat(ThreatZone threat, AutoDodgeThreatLevel level, Vector3 heroPosition, float heroRadius)
    {
        if (!threat.IsDodgeable || !threat.IsActive)
        {
            return false;
        }

        float threshold = GetThreatLevelThreshold(level);
        return IsThreatUrgent(threat, heroPosition, heroRadius, threshold, threshold);
    }

    private static float GetThreatLevelThreshold(AutoDodgeThreatLevel level)
    {
        switch (level)
        {
            case AutoDodgeThreatLevel.Red:
                return RedThreatThreshold;
            case AutoDodgeThreatLevel.Yellow:
                return YellowThreatThreshold;
            default:
                return GreenThreatThreshold;
        }
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

    private bool TryGetSideDodgeAxis(Vector3 heroPosition, float heroRadius, out Vector3 sideDodgeAxis)
    {
        sideDodgeAxis = Vector3.zero;
        Monster primaryMonster = null;
        ThreatZone primaryThreat = default;
        float bestPriority = float.NegativeInfinity;

        for (int i = 0; i < _threats.Count; i++)
        {
            ThreatZone threat = _threats[i];
            Monster monster = ResolveThreatMonster(threat);
            if (monster == null || monster.IsNullInactiveDeadOrKnockedOut())
            {
                continue;
            }

            float signedDistance = threat.SignedDistance(heroPosition, heroRadius);
            float distancePriority = signedDistance <= 0f
                ? 4f
                : Mathf.Max(2f - signedDistance, 0f);
            float timeToImpact = GetRemainingTimeToImpact(threat);
            float timePriority = !float.IsNaN(timeToImpact) && !float.IsInfinity(timeToImpact)
                ? Mathf.Max(2f - Mathf.Max(timeToImpact, 0f), 0f)
                : 0f;
            float priority = threat.Weight * 2f + distancePriority + timePriority;
            if (priority <= bestPriority)
            {
                continue;
            }

            bestPriority = priority;
            primaryMonster = monster;
            primaryThreat = threat;
        }

        if (primaryMonster == null)
        {
            return false;
        }

        Vector3 attackAxis = heroPosition - primaryMonster.agentPosition;
        attackAxis.y = 0f;
        if (attackAxis.sqrMagnitude <= 0.0001f)
        {
            attackAxis = primaryThreat.Direction;
            attackAxis.y = 0f;
        }

        if (attackAxis.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        attackAxis.Normalize();
        sideDodgeAxis = new Vector3(-attackAxis.z, 0f, attackAxis.x);
        return true;
    }

    private static Monster ResolveThreatMonster(ThreatZone threat)
    {
        if (threat.Trigger != null && threat.Trigger.owner is Monster triggerOwner)
        {
            return triggerOwner;
        }

        Actor cursor = threat.Source;
        for (int depth = 0; cursor != null && depth < 8; depth++)
        {
            if (cursor is Monster monster)
            {
                return monster;
            }

            if (cursor is AbilityInstance instance && instance.info.caster is Monster caster)
            {
                return caster;
            }

            cursor = cursor.parentActor;
        }

        return null;
    }

    private bool TryFindSafePoint(
        Hero hero,
        PluginConfig config,
        float heroRadius,
        float currentRisk,
        float currentMinimumDistance,
        float searchRadius,
        float travelSpeed,
        float responseDelay,
        float minimumTravelDistance,
        float uncollidableRatio,
        bool isMovementSkill,
        bool hasMonsterThreat,
        Vector3 sideDodgeAxis,
        out Vector3 safePoint,
        out bool isFullySafe)
    {
        Vector3 heroPosition = hero.agentPosition;
        safePoint = heroPosition;
        isFullySafe = false;
        float safeRiskThreshold = Mathf.Max(config.AutoDodgeRiskThreshold, 0.01f);
        Vector3 desiredDirection = GetDesiredDirection(heroPosition);
        CandidateEvaluation best = default;
        bool hasBest = false;
        CandidateEvaluation bestProgress = default;
        bool hasProgress = false;
        for (int ring = 1; ring <= RingCount; ring++)
        {
            float distance = Mathf.Lerp(
                Mathf.Min(minimumTravelDistance, searchRadius),
                searchRadius,
                ring / (float)RingCount);
            int samples = BaseDirectionSamples + ring * 8;

            for (int i = 0; i < samples; i++)
            {
                float angle = i * 360f / samples;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 rawPoint = heroPosition + direction * distance;
                // 优先使用公开的线性扫掠 API；特殊地形上失败时回退到最近合法点 API。
                Vector3 candidate = GetValidDodgeDestination(heroPosition, rawPoint);

                if (!Dew.IsOkay(candidate) || Vector2.Distance(candidate.ToXY(), heroPosition.ToXY()) < MinimumCandidateDistance)
                {
                    continue;
                }

                CandidateEvaluation evaluation = EvaluateCandidate(
                    candidate,
                    heroPosition,
                    desiredDirection,
                    heroRadius,
                    travelSpeed,
                    responseDelay,
                    uncollidableRatio,
                    isMovementSkill,
                    hasMonsterThreat,
                    sideDodgeAxis);
                float requiredEndpointDistance = MinimumSafeThreatDistance +
                                                 (isMovementSkill ? MovementSkillLandingClearance : 0f);
                bool isSafe = evaluation.EndpointRisk <= safeRiskThreshold &&
                              evaluation.EndpointMinimumDistance >= requiredEndpointDistance &&
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
            isFullySafe = true;
            return true;
        }

        if (!hasProgress)
        {
            return false;
        }

        safePoint = bestProgress.Point;
        return true;
    }

    private bool TryGetEarliestIncomingImpact(
        Vector3 heroPosition,
        float heroRadius,
        out float earliestImpact)
    {
        earliestImpact = float.PositiveInfinity;
        for (int i = 0; i < _threats.Count; i++)
        {
            ThreatZone threat = _threats[i];
            float timeToImpact = GetRemainingTimeToImpact(threat);
            if (threat.IsDodgeable &&
                threat.IsActive &&
                !float.IsNaN(timeToImpact) &&
                !float.IsInfinity(timeToImpact) &&
                timeToImpact > 0f &&
                (threat.RequiresDodgeSkill ||
                 threat.IsProjectile ||
                 threat.SignedDistance(heroPosition, heroRadius) <= MinimumSafeThreatDistance))
            {
                earliestImpact = Mathf.Min(earliestImpact, timeToImpact);
            }
        }

        return !float.IsPositiveInfinity(earliestImpact);
    }

    private CandidateEvaluation EvaluateCandidate(
        Vector3 candidate,
        Vector3 heroPosition,
        Vector3 desiredDirection,
        float heroRadius,
        float travelSpeed,
        float responseDelay,
        float uncollidableRatio,
        bool isMovementSkill,
        bool hasMonsterThreat,
        Vector3 sideDodgeAxis)
    {
        float endpointRisk = CalculateNonProjectileRisk(candidate, heroRadius, out float endpointMinimumDistance);
        float avoidablePathRisk = CalculateAvoidablePathRisk(candidate, heroPosition, heroRadius);
        float timedImpactRisk = CalculateTimedImpactRisk(
            candidate,
            heroPosition,
            heroRadius,
            travelSpeed,
            responseDelay,
            uncollidableRatio,
            isMovementSkill);
        float escapeMargin = 0f;

        for (int i = 0; i < _threats.Count; i++)
        {
            ThreatZone threat = _threats[i];
            float currentDistance = i < _currentThreatDistances.Count
                ? _currentThreatDistances[i]
                : threat.SignedDistance(heroPosition, heroRadius);
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
        // 侧向闪避作为软约束，保留复杂地形下的可行点作为最后退路。
        float sideDodgePenalty = hasMonsterThreat
            ? GetSideDodgePenalty(candidate, heroPosition, sideDodgeAxis)
            : 0f;
        float score = endpointRisk * 1000f +
                      avoidablePathRisk * 750f +
                      timedImpactRisk * 900f -
                      escapeMargin * 8f +
                      intentPenalty +
                      sideDodgePenalty +
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
                if (threat.IsProjectile || threat.RequiresDodgeSkill)
                {
                    continue;
                }

                float currentDistance = i < _currentThreatDistances.Count
                    ? _currentThreatDistances[i]
                    : threat.SignedDistance(heroPosition, heroRadius);
                if (currentDistance <= CurrentThreatPathIgnoreDistance)
                {
                    continue;
                }

                if (threat.Kind == ThreatZoneKind.GroundHazard && sample % 2 != 0)
                {
                    continue;
                }

                sampleRisk += threat.RiskAt(
                    point,
                    threat.Kind == ThreatZoneKind.GroundHazard ? 0f : heroRadius);
            }

            pathRisk = Mathf.Max(pathRisk, sampleRisk);
        }

        return pathRisk;
    }

    private float CalculateTimedImpactRisk(
        Vector3 candidate,
        Vector3 heroPosition,
        float heroRadius,
        float travelSpeed,
        float responseDelay,
        float uncollidableRatio,
        bool isMovementSkill)
    {
        float travelDistance = Vector2.Distance(candidate.ToXY(), heroPosition.ToXY());
        if (travelDistance <= 0.01f || travelSpeed <= 0.01f)
        {
            return 0f;
        }

        float displacementTime = travelDistance / travelSpeed;
        float travelTime = responseDelay + displacementTime;
        if (travelTime <= 0.01f)
        {
            return 0f;
        }

        float impactRisk = CalculateMovingProjectileRisk(
            candidate,
            heroPosition,
            heroRadius,
            travelTime,
            responseDelay,
            uncollidableRatio,
            isMovementSkill);
        for (int i = 0; i < _threats.Count; i++)
        {
            ThreatZone threat = _threats[i];
            if (threat.IsProjectile)
            {
                continue;
            }

            float timeToImpact = GetRemainingTimeToImpact(threat);
            if (float.IsNaN(timeToImpact) ||
                float.IsInfinity(timeToImpact) ||
                timeToImpact <= 0f)
            {
                continue;
            }

            if (IsProtectedByDodgeSkill(
                    timeToImpact,
                    responseDelay,
                    displacementTime,
                    uncollidableRatio,
                    isMovementSkill))
            {
                continue;
            }

            if (threat.RequiresDodgeSkill)
            {
                if (isMovementSkill && timeToImpact > travelTime)
                {
                    continue;
                }

                impactRisk += threat.Weight + 1f;
                continue;
            }

            if (timeToImpact >= travelTime ||
                threat.SignedDistance(heroPosition, heroRadius) > GreenThreatThreshold)
            {
                continue;
            }

            float movementProgress = displacementTime > 0.001f
                ? Mathf.Clamp01((timeToImpact - responseDelay) / displacementTime)
                : 1f;
            Vector3 pointAtImpact = Vector3.Lerp(heroPosition, candidate, movementProgress);
            impactRisk += threat.RiskAt(pointAtImpact, heroRadius);
        }

        return impactRisk;
    }

    private float CalculateMovingProjectileRisk(
        Vector3 candidate,
        Vector3 heroPosition,
        float heroRadius,
        float travelTime,
        float responseDelay,
        float uncollidableRatio,
        bool isMovementSkill)
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
            float displacementTime = Mathf.Max(travelTime - responseDelay, 0f);
            Vector3 projectileOrigin = threat.Projectile != null ? threat.Projectile.position : threat.Origin;
            Vector2 previousRelativePosition = Vector2.zero;
            float previousTime = 0f;
            bool hasPreviousSample = false;

            for (int sample = 0; sample <= samples; sample++)
            {
                float time = horizon * sample / samples;
                float heroProgress = displacementTime > 0.001f
                    ? Mathf.Clamp01((time - responseDelay) / displacementTime)
                    : 1f;
                Vector3 heroAtTime = Vector3.Lerp(heroPosition, candidate, heroProgress);
                Vector3 projectileAtTime = projectileOrigin +
                                           threat.Direction * Mathf.Min(threat.ProjectileSpeed * time, threat.Length);
                Vector2 relativePosition = heroAtTime.ToXY() - projectileAtTime.ToXY();
                float clearance;
                if (hasPreviousSample)
                {
                    Vector2 relativeDelta = relativePosition - previousRelativePosition;
                    float relativeLengthSquared = relativeDelta.sqrMagnitude;
                    float closestProgress = relativeLengthSquared > 0.000001f
                        ? Mathf.Clamp01(-Vector2.Dot(previousRelativePosition, relativeDelta) / relativeLengthSquared)
                        : 1f;
                    clearance = (previousRelativePosition + relativeDelta * closestProgress).magnitude - collisionRadius;
                }
                else
                {
                    clearance = relativePosition.magnitude - collisionRadius;
                }

                if (clearance <= 0f)
                {
                    bool isProtected = IsProtectedByDodgeSkill(
                                           time,
                                           responseDelay,
                                           displacementTime,
                                           uncollidableRatio,
                                           isMovementSkill) &&
                                       (!hasPreviousSample || IsProtectedByDodgeSkill(
                                           previousTime,
                                           responseDelay,
                                           displacementTime,
                                           uncollidableRatio,
                                           isMovementSkill));
                    if (!isProtected)
                    {
                        threatRisk = Mathf.Max(threatRisk, threat.Weight + 1f + Mathf.Clamp01(-clearance));
                    }
                }
                else if (clearance < nearMissRadius - collisionRadius)
                {
                    float proximity = 1f - clearance / (nearMissRadius - collisionRadius);
                    threatRisk = Mathf.Max(threatRisk, threat.Weight * 0.45f * proximity);
                }

                previousRelativePosition = relativePosition;
                previousTime = time;
                hasPreviousSample = true;
            }

            maximumRisk = Mathf.Max(maximumRisk, threatRisk);
            accumulatedRisk += threatRisk;
        }

        return maximumRisk + accumulatedRisk * 0.35f;
    }

    private static bool IsProtectedByDodgeSkill(
        float time,
        float responseDelay,
        float displacementTime,
        float uncollidableRatio,
        bool isMovementSkill)
    {
        return isMovementSkill &&
               time >= responseDelay &&
               time <= responseDelay + displacementTime * uncollidableRatio;
    }

    private float GetRemainingTimeToImpact(ThreatZone threat)
    {
        float timeToImpact = threat.TimeToImpact;
        if (float.IsNaN(timeToImpact) || float.IsInfinity(timeToImpact))
        {
            return timeToImpact;
        }

        float snapshotAge = float.IsNegativeInfinity(_threatSnapshotTime)
            ? 0f
            : Mathf.Max(Time.unscaledTime - _threatSnapshotTime, 0f);
        return Mathf.Max(timeToImpact - snapshotAge, 0f);
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
        return CalculateRisk(point, heroRadius, out minimumSignedDistance, distances: null);
    }

    private float CalculateRisk(
        Vector3 point,
        float heroRadius,
        out float minimumSignedDistance,
        List<float> distances)
    {
        float risk = 0f;
        minimumSignedDistance = float.PositiveInfinity;
        distances?.Clear();
        for (int i = 0; i < _threats.Count; i++)
        {
            ThreatZone threat = _threats[i];
            float distance = threat.SignedDistance(point, heroRadius);
            distances?.Add(distance);
            minimumSignedDistance = Mathf.Min(minimumSignedDistance, distance);
            if (distance <= 0f)
            {
                risk += threat.Weight + Mathf.Clamp01(-distance / 2f);
            }
            else if (distance < ThreatNearMissFalloff)
            {
                risk += threat.Weight * 0.25f * (1f - distance / ThreatNearMissFalloff);
            }
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
            if (threat.IsProjectile || threat.RequiresDodgeSkill)
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

    private static float EstimateMovementSpeed(Hero hero)
    {
        float speed = hero.Control != null ? hero.Control.currentMaxAgentSpeed : 0f;
        return Mathf.Max(speed, MinimumTravelSpeed);
    }

    // 根据尚未发生的命中时间调整候选搜索范围。
    private static float GetMovementSearchRadius(PluginConfig config, float earliestImpact)
    {
        float baseRadius = Mathf.Max(config.AutoDodgeSearchRadius, 1f);
        if (float.IsNaN(earliestImpact) || float.IsInfinity(earliestImpact))
        {
            return baseRadius;
        }

        // 命中越近，扩大搜索空间以优先寻找真正脱离威胁的点。
        float urgency = Mathf.Clamp01(1f - Mathf.Max(earliestImpact, 0f) / 1.5f);
        return baseRadius + EmergencySearchRadiusBonus * urgency;
    }

    // 使用公开导航 API 获取可达点；不同版本 API 行为变化时回退到最近合法点。
    private static Vector3 GetValidDodgeDestination(Vector3 origin, Vector3 target)
    {
        Vector3 swept = Dew.GetValidAgentDestination_LinearSweep(origin, target);
        return Dew.IsOkay(swept)
            ? swept
            : Dew.GetValidAgentDestination_Closest(origin, target);
    }

    // 以评分方式偏好怪物攻击轴的侧面，避免硬过滤导致无候选点。
    private static float GetSideDodgePenalty(Vector3 candidate, Vector3 heroPosition, Vector3 sideDodgeAxis)
    {
        Vector3 travel = candidate - heroPosition;
        travel.y = 0f;
        if (travel.sqrMagnitude <= 0.0001f || sideDodgeAxis.sqrMagnitude <= 0.0001f)
        {
            return SideDodgePreferenceWeight;
        }

        float alignment = Mathf.Abs(Vector3.Dot(travel.normalized, sideDodgeAxis.normalized));
        return (1f - alignment) * SideDodgePreferenceWeight;
    }

    private static bool TryGetDodgeSkillProfile(
        SkillTrigger movementSkill,
        PluginConfig config,
        out DodgeSkillProfile profile)
    {
        profile = default;
        if (!TryGetSkillRange(movementSkill, out float skillRange))
        {
            return false;
        }

        float maxSearch = Mathf.Max(config.AutoDodgeSearchRadius, 1f);
        TriggerConfig triggerConfig = movementSkill.currentConfig;
        object spawnedInstance = triggerConfig.spawnedInstance;
        float speed;
        float minimumDistance;
        float uncollidableRatio;
        if (spawnedInstance is Ai_GenericDodge genericDodge)
        {
            // 通用闪避组件字段是游戏公开运行时类型，优先使用强类型 API。
            speed = genericDodge.speed;
            minimumDistance = genericDodge.minDistance;
            uncollidableRatio = genericDodge.uncollidableRatio;
        }
        else
        {
            // 自定义位移技能没有统一接口时，回退到字段读取兼容路径。
            speed = ReadSkillFloat(spawnedInstance, "speed", FallbackDodgeSkillTravelSpeed);
            minimumDistance = ReadSkillFloat(spawnedInstance, "minDistance", 0f);
            uncollidableRatio = ReadSkillFloat(
                spawnedInstance,
                "uncollidableRatio",
                FallbackDodgeSkillUncollidableRatio);
        }
        float channelDuration = triggerConfig.channel != null ? Mathf.Max(triggerConfig.channel.duration, 0f) : 0f;
        try
        {
            channelDuration *= movementSkill.GetChannelDurationMultiplier();
        }
        catch (Exception)
        {
            // The base timing remains a safe fallback for version-specific triggers.
        }

        profile = new DodgeSkillProfile(
            Mathf.Clamp(skillRange, 1f, maxSearch),
            Mathf.Max(speed, MinimumTravelSpeed),
            Mathf.Clamp(minimumDistance, 0f, skillRange),
            MovementSkillCommandLeadTime + channelDuration,
            Mathf.Clamp01(uncollidableRatio));
        return true;
    }

    private static float ReadSkillFloat(object instance, string fieldName, float fallback)
    {
        if (instance == null)
        {
            return fallback;
        }

        try
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field?.GetValue(instance) is float value &&
                value >= 0f &&
                !float.IsNaN(value) &&
                !float.IsInfinity(value))
            {
                return value;
            }
        }
        catch (Exception)
        {
            // Movement skill implementations vary by hero and game version.
        }

        return fallback;
    }

    private bool TryCastMovementSkill(
        Hero hero,
        Vector3 destination,
        PluginConfig config,
        DodgeSkillProfile skillProfile)
    {
        if (Time.unscaledTime < _movementSkillLockUntil)
        {
            return true;
        }

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

        float travelDistance = Vector2.Distance(hero.agentPosition.ToXY(), destination.ToXY());
        float displacementTime = travelDistance / Mathf.Max(skillProfile.TravelSpeed, MinimumTravelSpeed);
        float invulnerabilityDuration = skillProfile.ActivationDelay +
                                        displacementTime * skillProfile.UncollidableRatio;
        _movementSkillLockUntil = Time.unscaledTime +
                                  invulnerabilityDuration +
                                  MovementSkillCommandLockoutPadding;
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

    private void TryMoveTowardCursorWhileHeld(Hero hero, PluginConfig config)
    {
        KeyCode key = config.AutoDodgeKey;
        float commandInterval = Mathf.Max(config.AutoDodgeCommandInterval, MinimumCommandInterval);
        if (key == KeyCode.None || !Input.GetKey(key) ||
            Time.unscaledTime - _lastCommandTime < commandInterval)
        {
            return;
        }

        Vector3 cursorPoint = ControlManager.GetWorldPositionOnGroundOnCursor();
        if (!Dew.IsOkay(cursorPoint) ||
            Vector2.Distance(cursorPoint.ToXY(), hero.agentPosition.ToXY()) < MinimumCandidateDistance ||
            !TryMoveToSafePoint(hero, cursorPoint, config))
        {
            return;
        }

        _lastCommandTime = Time.unscaledTime;
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

    private readonly struct DodgeSkillProfile
    {
        public readonly float SearchRadius;
        public readonly float TravelSpeed;
        public readonly float MinimumDistance;
        public readonly float ActivationDelay;
        public readonly float UncollidableRatio;

        public DodgeSkillProfile(
            float searchRadius,
            float travelSpeed,
            float minimumDistance,
            float activationDelay,
            float uncollidableRatio)
        {
            SearchRadius = searchRadius;
            TravelSpeed = travelSpeed;
            MinimumDistance = minimumDistance;
            ActivationDelay = activationDelay;
            UncollidableRatio = uncollidableRatio;
        }
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
