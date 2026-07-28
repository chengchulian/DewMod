using UnityEngine;

namespace DewSuperSmart;

internal enum ThreatZoneKind
{
    Circle,
    Cone,
    Line
}

internal readonly struct ThreatZone
{
    private const float NearMissFalloff = 1.75f;

    public readonly ThreatZoneKind Kind;
    public readonly Entity Source;
    public readonly AbilityTrigger Trigger;
    public readonly Projectile Projectile;
    public readonly Vector3 Origin;
    public readonly Vector3 Center;
    public readonly Vector3 Direction;
    public readonly float Radius;
    public readonly float Length;
    public readonly float Width;
    public readonly float Angle;
    public readonly float Weight;
    public readonly float TimeToImpact;
    public readonly bool IsReady;
    public readonly bool IsProjectile;

    private ThreatZone(
        ThreatZoneKind kind,
        Entity source,
        AbilityTrigger trigger,
        Projectile projectile,
        Vector3 origin,
        Vector3 center,
        Vector3 direction,
        float radius,
        float length,
        float width,
        float angle,
        float weight,
        float timeToImpact,
        bool isReady,
        bool isProjectile)
    {
        Kind = kind;
        Source = source;
        Trigger = trigger;
        Projectile = projectile;
        Origin = origin;
        Center = center;
        Direction = NormalizeFlat(direction);
        Radius = radius;
        Length = length;
        Width = width;
        Angle = angle;
        Weight = weight;
        TimeToImpact = timeToImpact;
        IsReady = isReady;
        IsProjectile = isProjectile;
    }

    public static ThreatZone Circle(Entity source, AbilityTrigger trigger, Vector3 center, float radius, bool isReady, float weight, float timeToImpact = float.PositiveInfinity)
    {
        return new ThreatZone(ThreatZoneKind.Circle, source, trigger, null, center, center, Vector3.forward, radius, 0f, 0f, 360f, weight, timeToImpact, isReady, false);
    }

    public static ThreatZone Cone(Entity source, AbilityTrigger trigger, Vector3 origin, Vector3 direction, float radius, float angle, bool isReady, float weight, float timeToImpact = float.PositiveInfinity)
    {
        return new ThreatZone(ThreatZoneKind.Cone, source, trigger, null, origin, origin, direction, radius, 0f, 0f, angle, weight, timeToImpact, isReady, false);
    }

    public static ThreatZone Line(Entity source, AbilityTrigger trigger, Vector3 origin, Vector3 direction, float length, float width, bool isReady, float weight, float timeToImpact = float.PositiveInfinity)
    {
        return new ThreatZone(ThreatZoneKind.Line, source, trigger, null, origin, origin, direction, 0f, length, width, 0f, weight, timeToImpact, isReady, false);
    }

    public static ThreatZone ProjectileLine(Projectile projectile, Entity source, Vector3 origin, Vector3 direction, float length, float width, float weight, float timeToImpact)
    {
        return new ThreatZone(ThreatZoneKind.Line, source, null, projectile, origin, origin, direction, 0f, length, width, 0f, weight, timeToImpact, true, true);
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
            default:
                return point;
        }
    }

    private float SignedDistanceToCone(Vector3 point, float extraRadius)
    {
        Vector3 delta = point - Origin;
        delta.y = 0f;

        float distance = delta.magnitude;
        if (distance <= 0.001f)
        {
            return -extraRadius;
        }

        float halfAngle = Mathf.Max(Angle * 0.5f, 0.01f);
        float angleDelta = Vector3.Angle(Direction, delta / distance);
        float radialDistance = distance - Radius - extraRadius;
        float angularDistance = Mathf.Sin(Mathf.Max(angleDelta - halfAngle, 0f) * Mathf.Deg2Rad) * distance - extraRadius;

        if (angleDelta <= halfAngle && radialDistance <= 0f)
        {
            float radialInside = Radius + extraRadius - distance;
            float angularInside = (halfAngle - angleDelta) * Mathf.Deg2Rad * distance + extraRadius;
            return -Mathf.Min(radialInside, angularInside);
        }

        return Mathf.Max(radialDistance, angularDistance);
    }

    private static float DistancePointToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        return Vector2.Distance(point.ToXY(), ClosestPointOnSegment(point, start, end).ToXY());
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
        if (value.sqrMagnitude <= 0.0001f)
        {
            return Vector3.forward;
        }

        return value.normalized;
    }
}
