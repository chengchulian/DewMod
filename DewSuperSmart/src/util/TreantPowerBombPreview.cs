using UnityEngine;

namespace DewSuperSmart;

internal static class TreantPowerBombPreview
{
    // 树灵施法配置的预览半径小于实际伤害碰撞体，必须从生成模板读取。
    public static bool TryGet(AbilityInstance instance, out float radius, out float damageDelay)
    {
        radius = 0f;
        damageDelay = 0f;
        if (instance == null || instance.GetType().Name != "Ai_Mon_Forest_Treant_PowerBomb" ||
            instance is not InstantDamageInstance damage)
        {
            return false;
        }

        DewCollider[] colliders = instance.GetComponentsInChildren<DewCollider>(includeInactive: true);
        for (int i = 0; i < colliders.Length; i++)
        {
            DewCollider collider = colliders[i];
            if (collider == null || collider.shape != DewCollider.ColliderShape.Circle)
            {
                continue;
            }

            Vector3 scale = collider.transform.lossyScale;
            float actualRadius = collider.radius * Mathf.Max(
                Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)), 0.01f);
            if (!float.IsNaN(actualRadius) && !float.IsInfinity(actualRadius))
            {
                radius = Mathf.Max(radius, actualRadius);
            }
        }

        damageDelay = Mathf.Max(damage.damageDelay, 0f);
        return radius > 0f;
    }
}
