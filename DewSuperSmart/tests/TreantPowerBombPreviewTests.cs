using System;

namespace DewSuperSmart
{
    internal static class TreantPowerBombPreviewTests
    {
        public static void Register(Action<string, Action> run)
        {
            run("treant power bomb preview uses its damage collider and post-channel delay", UsesDamageTemplate);
            run("other damage skills do not receive treant preview overrides", IgnoresOtherSkills);
        }

        private static void UsesDamageTemplate()
        {
            var instance = new Ai_Mon_Forest_Treant_PowerBomb
            {
                damageDelay = 0.05f,
                Colliders = new[]
                {
                    new DewCollider { shape = DewCollider.ColliderShape.Circle, radius = 5f },
                    new DewCollider { shape = DewCollider.ColliderShape.Box, radius = 20f }
                }
            };
            Assert(TreantPowerBombPreview.TryGet(instance, out float radius, out float delay),
                "the treant damage template must be recognized");
            Assert(Math.Abs(radius - 5f) < 0.001f, "the circle collider radius must override the cast preview radius 2");
            Assert(Math.Abs((1.25f + delay) - 1.3f) < 0.001f,
                "impact must follow the 1.25-second channel and 0.05-second damage delay");
        }

        private static void IgnoresOtherSkills()
        {
            var instance = new InstantDamageInstance
            {
                damageDelay = 1f,
                Colliders = new[] { new DewCollider { shape = DewCollider.ColliderShape.Circle, radius = 5f } }
            };
            Assert(!TreantPowerBombPreview.TryGet(instance, out _, out _),
                "unrelated damage skills must retain their existing preview geometry and timing");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
