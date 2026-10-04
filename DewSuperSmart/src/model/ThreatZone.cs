using System;
using System.Reflection;
using UnityEngine;

namespace DewSuperSmart;

internal enum ThreatZoneKind
{
    Circle,
    Cone,
    Line,
    Polygon,
    OutsideCircles,
    GroundHazard
}

internal enum GroundHazardKind
{
    None,
    Lava,
    LightFog
}

internal enum ThreatSourceKind
{
    EnemyCast,
    ActiveEffect,
    Projectile,
    Environment
}

internal enum ThreatActivity
{
    Preview,
    Imminent,
    Active
}

internal readonly struct ThreatZone
{
    public const float ProjectileSlidingWindowSeconds = 0.45f;
    private const float NearMissFalloff = 1.75f;
    private const float EscapeBoundaryEpsilon = 0.001f;

    private static Component _lightFogSamplerTarget;
    private static Func<Vector3, float> _lightFogSampler;

    public readonly ThreatZoneKind Kind;
    public readonly ThreatSourceKind SourceKind;
    public readonly ThreatActivity Activity;
    public readonly Actor Source;
    public readonly AbilityTrigger Trigger;
    public readonly Projectile Projectile;
    public readonly Vector3 Origin;
    public readonly Vector3 Center;
    public readonly Vector3 Direction;
    public readonly Vector3[] Points;
    public readonly float Radius;
    public readonly float Length;
    public readonly float Width;
    public readonly float Angle;
    public readonly float Weight;
    public readonly float TimeToImpact;
    public readonly float ProjectileSpeed;
    public readonly bool IsDodgeable;
    public readonly bool RequiresDodgeSkill;
    public readonly bool IsProjectileWindow;
    public readonly GroundHazardKind GroundHazard;
    public readonly float GroundHazardThreshold;

    public bool IsProjectile => SourceKind == ThreatSourceKind.Projectile;
    // 飞行轨迹用相对运动预测；飞弹落地圆圈/多边形仍然是需要避开的几何区域。
    public bool IsMovingProjectile => IsProjectile && !IsProjectileWindow &&
                                      Kind == ThreatZoneKind.Line && ProjectileSpeed > 0.01f;
    public bool IsActive => Activity != ThreatActivity.Preview;

    private ThreatZone(
        ThreatZoneKind kind,
        ThreatSourceKind sourceKind,
        ThreatActivity activity,
        Actor source,
        AbilityTrigger trigger,
        Projectile projectile,
        Vector3 origin,
        Vector3 center,
        Vector3 direction,
        Vector3[] points,
        float radius,
        float length,
        float width,
        float angle,
        float weight,
        float timeToImpact,
        float projectileSpeed,
        bool isDodgeable,
        bool requiresDodgeSkill = false,
        GroundHazardKind groundHazard = GroundHazardKind.None,
        float groundHazardThreshold = 0f,
        bool isProjectileWindow = false)
    {
        Kind = kind;
        SourceKind = sourceKind;
        Activity = activity;
        Source = source;
        Trigger = trigger;
        Projectile = projectile;
        Origin = origin;
        Center = center;
        Direction = NormalizeFlat(direction);
        Points = points;
        Radius = radius;
        Length = length;
        Width = width;
        Angle = angle;
        Weight = weight;
        TimeToImpact = timeToImpact;
        ProjectileSpeed = projectileSpeed;
        IsDodgeable = isDodgeable;
        RequiresDodgeSkill = requiresDodgeSkill;
        IsProjectileWindow = isProjectileWindow;
        GroundHazard = groundHazard;
        GroundHazardThreshold = groundHazardThreshold;
    }

    public static ThreatZone Circle(
        Actor source,
        AbilityTrigger trigger,
        Vector3 center,
        float radius,
        ThreatSourceKind sourceKind,
        ThreatActivity activity,
        float weight,
        float timeToImpact,
        bool isDodgeable,
        bool requiresDodgeSkill = false)
    {
        return new ThreatZone(
            ThreatZoneKind.Circle,
            sourceKind,
            activity,
            source,
            trigger,
            sourceKind == ThreatSourceKind.Projectile ? source as Projectile : null,
            center,
            center,
            Vector3.forward,
            null,
            Mathf.Max(radius, 0.01f),
            0f,
            0f,
            360f,
            weight,
            timeToImpact,
            0f,
            isDodgeable,
            requiresDodgeSkill);
    }

    public static ThreatZone Cone(
        Actor source,
        AbilityTrigger trigger,
        Vector3 origin,
        Vector3 direction,
        float radius,
        float angle,
        ThreatActivity activity,
        float weight,
        float timeToImpact,
        bool isDodgeable)
    {
        return new ThreatZone(
            ThreatZoneKind.Cone,
            ThreatSourceKind.EnemyCast,
            activity,
            source,
            trigger,
            null,
            origin,
            origin,
            direction,
            null,
            Mathf.Max(radius, 0.01f),
            0f,
            0f,
            Mathf.Clamp(angle, 0.01f, 360f),
            weight,
            timeToImpact,
            0f,
            isDodgeable);
    }

    public static ThreatZone Line(
        Actor source,
        AbilityTrigger trigger,
        Projectile projectile,
        Vector3 origin,
        Vector3 direction,
        float length,
        float width,
        ThreatSourceKind sourceKind,
        ThreatActivity activity,
        float weight,
        float timeToImpact,
        bool isDodgeable,
        float projectileSpeed = 0f,
        bool isProjectileWindow = false)
    {
        return new ThreatZone(
            ThreatZoneKind.Line,
            sourceKind,
            activity,
            source,
            trigger,
            projectile,
            origin,
            origin,
            direction,
            null,
            0f,
            Mathf.Max(length, 0.01f),
            Mathf.Max(width, 0.01f),
            0f,
            weight,
            timeToImpact,
            Mathf.Max(projectileSpeed, 0f),
            isDodgeable,
            isProjectileWindow: isProjectileWindow);
    }

    public static ThreatZone ProjectileWindow(ThreatZone path, Vector3 position, float remainingLength)
    {
        return Line(path.Source, path.Trigger, path.Projectile, position, path.Direction,
            Mathf.Min(Mathf.Max(remainingLength, 0f), path.ProjectileSpeed * ProjectileSlidingWindowSeconds),
            path.Width, path.SourceKind, ThreatActivity.Active, path.Weight,
            float.PositiveInfinity, true, path.ProjectileSpeed, isProjectileWindow: true);
    }

    public static ThreatZone Polygon(
        Actor source,
        Vector3[] points,
        ThreatSourceKind sourceKind,
        ThreatActivity activity,
        float weight,
        float timeToImpact,
        bool isDodgeable)
    {
        Vector3 center = Vector3.zero;
        if (points != null && points.Length > 0)
        {
            for (int i = 0; i < points.Length; i++)
            {
                center += points[i];
            }

            center /= points.Length;
        }

        return new ThreatZone(
            ThreatZoneKind.Polygon,
            sourceKind,
            activity,
            source,
            null,
            sourceKind == ThreatSourceKind.Projectile ? source as Projectile : null,
            center,
            center,
            Vector3.forward,
            points,
            0f,
            0f,
            0f,
            0f,
            weight,
            timeToImpact,
            0f,
            isDodgeable);
    }

    public static ThreatZone OutsideCircles(
        Actor source,
        Vector3[] centers,
        float radius,
        ThreatSourceKind sourceKind,
        ThreatActivity activity,
        float weight,
        float timeToImpact,
        bool isDodgeable)
    {
        Vector3 center = centers != null && centers.Length > 0 ? centers[0] : Vector3.zero;
        return new ThreatZone(
            ThreatZoneKind.OutsideCircles,
            sourceKind,
            activity,
            source,
            null,
            null,
            center,
            center,
            Vector3.forward,
            centers,
            Mathf.Max(radius, 0.01f),
            0f,
            0f,
            360f,
            weight,
            timeToImpact,
            0f,
            isDodgeable);
    }

    public static ThreatZone Ground(
        Actor source,
        GroundHazardKind groundHazard,
        float threshold,
        float weight)
    {
        return new ThreatZone(
            ThreatZoneKind.GroundHazard,
            ThreatSourceKind.Environment,
            ThreatActivity.Active,
            source,
            null,
            null,
            source != null ? source.position : Vector3.zero,
            source != null ? source.position : Vector3.zero,
            Vector3.forward,
            null,
            0f,
            0f,
            0f,
            360f,
            weight,
            0f,
            0f,
            isDodgeable: true,
            requiresDodgeSkill: false,
            groundHazard,
            threshold);
    }

    public float RiskAt(Vector3 point, float extraRadius)
    {
        float signedDistance = SignedDistance(point, extraRadius);
        if (signedDistance <= 0f)
        {
            return Weight + Mathf.Clamp01(-signedDistance / 2f);
        }

        if (signedDistance < NearMissFalloff)
        {
            return Weight * 0.25f * (1f - signedDistance / NearMissFalloff);
        }

        return 0f;
    }

    public float SignedDistance(Vector3 point, float extraRadius)
    {
        switch (Kind)
        {
            case ThreatZoneKind.Circle:
                return Vector2.Distance(point.ToXY(), Center.ToXY()) - Radius - extraRadius;
            case ThreatZoneKind.Cone:
                return SignedDistanceToCone(point, extraRadius);
            case ThreatZoneKind.Line:
                return DistancePointToSegment(point, Origin, Origin + Direction * Length) - Width * 0.5f - extraRadius;
            case ThreatZoneKind.Polygon:
                return SignedDistanceToPolygon(point, extraRadius);
            case ThreatZoneKind.OutsideCircles:
                return SignedDistanceOutsideCircles(point, extraRadius);
            case ThreatZoneKind.GroundHazard:
                return IsGroundHazardAt(point, extraRadius) ? -0.25f : NearMissFalloff;
            default:
                return float.PositiveInfinity;
        }
    }

    public Vector3 ClosestPoint(Vector3 point)
    {
        switch (Kind)
        {
            case ThreatZoneKind.Circle:
                return Center;
            case ThreatZoneKind.Cone:
            case ThreatZoneKind.Line:
                return ClosestPointOnSegment(point, Origin, Origin + Direction * Length);
            case ThreatZoneKind.Polygon:
                return ClosestPointOnPolygon(point);
            case ThreatZoneKind.OutsideCircles:
                return ClosestSafeCircleCenter(point);
            case ThreatZoneKind.GroundHazard:
                return point;
            default:
                return point;
        }
    }

    // 在真实威胁边缘外保留角色半径和补偿，返回当前威胁自身验证通过的最近候选点。
    public bool TryGetEscapePoint(Vector3 point, float extraRadius, float clearance, out Vector3 escapePoint)
    {
        escapePoint = point;
        if (Kind == ThreatZoneKind.GroundHazard)
        {
            // 地表危险只能采样，不能把未知边界当作已确认的安全边缘。
            return false;
        }

        extraRadius = Mathf.Max(extraRadius, 0f);
        clearance = Mathf.Max(clearance, 0f);
        float currentDistance = SignedDistance(point, extraRadius);
        if (!float.IsInfinity(currentDistance) && currentDistance >= clearance)
        {
            return true;
        }

        float offset = extraRadius + clearance + EscapeBoundaryEpsilon;
        float bestDistance = float.PositiveInfinity;
        switch (Kind)
        {
            case ThreatZoneKind.Circle:
                ConsiderEscapePoint(point, Center + NormalizeFlat(point - Center) * (Radius + offset),
                    extraRadius, clearance, ref escapePoint, ref bestDistance);
                break;
            case ThreatZoneKind.Line:
                Vector3 axisPoint = ClosestPointOnSegment(point, Origin, Origin + Direction * Length);
                Vector3 outward = point - axisPoint;
                outward.y = 0f;
                if (outward.sqrMagnitude <= 0.0001f)
                {
                    outward = new Vector3(Direction.z, 0f, -Direction.x);
                }

                ConsiderEscapePoint(point, axisPoint + outward.normalized * (Width * 0.5f + offset),
                    extraRadius, clearance, ref escapePoint, ref bestDistance);
                break;
            case ThreatZoneKind.Cone:
                FindConeEscapePoint(point, extraRadius, clearance, offset, ref escapePoint, ref bestDistance);
                break;
            case ThreatZoneKind.Polygon:
                FindPolygonEscapePoint(point, extraRadius, clearance, offset, ref escapePoint, ref bestDistance);
                break;
            case ThreatZoneKind.OutsideCircles:
                float safeRadius = Radius - offset;
                if (Points == null || safeRadius < 0f)
                {
                    break;
                }

                for (int i = 0; i < Points.Length; i++)
                {
                    Vector3 delta = point - Points[i];
                    delta.y = 0f;
                    Vector3 candidate = Points[i] + Vector3.ClampMagnitude(delta, safeRadius);
                    ConsiderEscapePoint(point, candidate, extraRadius, clearance, ref escapePoint, ref bestDistance);
                }

                break;
        }

        return !float.IsPositiveInfinity(bestDistance);
    }

    // 复核外扩后的候选，避免凹多边形或宽扇形的另一条边仍然覆盖角色。
    private void ConsiderEscapePoint(
        Vector3 point,
        Vector3 candidate,
        float extraRadius,
        float clearance,
        ref Vector3 bestPoint,
        ref float bestDistance)
    {
        candidate.y = point.y;
        if (SignedDistance(candidate, extraRadius) < clearance)
        {
            return;
        }

        float distance = Vector2.SqrMagnitude(candidate.ToXY() - point.ToXY());
        if (distance < bestDistance)
        {
            bestPoint = candidate;
            bestDistance = distance;
        }
    }

    // 扇形的圆弧、两条边及端点分别外扩，保留有效候选中移动最短的一项。
    private void FindConeEscapePoint(
        Vector3 point,
        float extraRadius,
        float clearance,
        float offset,
        ref Vector3 bestPoint,
        ref float bestDistance)
    {
        Vector3 delta = point - Origin;
        delta.y = 0f;
        Vector3 radial = delta.sqrMagnitude > 0.0001f ? delta.normalized : Direction;
        if (Angle >= 359.99f)
        {
            ConsiderEscapePoint(point, Origin + radial * (Radius + offset),
                extraRadius, clearance, ref bestPoint, ref bestDistance);
            return;
        }

        float halfAngle = Angle * 0.5f;
        Vector3 left = Quaternion.Euler(0f, -halfAngle, 0f) * Direction;
        Vector3 right = Quaternion.Euler(0f, halfAngle, 0f) * Direction;
        if (Vector3.Angle(Direction, radial) > halfAngle)
        {
            radial = Vector3.Dot(radial, left) >= Vector3.Dot(radial, right) ? left : right;
        }

        ConsiderEscapePoint(point, Origin + radial * (Radius + offset),
            extraRadius, clearance, ref bestPoint, ref bestDistance);
        for (int side = 0; side < 2; side++)
        {
            Vector3 edge = side == 0 ? left : right;
            Vector3 normal = side == 0
                ? new Vector3(-edge.z, 0f, edge.x)
                : new Vector3(edge.z, 0f, -edge.x);
            Vector3 edgePoint = ClosestPointOnSegment(point, Origin, Origin + edge * Radius);
            ConsiderEscapePoint(point, edgePoint + normal * offset,
                extraRadius, clearance, ref bestPoint, ref bestDistance);
            Vector3 tip = Origin + edge * Radius;
            Vector3 tipDirection = point - tip;
            tipDirection.y = 0f;
            if (tipDirection.sqrMagnitude <= 0.0001f)
            {
                tipDirection = edge + normal;
            }

            ConsiderEscapePoint(point, tip + tipDirection.normalized * offset,
                extraRadius, clearance, ref bestPoint, ref bestDistance);
        }

        Vector3 apexDirection = delta.sqrMagnitude > 0.0001f ? delta.normalized : -Direction;
        ConsiderEscapePoint(point, Origin + apexDirection * offset,
            extraRadius, clearance, ref bestPoint, ref bestDistance);
        // 大于半圆的扇形在顶点形成凹角，需走到两条外扩边的交点才能完整容纳角色。
        if (halfAngle > 90f)
        {
            float sine = Mathf.Sin(halfAngle * Mathf.Deg2Rad);
            if (sine > 0.0001f)
            {
                ConsiderEscapePoint(point, Origin - Direction * (offset / sine),
                    extraRadius, clearance, ref bestPoint, ref bestDistance);
            }
        }
    }

    // 按多边形绕序确定外法线；凸角使用圆角，凹角额外验证外扩边的交点。
    private void FindPolygonEscapePoint(
        Vector3 point,
        float extraRadius,
        float clearance,
        float offset,
        ref Vector3 bestPoint,
        ref float bestDistance)
    {
        if (Points == null || Points.Length < 3)
        {
            return;
        }

        float area = 0f;
        for (int i = 0; i < Points.Length; i++)
        {
            Vector3 a = Points[i];
            Vector3 b = Points[(i + 1) % Points.Length];
            area += a.x * b.z - b.x * a.z;
        }

        float winding = area >= 0f ? 1f : -1f;
        for (int i = 0; i < Points.Length; i++)
        {
            Vector3 vertex = Points[i];
            Vector3 next = Points[(i + 1) % Points.Length];
            Vector3 previous = Points[(i + Points.Length - 1) % Points.Length];
            Vector3 normal = GetPolygonOutwardNormal(vertex, next, winding);
            if (normal.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            Vector3 edgePoint = ClosestPointOnSegment(point, vertex, next);
            ConsiderEscapePoint(point, edgePoint + normal * offset,
                extraRadius, clearance, ref bestPoint, ref bestDistance);

            Vector3 previousNormal = GetPolygonOutwardNormal(previous, vertex, winding);
            Vector3 bisector = (previousNormal + normal).normalized;
            Vector3 vertexDirection = point - vertex;
            vertexDirection.y = 0f;
            if (vertexDirection.sqrMagnitude <= 0.0001f)
            {
                vertexDirection = bisector;
            }

            ConsiderEscapePoint(point, vertex + vertexDirection.normalized * offset,
                extraRadius, clearance, ref bestPoint, ref bestDistance);
            float projection = Vector3.Dot(bisector, normal);
            if (projection > 0.0001f)
            {
                ConsiderEscapePoint(point, vertex + bisector * (offset / projection),
                    extraRadius, clearance, ref bestPoint, ref bestDistance);
            }
        }
    }

    // 平面坐标采用世界 X/Z；逆时针边的右侧为外侧。
    private static Vector3 GetPolygonOutwardNormal(Vector3 start, Vector3 end, float winding)
    {
        Vector3 edge = end - start;
        edge.y = 0f;
        return new Vector3(edge.z, 0f, -edge.x).normalized * winding;
    }

    private bool IsGroundHazardAt(Vector3 point, float extraRadius)
    {
        if (IsGroundHazardAtSingle(point))
        {
            return true;
        }

        float radius = Mathf.Max(extraRadius, 0f);
        if (radius <= 0.05f)
        {
            return false;
        }

        for (int i = 0; i < 4; i++)
        {
            float angle = i * Mathf.PI * 0.5f;
            Vector3 sample = point + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            if (IsGroundHazardAtSingle(sample))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsGroundHazardAtSingle(Vector3 point)
    {
        switch (GroundHazard)
        {
            case GroundHazardKind.Lava:
                if (Source == null ||
                    !Physics.Raycast(point + Vector3.up * 8f, Vector3.down, out RaycastHit hit, 16f, LayerMasks.Ground))
                {
                    return false;
                }

                return hit.transform == Source.transform || hit.transform.IsChildOf(Source.transform);
            case GroundHazardKind.LightFog:
                Func<Vector3, float> sampler = GetLightFogSampler();
                if (sampler == null)
                {
                    return false;
                }

                try
                {
                    return sampler(point) >= GroundHazardThreshold;
                }
                catch (Exception)
                {
                    _lightFogSamplerTarget = null;
                    _lightFogSampler = null;
                    return false;
                }
            default:
                return false;
        }
    }

    private Func<Vector3, float> GetLightFogSampler()
    {
        if (_lightFogSamplerTarget != null && _lightFogSampler != null)
        {
            return _lightFogSampler;
        }

        if (Source == null)
        {
            return null;
        }

        Type fogType = Source.GetType().Assembly.GetType("Sky_LightFog", throwOnError: false);
        MethodInfo sampleMethod = fogType?.GetMethod(
            "SampleFogOpacity",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(Vector3) },
            modifiers: null);
        Component target = fogType != null ? UnityEngine.Object.FindAnyObjectByType(fogType) as Component : null;
        if (target == null || sampleMethod == null)
        {
            return null;
        }

        _lightFogSamplerTarget = target;
        _lightFogSampler = Delegate.CreateDelegate(
            typeof(Func<Vector3, float>),
            target,
            sampleMethod,
            throwOnBindFailure: false) as Func<Vector3, float>;
        return _lightFogSampler;
    }

    private float SignedDistanceToCone(Vector3 point, float extraRadius)
    {
        Vector3 delta = point - Origin;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (Angle >= 359.99f)
        {
            return distance - Radius - extraRadius;
        }

        float halfAngle = Angle * 0.5f;
        float angleDelta = distance > 0.0001f ? Vector3.Angle(Direction, delta / distance) : 0f;
        Vector3 leftTip = Origin + Quaternion.Euler(0f, -halfAngle, 0f) * Direction * Radius;
        Vector3 rightTip = Origin + Quaternion.Euler(0f, halfAngle, 0f) * Direction * Radius;
        float edgeDistance = Mathf.Min(
            DistancePointToSegment(point, Origin, leftTip),
            DistancePointToSegment(point, Origin, rightTip));
        float arcDistance = angleDelta <= halfAngle
            ? Mathf.Abs(distance - Radius)
            : Mathf.Min(Vector2.Distance(point.ToXY(), leftTip.ToXY()),
                Vector2.Distance(point.ToXY(), rightTip.ToXY()));
        // 先求有限扇形的真实边界距离，再扩张角色圆盘；角度正弦近似会误判背面及端点。
        float boundaryDistance = Mathf.Min(edgeDistance, arcDistance);
        bool inside = distance <= Radius && angleDelta <= halfAngle;
        return (inside ? -boundaryDistance : boundaryDistance) - extraRadius;
    }

    private float SignedDistanceToPolygon(Vector3 point, float extraRadius)
    {
        if (Points == null || Points.Length < 3)
        {
            return float.PositiveInfinity;
        }

        Vector2 query = point.ToXY();
        float minimumDistance = float.PositiveInfinity;
        bool inside = false;

        for (int i = 0, j = Points.Length - 1; i < Points.Length; j = i++)
        {
            Vector2 a = Points[j].ToXY();
            Vector2 b = Points[i].ToXY();
            minimumDistance = Mathf.Min(minimumDistance, DistancePointToSegment(query, a, b));

            if ((a.y > query.y) != (b.y > query.y) &&
                query.x < (b.x - a.x) * (query.y - a.y) / (b.y - a.y) + a.x)
            {
                inside = !inside;
            }
        }

        return (inside ? -minimumDistance : minimumDistance) - extraRadius;
    }

    private Vector3 ClosestPointOnPolygon(Vector3 point)
    {
        if (Points == null || Points.Length == 0)
        {
            return point;
        }

        Vector3 best = Points[0];
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < Points.Length; i++)
        {
            Vector3 candidate = ClosestPointOnSegment(point, Points[i], Points[(i + 1) % Points.Length]);
            float distance = (candidate.ToXY() - point.ToXY()).sqrMagnitude;
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }

    private float SignedDistanceOutsideCircles(Vector3 point, float extraRadius)
    {
        if (Points == null || Points.Length == 0)
        {
            return float.PositiveInfinity;
        }

        float bestSafeMargin = float.NegativeInfinity;
        for (int i = 0; i < Points.Length; i++)
        {
            float safeMargin = Radius - Vector2.Distance(point.ToXY(), Points[i].ToXY()) - extraRadius;
            bestSafeMargin = Mathf.Max(bestSafeMargin, safeMargin);
        }

        return bestSafeMargin;
    }

    private Vector3 ClosestSafeCircleCenter(Vector3 point)
    {
        if (Points == null || Points.Length == 0)
        {
            return point;
        }

        Vector3 best = Points[0];
        float bestDistance = Vector2.SqrMagnitude(point.ToXY() - best.ToXY());
        for (int i = 1; i < Points.Length; i++)
        {
            float distance = Vector2.SqrMagnitude(point.ToXY() - Points[i].ToXY());
            if (distance < bestDistance)
            {
                best = Points[i];
                bestDistance = distance;
            }
        }

        return best;
    }

    private static float DistancePointToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        return DistancePointToSegment(point.ToXY(), start.ToXY(), end.ToXY());
    }

    private static float DistancePointToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float sqrLength = segment.sqrMagnitude;
        if (sqrLength <= 0.0001f)
        {
            return Vector2.Distance(point, start);
        }

        float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / sqrLength);
        return Vector2.Distance(point, start + segment * t);
    }

    private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector2 point2 = point.ToXY();
        Vector2 start2 = start.ToXY();
        Vector2 end2 = end.ToXY();
        Vector2 segment = end2 - start2;
        float sqrLength = segment.sqrMagnitude;
        if (sqrLength <= 0.0001f)
        {
            return start;
        }

        float t = Mathf.Clamp01(Vector2.Dot(point2 - start2, segment) / sqrLength);
        Vector2 closest = start2 + segment * t;
        return new Vector3(closest.x, start.y, closest.y);
    }

    private static Vector3 NormalizeFlat(Vector3 value)
    {
        value.y = 0f;
        return value.sqrMagnitude <= 0.0001f ? Vector3.forward : value.normalized;
    }
}
