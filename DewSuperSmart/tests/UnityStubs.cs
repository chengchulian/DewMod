using System;

namespace UnityEngine
{
    public class Object
    {
        public static Object FindAnyObjectByType(Type type) => throw new NotSupportedException("Unity object discovery is outside the geometry harness.");
    }

    public class Component : Object
    {
        private Transform _transform;
        public Transform transform => this is Transform value ? value : _transform ?? (_transform = new Transform());
    }

    public class MonoBehaviour : Component { }

    public class Transform : Component
    {
        public Vector3 position;
        public Vector3 forward = Vector3.forward;
        public Quaternion rotation;
        public Vector3 Scale = Vector3.one;

        public bool IsChildOf(Transform other) => false;
        public Vector3 lossyScale => Scale;
        public Vector3 TransformPoint(Vector3 point) => position + rotation * Vector3.Scale(point, Scale);
        public Vector3 InverseTransformPoint(Vector3 point)
        {
            Vector3 local = Quaternion.Inverse(rotation) * (point - position);
            return new Vector3(local.x / Scale.x, local.y / Scale.y, local.z / Scale.z);
        }
    }

    public struct Vector2
    {
        public float x;
        public float y;

        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public float sqrMagnitude => x * x + y * y;

        public static Vector2 zero => new Vector2(0f, 0f);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 value, float scale) => new Vector2(value.x * scale, value.y * scale);
        public static Vector2 operator *(float scale, Vector2 value) => value * scale;
        public static Vector2 operator /(Vector2 value, float scale) => new Vector2(value.x / scale, value.y / scale);
        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
        public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;
        public static float SqrMagnitude(Vector2 value) => value.sqrMagnitude;
        public override string ToString() => $"({x},{y})";
    }

    public struct Vector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public float sqrMagnitude => x * x + y * y + z * z;
        public Vector3 normalized => Normalize(this);

        public static Vector3 zero => new Vector3(0f, 0f, 0f);
        public static Vector3 one => new Vector3(1f, 1f, 1f);
        public static Vector3 up => new Vector3(0f, 1f, 0f);
        public static Vector3 down => new Vector3(0f, -1f, 0f);
        public static Vector3 forward => new Vector3(0f, 0f, 1f);
        public void Normalize() => this = Normalize(this);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 value) => new Vector3(-value.x, -value.y, -value.z);
        public static Vector3 operator *(Vector3 value, float scale) => new Vector3(value.x * scale, value.y * scale, value.z * scale);
        public static Vector3 operator *(float scale, Vector3 value) => value * scale;
        public static Vector3 operator /(Vector3 value, float scale) => new Vector3(value.x / scale, value.y / scale, value.z / scale);
        public static bool operator ==(Vector3 a, Vector3 b) => Math.Abs(a.x - b.x) < 0.00001f && Math.Abs(a.y - b.y) < 0.00001f && Math.Abs(a.z - b.z) < 0.00001f;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static float Angle(Vector3 a, Vector3 b)
        {
            float denominator = (float)Math.Sqrt(a.sqrMagnitude * b.sqrMagnitude);
            if (denominator <= 0.000001f) return 0f;
            float cosine = Mathf.Clamp(Dot(a, b) / denominator, -1f, 1f);
            return (float)Math.Acos(cosine) * Mathf.Rad2Deg;
        }

        public static Vector3 ClampMagnitude(Vector3 value, float maxLength)
        {
            if (value.sqrMagnitude <= maxLength * maxLength) return value;
            return value.normalized * maxLength;
        }

        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + (b - a) * t;
        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);

        private static Vector3 Normalize(Vector3 value)
        {
            float length = value.magnitude;
            return length > 0.000001f ? value / length : zero;
        }

        public override bool Equals(object obj) => obj is Vector3 other && this == other;
        public override int GetHashCode() => unchecked((x.GetHashCode() * 397) ^ (y.GetHashCode() * 17) ^ z.GetHashCode());
        public override string ToString() => $"({x},{y},{z})";
    }

    public struct Quaternion
    {
        private float radians;
        private Quaternion(float radians) => this.radians = radians;

        public static Quaternion Euler(float x, float y, float z) => new Quaternion(y * Mathf.Deg2Rad);
        public static Quaternion Inverse(Quaternion value) => new Quaternion(-value.radians);

        public static Vector3 operator *(Quaternion rotation, Vector3 value)
        {
            float cos = (float)Math.Cos(rotation.radians);
            float sin = (float)Math.Sin(rotation.radians);
            return new Vector3(value.x * cos + value.z * sin, value.y, -value.x * sin + value.z * cos);
        }
    }

    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public const float Deg2Rad = (float)Math.PI / 180f;
        public const float Rad2Deg = 180f / (float)Math.PI;
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Abs(float value) => Math.Abs(value);
        public static float Sqrt(float value) => (float)Math.Sqrt(value);
        public static float Sin(float value) => (float)Math.Sin(value);
        public static float Cos(float value) => (float)Math.Cos(value);
        public static float Acos(float value) => (float)Math.Acos(value);
        public static float Clamp(float value, float min, float max) => Math.Min(Math.Max(value, min), max);
        public static float Clamp01(float value) => Clamp(value, 0f, 1f);
        public static float Clamp01(double value) => Clamp01((float)value);
        public static int CeilToInt(float value) => (int)Math.Ceiling(value);
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static bool Approximately(float a, float b) => Abs(a - b) <= 0.00001f * Max(1f, Max(Abs(a), Abs(b)));
    }

    public struct RaycastHit
    {
        public Transform transform;
    }

    public static class Physics
    {
        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float distance, int layerMask)
        {
            throw new NotSupportedException("Unity Physics.Raycast is outside the geometry harness.");
        }
    }
}
