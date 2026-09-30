using System;
using System.Collections.Generic;
using System.Reflection;
using DewSuperSmart.config;
using UnityEngine;
using UnityEngine.AI;

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
    private const float MinimumCandidateDistance = 0.25f;
    private const float MinimumSafeThreatDistance = 0.1f;
    // 游戏会在距目标 sqrt(0.1) 内停止；额外补偿确保停下时碰撞圆仍在边界外。
    private const float DestinationArrivalDistance = 0.33f;
    private const float MovementStallTimeout = 0.6f;
    private const float DestinationChangeThreshold = 0.35f;
    private const float MinimumTravelSpeed = 2f;
    private const float FallbackDodgeSkillTravelSpeed = 12f;
    private const float MovementResponseDelay = 0.04f;
    private const float MovementSkillCommandLeadTime = 0.04f;
    private const float FallbackDodgeSkillUncollidableRatio = 0.7f;
    private const float EmergencyDodgeMaxTimeToImpact = 0.1f;
    private const float MovementSkillCommandLockoutPadding = 0.1f;
    private const float MovementSkillLandingClearance = 0.35f;
    private const float PostDodgeRecoveryDuration = 0.2f;
    private const float ProjectilePredictionStep = 0.025f;
    private const float ProjectileArrivalSafetyWindow = 0.45f;
    private const float DenseBarrageArrivalSafetyWindow = 1.15f;
    private const float SideDodgePreferenceWeight = 5f;
    private const float EmergencySearchRadiusBonus = 1.5f;
    private const string PrimusMeteorProjectileTypeName =
        "Ai_Mon_Primus_BossPrimusAeron_Adapt_Doom_Meteor_SubFireball";

    private readonly List<ThreatZone> _threats = new List<ThreatZone>(128);
    private readonly List<ThreatZone> _navigationThreats = new List<ThreatZone>(192);
    // 只读取本地导航目标，避免缓存失效时停止或吞掉玩家自己发出的移动命令。
    private static readonly FieldInfo DesiredDestinationField = typeof(EntityControl).GetField(
        "_desiredAgentDestination", BindingFlags.Instance | BindingFlags.NonPublic);
    private Hero _hero;
    private Vector3 _lastProgressPosition;
    private float _lastProgressTime;

    private float _keyDownTime = float.NegativeInfinity;
    private float _lastCommandTime = float.NegativeInfinity;
    private float _nextThreatCollectTime = float.NegativeInfinity;
    private float _threatSnapshotTime = float.NegativeInfinity;
    private float _movementSkillLockUntil = float.NegativeInfinity;
    private float _safeRecoveryUntil = float.NegativeInfinity;
    private Vector3 _activeSafeDestination;
    private Vector3 _lastCommandDestination;
    private AutoDodgeThreatLevel _lastAutoDodgeLevel;
    private bool _hasThreatSnapshot;
    private bool _hasActiveSafeDestination;
    private bool _hasLastCommandDestination;

    // 保持安全目标；只有当前位置或已发路径失效时才重新规划。
    private void Update()
    {
        DewSuperSmart instance = DewSuperSmart.Instance;
        if (instance == null || !instance.Config.EnableAutoDodge || !IsAutoDodgeActive(instance.Config) ||
            !TryGetLocalHero(out Hero hero))
        {
            InvalidateThreatSnapshot();
            return;
        }

        PluginConfig config = instance.Config;
        if (_hero != hero)
        {
            InvalidateThreatSnapshot();
            _hero = hero;
        }

        float heroRadius = GetHeroThreatRadius(hero, config);
        RefreshThreatSnapshot(hero, config, heroRadius);
        RefreshMovementProgress(hero);
        if (Time.unscaledTime < _movementSkillLockUntil || hero.Control == null || hero.Control.isDisplacing)
        {
            return;
        }

        if (_hasActiveSafeDestination && TryContinueSafeDestination(hero, config, heroRadius))
        {
            return;
        }

        _hasActiveSafeDestination = false;
        if (IsCurrentPositionSafe(hero.agentPosition, heroRadius, config))
        {
            StopUnsafeOwnedMovement(hero, config, heroRadius);
            if (Time.unscaledTime >= _safeRecoveryUntil)
            {
                TryMoveTowardCursorWhileHeld(hero, config);
            }

            return;
        }

        bool hasIncomingImpact = TryGetEarliestIncomingImpact(hero.agentPosition, heroRadius, out float earliestImpact);
        bool hasMonsterThreat = TryGetSideDodgeAxis(hero.agentPosition, heroRadius, out Vector3 sideDodgeAxis);
        bool hasMovementPoint = TryFindSafePoint(
            hero, config, heroRadius, GetMovementSearchRadius(config, earliestImpact),
            EstimateMovementSpeed(hero), MovementResponseDelay, minimumTravelDistance: 0f,
            uncollidableRatio: 0f, isMovementSkill: false, hasMonsterThreat, sideDodgeAxis,
            out Vector3 movementPoint);

        float timeSinceLastCommand = Time.unscaledTime - _lastCommandTime;
        float commandInterval = Mathf.Max(config.AutoDodgeCommandInterval, MinimumCommandInterval);
        bool canIssueMovementCommand = timeSinceLastCommand >= commandInterval ||
                                       hasIncomingImpact && earliestImpact <= 0.25f &&
                                       timeSinceLastCommand >= MinimumCommandInterval;
        if (hasMovementPoint && canIssueMovementCommand && TryMoveToSafePoint(hero, movementPoint, config))
        {
            return;
        }

        if (!hasMovementPoint && hasIncomingImpact &&
            TryGetAutoDodgeMovementSkill(hero, config, out SkillTrigger movementSkill) &&
            TryGetDodgeSkillProfile(movementSkill, config, out DodgeSkillProfile skillProfile) &&
            earliestImpact <= EmergencyDodgeMaxTimeToImpact + skillProfile.ActivationDelay &&
            TryFindSafePoint(
                hero, config, heroRadius, skillProfile.SearchRadius, skillProfile.TravelSpeed,
                skillProfile.ActivationDelay, skillProfile.MinimumDistance, skillProfile.UncollidableRatio,
                isMovementSkill: true, hasMonsterThreat, sideDodgeAxis, out Vector3 skillPoint) &&
            TryCastMovementSkill(hero, skillPoint, config, skillProfile))
        {
            _lastCommandTime = Time.unscaledTime;
            return;
        }

        // 没有完整安全方案时取消本 mod 的旧危险路径，不把“风险较低”误当成安全。
        StopUnsafeOwnedMovement(hero, config, heroRadius);
    }

    // 已在补偿后的碰撞边界外时不因邻近威胁累计评分而反复换点。
    private bool IsCurrentPositionSafe(Vector3 position, float heroRadius, PluginConfig config)
    {
        for (int i = 0; i < _threats.Count; i++)
        {
            ThreatZone threat = _threats[i];
            if (!threat.IsMovingProjectile &&
                (threat.RequiresDodgeSkill || threat.SignedDistance(position, heroRadius) <= 0f))
            {
                return false;
            }
        }

        return CalculateMovingProjectileRisk(position, position, heroRadius, 0f, 0f, 0f, false) <=
               Mathf.Max(config.AutoDodgeRiskThreshold, 0.01f);
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

        int previousThreatCount = _threats.Count;
        IReadOnlyList<ThreatZone> snapshot = DewSuperSmart.Instance.Threats.GetThreats(hero, config);
        _navigationThreats.Clear();
        for (int i = 0; i < snapshot.Count; i++)
        {
            ThreatZone threat = snapshot[i];
            if (threat.IsDodgeable && threat.IsActive)
            {
                _navigationThreats.Add(threat);
            }
        }

        _threats.Clear();
        for (int i = 0; i < snapshot.Count; i++)
        {
            _threats.Add(snapshot[i]);
        }

        FilterThreatsByDodgeLevel(level, hero.agentPosition, heroRadius);
        if (previousThreatCount > 0 && _threats.Count == 0)
        {
            // 快照切换存在一个帧间隙，保留极短恢复期，避免立即沿鼠标方向穿回刚结束的区域。
            _safeRecoveryUntil = Mathf.Max(_safeRecoveryUntil, now + PostDodgeRecoveryDuration);
        }
        _lastAutoDodgeLevel = level;
        _hasThreatSnapshot = true;
        _threatSnapshotTime = now;
        _nextThreatCollectTime = now + ThreatCollectInterval;
    }

    private void InvalidateThreatSnapshot()
    {
        _hero = null;
        _threats.Clear();
        _navigationThreats.Clear();
        ClearActiveSafeDestination(resetLastCommand: true);
        _hasThreatSnapshot = false;
        _nextThreatCollectTime = float.NegativeInfinity;
        _threatSnapshotTime = float.NegativeInfinity;
        _movementSkillLockUntil = float.NegativeInfinity;
        _safeRecoveryUntil = float.NegativeInfinity;
    }

    // 到达后保持安全站位，行进中复核已有路径；鼠标移动不会使已安全目标换边。
    private bool TryContinueSafeDestination(Hero hero, PluginConfig config, float heroRadius)
    {
        Vector3 position = hero.agentPosition;
        float distance = Vector2.Distance(position.ToXY(), _activeSafeDestination.ToXY());
        if (distance <= DestinationArrivalDistance &&
            IsCurrentPositionSafe(position, heroRadius, config))
        {
            if (_navigationThreats.Count > 0)
            {
                return true;
            }

            _safeRecoveryUntil = Time.unscaledTime + PostDodgeRecoveryDuration;
            return false;
        }

        if (!IsFollowingOwnedMovement(hero) ||
            !IsNavigationCandidateSafe(_activeSafeDestination, position, heroRadius, MinimumSafeThreatDistance) ||
            CalculateTimedImpactRisk(_activeSafeDestination, position, heroRadius, EstimateMovementSpeed(hero),
                MovementResponseDelay, 0f, false) > Mathf.Max(config.AutoDodgeRiskThreshold, 0.01f))
        {
            return false;
        }

        // 路径被打断或卡住时允许重规划，不能让过期缓存永久吞掉后续命令。
        return Time.unscaledTime - _lastProgressTime < MovementStallTimeout;
    }

    private void ClearActiveSafeDestination(bool resetLastCommand)
    {
        _hasActiveSafeDestination = false;
        if (resetLastCommand)
        {
            _hasLastCommandDestination = false;
        }
    }

    // 源码中的本地目标会在抵达、停止、其他移动指令覆盖时改变。
    private bool IsFollowingOwnedMovement(Hero hero)
    {
        return _hasLastCommandDestination && hero.Control != null &&
               DesiredDestinationField?.GetValue(hero.Control) is Vector3 destination &&
               Vector2.Distance(destination.ToXY(), _lastCommandDestination.ToXY()) < 0.05f;
    }

    // 长按鼠标移动也刷新进度，持续行进不会被误判为卡住后再次发送同一指令。
    private void RefreshMovementProgress(Hero hero)
    {
        if (IsFollowingOwnedMovement(hero) &&
            Vector2.Distance(hero.agentPosition.ToXY(), _lastProgressPosition.ToXY()) >= 0.05f)
        {
            _lastProgressPosition = hero.agentPosition;
            _lastProgressTime = Time.unscaledTime;
        }
    }

    // 仅停止仍属于本 mod 且已经不安全的路径，停止后清理记录以免重复发 CmdStop。
    private void StopUnsafeOwnedMovement(Hero hero, PluginConfig config, float heroRadius)
    {
        if (!IsFollowingOwnedMovement(hero) ||
            IsNavigationCandidateSafe(_lastCommandDestination, hero.agentPosition, heroRadius, MinimumSafeThreatDistance) &&
            CalculateTimedImpactRisk(_lastCommandDestination, hero.agentPosition, heroRadius,
                EstimateMovementSpeed(hero), MovementResponseDelay, 0f, false) <=
            Mathf.Max(config.AutoDodgeRiskThreshold, 0.01f))
        {
            return;
        }

        hero.Control.CmdStop();
        ClearActiveSafeDestination(resetLastCommand: true);
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

    // 优先求真实边缘，重叠区域再用环形候选补充，并只返回完全通过检查的点。
    private bool TryFindSafePoint(
        Hero hero,
        PluginConfig config,
        float heroRadius,
        float searchRadius,
        float travelSpeed,
        float responseDelay,
        float minimumTravelDistance,
        float uncollidableRatio,
        bool isMovementSkill,
        bool hasMonsterThreat,
        Vector3 sideDodgeAxis,
        out Vector3 safePoint)
    {
        Vector3 heroPosition = hero.agentPosition;
        safePoint = heroPosition;
        float safeRiskThreshold = Mathf.Max(config.AutoDodgeRiskThreshold, 0.01f);
        float clearance = MinimumSafeThreatDistance +
                          (isMovementSkill ? MovementSkillLandingClearance : DestinationArrivalDistance);
        Vector3 desiredDirection = GetDesiredDirection(heroPosition);
        Vector3 best = heroPosition;
        float bestScore = float.PositiveInfinity;
        float minimumDistance = Mathf.Max(minimumTravelDistance, MinimumCandidateDistance);

        void ConsiderRawPoint(Vector3 rawPoint)
        {
            if (Vector2.Distance(rawPoint.ToXY(), heroPosition.ToXY()) > searchRadius + 0.001f)
            {
                return;
            }

            // 重叠威胁的环形候选沿原方向收敛到完整安全边缘，避免为评分追求过多净空。
            Vector3 candidate = RefineBoundaryPoint(heroPosition, rawPoint, heroRadius, clearance, minimumDistance);
            candidate = GetValidDodgeDestination(heroPosition, candidate);
            float distance = Vector2.Distance(candidate.ToXY(), heroPosition.ToXY());
            if (!Dew.IsOkay(candidate) || distance < minimumDistance || distance > searchRadius + 0.001f ||
                !IsNavigationCandidateSafe(candidate, heroPosition, heroRadius, clearance) ||
                CalculateTimedImpactRisk(candidate, heroPosition, heroRadius, travelSpeed,
                    responseDelay, uncollidableRatio, isMovementSkill) > safeRiskThreshold)
            {
                return;
            }

            // 安全是硬条件；评分主要取最短路程，鼠标意图与侧向偏好仅用于接近的候选。
            Vector3 direction = candidate - heroPosition;
            direction.y = 0f;
            float intentPenalty = desiredDirection.sqrMagnitude > 0.0001f
                ? (1f - Vector3.Dot(desiredDirection, direction.normalized)) * 0.05f
                : 0f;
            float sidePenalty = hasMonsterThreat
                ? GetSideDodgePenalty(candidate, heroPosition, sideDodgeAxis) * 0.05f
                : 0f;
            float score = distance + intentPenalty + sidePenalty;
            if (score < bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        for (int i = 0; i < _navigationThreats.Count; i++)
        {
            ThreatZone threat = _navigationThreats[i];
            if (!threat.IsMovingProjectile && !threat.RequiresDodgeSkill &&
                threat.TryGetEscapePoint(heroPosition, heroRadius, clearance, out Vector3 edgePoint))
            {
                ConsiderRawPoint(edgePoint);
            }
        }

        for (int ring = 1; ring <= RingCount; ring++)
        {
            float distance = Mathf.Lerp(Mathf.Min(minimumDistance, searchRadius), searchRadius, ring / (float)RingCount);
            int samples = BaseDirectionSamples + ring * 8;
            for (int i = 0; i < samples; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, i * 360f / samples, 0f) * Vector3.forward;
                ConsiderRawPoint(heroPosition + direction * distance);
            }
        }

        safePoint = best;
        return !float.IsPositiveInfinity(bestScore);
    }

    // 只在起点仍有几何威胁时收短移动；投射物躲避需要保留原有的时序搜索空间。
    private Vector3 RefineBoundaryPoint(
        Vector3 origin, Vector3 candidate, float heroRadius, float clearance, float minimumDistance)
    {
        float distance = Vector2.Distance(origin.ToXY(), candidate.ToXY());
        if (distance <= minimumDistance ||
            IsEndpointClear(origin, heroRadius, clearance) ||
            !IsEndpointClear(candidate, heroRadius, clearance))
        {
            return candidate;
        }

        float low = minimumDistance / distance;
        float high = 1f;
        for (int i = 0; i < 12; i++)
        {
            float middle = (low + high) * 0.5f;
            if (IsEndpointClear(Vector3.Lerp(origin, candidate, middle), heroRadius, clearance + 0.001f))
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }

        return Vector3.Lerp(origin, candidate, high);
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

    // 检查所有已收集的威胁；触发等级只决定何时躲避，不限制落点的安全检查范围。
    private bool IsEndpointClear(Vector3 point, float heroRadius, float clearance)
    {
        for (int i = 0; i < _navigationThreats.Count; i++)
        {
            ThreatZone threat = _navigationThreats[i];
            if (threat.IsMovingProjectile || threat.RequiresDodgeSkill)
            {
                continue;
            }

            float distance = threat.SignedDistance(point, heroRadius);
            if (float.IsNaN(distance) || distance < clearance)
            {
                return false;
            }
        }

        return true;
    }

    // 预测使用直线路径，因此拒绝需要绕墙的导航；不能把“可达”当成“直线安全”。
    private bool IsNavigationCandidateSafe(
        Vector3 candidate, Vector3 heroPosition, float heroRadius, float clearance)
    {
        return Dew.IsOkay(candidate) &&
               IsEndpointClear(candidate, heroRadius, clearance) &&
               !NavMesh.Raycast(heroPosition, candidate, out _, NavMesh.AllAreas) &&
               Dew.GetNavMeshPath(heroPosition, candidate).status == NavMeshPathStatus.PathComplete &&
               ThreatPathSafety.IsSafe(_navigationThreats, heroPosition, candidate, heroRadius);
    }

    // 沿途和抵达后的短窗口都要安全，包含因躲避等级而未触发的投射物。
    private float CalculateTimedImpactRisk(
        Vector3 candidate,
        Vector3 heroPosition,
        float heroRadius,
        float travelSpeed,
        float responseDelay,
        float uncollidableRatio,
        bool isMovementSkill)
    {
        float displacementTime = Vector2.Distance(candidate.ToXY(), heroPosition.ToXY()) /
                                 Mathf.Max(travelSpeed, MinimumTravelSpeed);
        float travelTime = responseDelay + displacementTime;
        float impactRisk = CalculateMovingProjectileRisk(candidate, heroPosition, heroRadius,
            travelTime, responseDelay, uncollidableRatio, isMovementSkill);
        for (int i = 0; i < _navigationThreats.Count; i++)
        {
            ThreatZone threat = _navigationThreats[i];
            if (threat.IsMovingProjectile)
            {
                continue;
            }

            float timeToImpact = GetRemainingTimeToImpact(threat);
            if (float.IsNaN(timeToImpact) || float.IsInfinity(timeToImpact) ||
                timeToImpact < 0f || timeToImpact > travelTime + ProjectileArrivalSafetyWindow ||
                IsProtectedByDodgeSkill(timeToImpact, responseDelay, displacementTime, uncollidableRatio, isMovementSkill))
            {
                continue;
            }

            if (threat.RequiresDodgeSkill)
            {
                impactRisk += threat.Weight + 1f;
                continue;
            }

            // 已经生效的区域允许单调逃离；未来的落点伤害必须在命中时真正离开。
            if (timeToImpact <= 0f)
            {
                continue;
            }

            float progress = displacementTime > 0.001f
                ? Mathf.Clamp01((timeToImpact - responseDelay) / displacementTime)
                : 1f;
            if (threat.SignedDistance(Vector3.Lerp(heroPosition, candidate, progress), heroRadius) <= 0f)
            {
                impactRisk += threat.Weight + 1f;
            }
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

        for (int i = 0; i < _navigationThreats.Count; i++)
        {
            ThreatZone threat = _navigationThreats[i];
            if (!threat.IsMovingProjectile)
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
        if (triggerConfig.castMethod.type != CastMethodType.Point)
        {
            // 角度/目标施法不能指定已验证的落点，自动使用会把候选距离误当作真实位移距离。
            return false;
        }

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

        if (minimumDistance > Mathf.Min(skillRange, maxSearch))
        {
            return false;
        }

        profile = new DodgeSkillProfile(
            Mathf.Min(skillRange, maxSearch),
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

        // 施法的最终落点必须重新校验，不能先验证远端安全点、再截短回危险区。
        destination = ClampPointToSkillRange(hero.agentPosition, destination, movementSkill.currentConfig);
        destination = GetValidDodgeDestination(hero.agentPosition, destination);
        float travelDistance = Vector2.Distance(hero.agentPosition.ToXY(), destination.ToXY());
        float heroRadius = GetHeroThreatRadius(hero, config);
        if (travelDistance < skillProfile.MinimumDistance || travelDistance > skillProfile.SearchRadius + 0.001f ||
            !IsNavigationCandidateSafe(
                destination,
                hero.agentPosition,
                heroRadius,
                MinimumSafeThreatDistance + MovementSkillLandingClearance) ||
            CalculateTimedImpactRisk(destination, hero.agentPosition, heroRadius,
                skillProfile.TravelSpeed, skillProfile.ActivationDelay, skillProfile.UncollidableRatio, true) >
            Mathf.Max(config.AutoDodgeRiskThreshold, 0.01f))
        {
            return false;
        }

        CastInfo info = BuildMovementCastInfo(hero, movementSkill, destination);
        hero.Control.CmdCast(movementSkill, movementSkill.currentConfigIndex, info, allowMoveToCast: false, skipRangeCheck: false);

        if (!movementSkill.currentConfig.postponeBasicCommand)
        {
            hero.Control.CmdAttack(null, doChase: false);
        }

        float displacementTime = travelDistance / Mathf.Max(skillProfile.TravelSpeed, MinimumTravelSpeed);
        float invulnerabilityDuration = skillProfile.ActivationDelay +
                                        displacementTime * skillProfile.UncollidableRatio;
        _movementSkillLockUntil = Time.unscaledTime +
                                  skillProfile.ActivationDelay + displacementTime +
                                  MovementSkillCommandLockoutPadding;
        _activeSafeDestination = destination;
        _hasActiveSafeDestination = true;
        _hasLastCommandDestination = false;
        // 位移结束后再保留一个短恢复窗口，防止长按输入立刻把落点带回威胁区。
        _safeRecoveryUntil = Mathf.Max(
            _safeRecoveryUntil,
            Time.unscaledTime + invulnerabilityDuration + PostDodgeRecoveryDuration);
        return true;
    }

    // 相同且仍在执行的目标只发送一次；中断或卡住后允许重新发送。
    private bool TryMoveToSafePoint(Hero hero, Vector3 destination, PluginConfig config, bool rememberAsSafeDestination = true)
    {
        if (!config.AutoDodgeMoveFallback || hero.Control == null || hero.Control.isDisplacing)
        {
            return false;
        }

        if (IsFollowingOwnedMovement(hero) &&
            Vector2.Distance(destination.ToXY(), _lastCommandDestination.ToXY()) < DestinationChangeThreshold &&
            Time.unscaledTime - _lastProgressTime < MovementStallTimeout)
        {
            return false;
        }

        hero.Control.CmdMoveToDestination(destination, immediately: true, speedMult: 1f);
        _lastCommandTime = Time.unscaledTime;
        _lastCommandDestination = destination;
        _hasLastCommandDestination = true;
        _lastProgressPosition = hero.agentPosition;
        _lastProgressTime = Time.unscaledTime;
        _hasActiveSafeDestination = rememberAsSafeDestination;
        if (rememberAsSafeDestination)
        {
            _activeSafeDestination = destination;
        }

        return true;
    }

    // 长按仍可向安全鼠标位置移动，危险鼠标位置不触发另一次无必要的自动选点。
    private void TryMoveTowardCursorWhileHeld(Hero hero, PluginConfig config)
    {
        KeyCode key = config.AutoDodgeKey;
        float commandInterval = Mathf.Max(config.AutoDodgeCommandInterval, MinimumCommandInterval);
        if (key == KeyCode.None || !Input.GetKey(key) ||
            Time.unscaledTime - _lastCommandTime < commandInterval)
        {
            return;
        }

        if (TryGetSafeCursorPoint(hero, config, out Vector3 cursorPoint))
        {
            TryMoveToSafePoint(hero, cursorPoint, config, rememberAsSafeDestination: false);
        }
    }

    private bool TryGetSafeCursorPoint(Hero hero, PluginConfig config, out Vector3 safePoint)
    {
        safePoint = Vector3.zero;
        Vector3 cursorPoint = ControlManager.GetWorldPositionOnGroundOnCursor();
        if (!Dew.IsOkay(cursorPoint))
        {
            return false;
        }

        Vector3 origin = hero.agentPosition;
        Vector3 candidate = GetValidDodgeDestination(origin, cursorPoint);
        float heroRadius = GetHeroThreatRadius(hero, config);
        if (Vector2.Distance(candidate.ToXY(), origin.ToXY()) <= DestinationArrivalDistance ||
            !IsNavigationCandidateSafe(candidate, origin, heroRadius, MinimumSafeThreatDistance + DestinationArrivalDistance) ||
            CalculateTimedImpactRisk(candidate, origin, heroRadius, EstimateMovementSpeed(hero),
                MovementResponseDelay, 0f, false) > Mathf.Max(config.AutoDodgeRiskThreshold, 0.01f))
        {
            return false;
        }

        safePoint = candidate;
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

}
