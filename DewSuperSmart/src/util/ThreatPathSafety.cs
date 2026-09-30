using System;
using System.Collections.Generic;
using UnityEngine;

namespace DewSuperSmart;

/// <summary>
/// 对角色从起点移动到目标点的路径做几何安全检查。
/// </summary>
internal static class ThreatPathSafety
{
    // SignedDistance 对普通几何威胁是 1-Lipschitz 的，因此每个采样间隔都保留半步余量。
    private const float MaximumSampleStep = 0.1f;
    private const float DistanceTolerance = 0.001f;

    /// <summary>
    /// 检查线性移动路径是否会穿过导航威胁。
    /// 飞行轨迹和必须使用位移技能的威胁由时序逻辑处理；飞弹落地区仍做几何过滤。
    /// </summary>
    public static bool IsSafe(
        IReadOnlyList<ThreatZone> threats,
        Vector3 origin,
        Vector3 destination,
        float heroRadius)
    {
        if (threats == null || threats.Count == 0)
        {
            return true;
        }

        float radius = Mathf.Max(heroRadius, 0f);
        Vector3 delta = destination - origin;
        delta.y = 0f;
        float distance = delta.magnitude;
        int segmentCount = Mathf.Max(1, Mathf.CeilToInt(distance / MaximumSampleStep));
        float segmentLength = distance / segmentCount;

        for (int i = 0; i < threats.Count; i++)
        {
            ThreatZone threat = threats[i];
            if (threat.IsMovingProjectile || threat.RequiresDodgeSkill)
            {
                continue;
            }

            float startDistance = threat.SignedDistance(origin, radius);
            if (float.IsNaN(startDistance) || float.IsNegativeInfinity(startDistance))
            {
                // 无法判定时宁可拒绝路径，避免把未知几何当成安全点。
                return false;
            }

            if (float.IsPositiveInfinity(startDistance))
            {
                // 无效/空多边形等没有可计算的边界，不会对路径产生约束。
                continue;
            }

            if (threat.Kind == ThreatZoneKind.GroundHazard)
            {
                if (!IsGroundPathSafe(threat, origin, destination, radius, segmentCount))
                {
                    return false;
                }

                continue;
            }

            bool hasExited = startDistance > 0f;
            float previousDistance = startDistance;
            for (int sample = 1; sample <= segmentCount; sample++)
            {
                float t = sample / (float)segmentCount;
                Vector3 point = Vector3.Lerp(origin, destination, t);
                float currentDistance = threat.SignedDistance(point, radius);
                if (float.IsNaN(currentDistance) || float.IsNegativeInfinity(currentDistance))
                {
                    return false;
                }

                if (float.IsPositiveInfinity(currentDistance))
                {
                    return false;
                }

                // 由两端距离及间隔长度给出下界，覆盖两个采样点之间的薄线。
                float conservativeDistance = (previousDistance + currentDistance - segmentLength) * 0.5f;
                if (hasExited)
                {
                    // 起点已在威胁外，整段都不能重新进入威胁。
                    if (conservativeDistance < -DistanceTolerance)
                    {
                        return false;
                    }
                }
                else
                {
                    // 内部允许沿切向逐渐逃离（例如两重叠圆之间的垂直方向）。
                    // 通用距离下界不能证明内部方向的单调性，改查端点和中点的实际深度；
                    // 真正离开后，立即恢复严格的段距离下界，防止细线或凹形区域重入。
                    Vector3 midpoint = Vector3.Lerp(origin, destination, (sample - 0.5f) / segmentCount);
                    float midpointDistance = threat.SignedDistance(midpoint, radius);
                    if (float.IsNaN(midpointDistance) ||
                        midpointDistance + DistanceTolerance < previousDistance ||
                        currentDistance + DistanceTolerance < midpointDistance ||
                        midpointDistance > 0f && (midpointDistance + currentDistance - segmentLength * 0.5f) * 0.5f < -DistanceTolerance)
                    {
                        return false;
                    }

                    hasExited = currentDistance > 0f;
                }

                previousDistance = currentDistance;
            }
        }

        return true;
    }

    /// <summary>
    /// 地表威胁的 SignedDistance 是离散的（命中时为负值，否则为固定正值），
    /// 不能使用连续距离的单调约束。起点在其中时允许保持在其中直到真正离开，
    /// 离开后禁止再次进入；这样不会因为跳变而永远无法逃离地表危险区。
    /// </summary>
    private static bool IsGroundPathSafe(
        ThreatZone threat,
        Vector3 origin,
        Vector3 destination,
        float heroRadius,
        int segmentCount)
    {
        bool startsInside = threat.SignedDistance(origin, heroRadius) <= 0f;
        bool hasExited = !startsInside;

        for (int segment = 1; segment <= segmentCount; segment++)
        {
            float startT = (segment - 1) / (float)segmentCount;
            float endT = segment / (float)segmentCount;

            // 对不连续地表在每个小区间加入三个内部采样点，降低薄边界漏检概率。
            if (!CheckGroundSample(threat, origin, destination, heroRadius, Mathf.Lerp(startT, endT, 0.25f), ref hasExited))
            {
                return false;
            }

            if (!CheckGroundSample(threat, origin, destination, heroRadius, Mathf.Lerp(startT, endT, 0.5f), ref hasExited))
            {
                return false;
            }

            if (!CheckGroundSample(threat, origin, destination, heroRadius, Mathf.Lerp(startT, endT, 0.75f), ref hasExited))
            {
                return false;
            }

            if (!CheckGroundSample(threat, origin, destination, heroRadius, endT, ref hasExited))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CheckGroundSample(
        ThreatZone threat,
        Vector3 origin,
        Vector3 destination,
        float heroRadius,
        float t,
        ref bool hasExited)
    {
        Vector3 point = Vector3.Lerp(origin, destination, t);
        float distance = threat.SignedDistance(point, heroRadius);
        if (float.IsNaN(distance) || float.IsNegativeInfinity(distance))
        {
            return false;
        }

        if (float.IsPositiveInfinity(distance))
        {
            return true;
        }

        bool inside = distance <= 0f;
        if (inside)
        {
            // 起点在外部或已经离开后，再次进入就是穿越危险区。
            return !hasExited;
        }

        hasExited = true;
        return true;
    }
}
