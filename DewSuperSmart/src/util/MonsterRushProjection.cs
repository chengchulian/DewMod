using UnityEngine;

namespace DewSuperSmart;

internal static class MonsterRushProjection
{
    internal static bool UsesIndependentCollider(AbilityInstance instance)
    {
        return instance.GetType().Name == "Ai_Mon_Forest_Hound_Charge";
    }

    // 叶犬只移动 range，不移动技能根节点；以 range 为基准消除重复位移。
    internal static Transform GetRoot(AbilityInstance instance, DewCollider collider)
    {
        return UsesIndependentCollider(instance) ? collider.transform : instance.transform;
    }

    // 保留碰撞体自身偏移、旋转和缩放，将其放到当前施法者的平面位置。
    internal static Vector3 Project(Transform root, Vector3 point, Vector3 position, Quaternion rotation)
    {
        return position + rotation * Vector3.Scale(root.InverseTransformPoint(point), root.lossyScale);
    }
}
