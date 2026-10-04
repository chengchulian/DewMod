using UnityEngine;

namespace DewSuperSmart
{
    public static class VectorExtensions
    {
        public static Vector2 ToXY(this Vector3 value) => new Vector2(value.x, value.z);
    }

    public class Actor : Component
    {
        public Vector3 position;
        public Actor parentActor;
    }

    public class AbilityTrigger : Actor
    {
        public Actor owner;
    }

    public class AttackTrigger : AbilityTrigger { }

    public class Projectile : Actor
    {
    }

    public static class LayerMasks
    {
        public const int Ground = 1;
    }
}
