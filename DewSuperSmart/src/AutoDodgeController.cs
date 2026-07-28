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

    private readonly ThreatAnalyzer _threatAnalyzer = new ThreatAnalyzer();
    private readonly List<ThreatZone> _threats = new List<ThreatZone>(128);

    private float _keyDownTime = float.NegativeInfinity;
    private float _lastCommandTime = float.NegativeInfinity;
    private float _lastPointerMoveTime = float.NegativeInfinity;
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
        if (!config.EnableAutoDodge || !IsAutoDodgeHeld(config))
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
            TryMoveTowardPointer(hero, config);
            return;
        }

        float currentRisk = CalculateRisk(hero.agentPosition, heroRadius);
        if (currentRisk < Mathf.Max(config.AutoDodgeRiskThreshold, 0.01f))
        {
            TryMoveTowardPointer(hero, config);
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
            _lastPointerMoveTime = Time.unscaledTime;
            return;
        }
    }

    private static bool TryGetLocalHero(out Hero hero)
    {
        hero = DewPlayer.local?.hero;
        return hero != null && !hero.IsNullInactiveDeadOrKnockedOut();
    }

    private bool IsAutoDodgeHeld(PluginConfig config)
    {
        KeyCode key = config.AutoDodgeKey;
        if (key == KeyCode.None)
        {
            _keyDownTime = float.NegativeInfinity;
            return false;
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

        _threatAnalyzer.CollectThreats(hero, config, _threats, forAutoDodge: true);
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
        if (level == AutoDodgeThreatLevel.Green)
        {
            return true;
        }

        if (!IsActiveThreat(threat))
        {
            return false;
        }

        if (level == AutoDodgeThreatLevel.Red)
        {
            return IsWithinThreatLevel(threat.SignedDistance(heroPosition, heroRadius), RedDistanceToHero) ||
                   IsWithinThreatLevel(threat.TimeToImpact, RedTimeToImpact);
        }

        return IsWithinThreatLevel(threat.SignedDistance(heroPosition, heroRadius), YellowDistanceToHero) ||
               IsWithinThreatLevel(threat.TimeToImpact, YellowTimeToImpact);
    }

    private static bool IsWithinThreatLevel(float value, float threshold)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && Mathf.Max(value, 0f) <= threshold;
    }

    private static bool IsActiveThreat(ThreatZone threat)
    {
        return threat.IsProjectile || threat.Trigger == null || threat.Trigger.Network_isCasting;
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
        CandidateEvaluation best = default;
        bool hasBest = false;

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

                if (!TryEvaluateCandidate(candidate, heroPosition, heroRadius, travelSpeed, safeRiskThreshold, out CandidateEvaluation evaluation))
                {
                    continue;
                }

                if (!hasBest || evaluation.Score < best.Score)
                {
                    best = evaluation;
                    hasBest = true;
                }
            }
        }

        if (!hasBest)
        {
            return false;
        }

        safePoint = best.Point;
        return true;
    }

    private bool TryEvaluateCandidate(
        Vector3 candidate,
        Vector3 heroPosition,
        float heroRadius,
        float travelSpeed,
        float safeRiskThreshold,
        out CandidateEvaluation evaluation)
    {
        evaluation = default;
        float endpointRisk = CalculateRisk(candidate, heroRadius, out float endpointMinimumDistance);
        if (endpointRisk > safeRiskThreshold || endpointMinimumDistance < MinimumSafeThreatDistance)
        {
            return false;
        }

        float avoidablePathRisk = CalculateAvoidablePathRisk(candidate, heroPosition, heroRadius);
        if (avoidablePathRisk > safeRiskThreshold)
        {
            return false;
        }

        float timedImpactRisk = CalculateTimedImpactRisk(candidate, heroPosition, heroRadius, travelSpeed);
        if (timedImpactRisk > safeRiskThreshold)
        {
            return false;
        }

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
        float score = endpointRisk * 1000f +
                      avoidablePathRisk * 750f +
                      timedImpactRisk * 900f -
                      escapeMargin * 8f +
                      travelDistance * 0.15f -
                      Mathf.Clamp(endpointMinimumDistance, 0f, 6f) * 2f;

        evaluation = new CandidateEvaluation(candidate, score, endpointRisk, endpointMinimumDistance, avoidablePathRisk, timedImpactRisk);
        return true;
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

        float impactRisk = 0f;
        for (int i = 0; i < _threats.Count; i++)
        {
            ThreatZone threat = _threats[i];
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

    private static float GetHeroThreatRadius(Hero hero, PluginConfig config)
    {
        float radius = hero.Control != null ? hero.Control.outerRadius : 0.45f;
        return Mathf.Max(radius + Mathf.Max(config.ThreatPadding, 0f), 0.1f);
    }

    private static float EstimateDodgeTravelSpeed(Hero hero, PluginConfig config)
    {
        float speed = hero.Control != null ? hero.Control.currentMaxAgentSpeed : 0f;
        if (config.AutoDodgeUseMovementSkill && TryGetReadyMovementSkill(hero, out _))
        {
            speed = Mathf.Max(speed, DodgeSkillTravelSpeed);
        }

        return Mathf.Max(speed, MinimumTravelSpeed);
    }

    private static float GetSearchRadius(Hero hero, PluginConfig config)
    {
        float maxSearch = Mathf.Max(config.AutoDodgeSearchRadius, 1f);
        float fallback = Mathf.Clamp(config.AutoDodgeFallbackDistance, 1f, maxSearch);

        if (config.AutoDodgeUseMovementSkill &&
            TryGetReadyMovementSkill(hero, out SkillTrigger movementSkill) &&
            TryGetSkillRange(movementSkill, out float skillRange))
        {
            return Mathf.Clamp(skillRange, 1f, maxSearch);
        }

        return fallback;
    }

    private static bool TryCastMovementSkill(Hero hero, Vector3 destination, PluginConfig config)
    {
        if (!config.AutoDodgeUseMovementSkill ||
            hero.Control == null ||
            hero.Control.isDisplacing ||
            !TryGetReadyMovementSkill(hero, out SkillTrigger movementSkill))
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

    private bool TryMoveTowardPointer(Hero hero, PluginConfig config)
    {
        if (!config.AutoDodgeMoveFallback || hero.Control == null || hero.Control.isDisplacing)
        {
            return false;
        }

        float interval = Mathf.Max(config.AutoDodgeCommandInterval, MinimumCommandInterval);
        if (Time.unscaledTime - _lastPointerMoveTime < interval)
        {
            return false;
        }

        Vector3 cursorPoint = ControlManager.GetWorldPositionOnGroundOnCursor();
        if (!Dew.IsOkay(cursorPoint) || Vector2.Distance(cursorPoint.ToXY(), hero.agentPosition.ToXY()) < 0.25f)
        {
            return false;
        }

        Vector3 destination = Dew.GetValidAgentDestination_LinearSweep(hero.agentPosition, cursorPoint);
        hero.Control.CmdMoveToDestination(destination, immediately: true, speedMult: 1f);
        _lastPointerMoveTime = Time.unscaledTime;
        return true;
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
