using System;
using UnityEngine;

namespace DewSuperSmart;

internal static class MonsterRushProjectionTests
{
    internal static void Register(Action<string, Action> run)
    {
        run("hound charge collider does not accumulate movement or widen after rotation", HoundColliderFollowsCaster);
        run("ordinary rush retains collider offset from ability root", OrdinaryRushKeepsOffset);
    }

    // 模拟技能根节点留在出生点，而碰撞体由服务器独立更新，含远端陈旧坐标。
    private static void HoundColliderFollowsCaster()
    {
        var instance = new Ai_Mon_Forest_Hound_Charge();
        var collider = new DewCollider();
        instance.transform.position = new Vector3(-30f, 0f, 40f);
        instance.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
        collider.transform.Scale = new Vector3(2f, 1f, 3f);
        Vector3 offset = new Vector3(0.5f, 0f, 1f);
        for (int step = 0; step < 8; step++)
        {
            collider.transform.position = new Vector3(step * 4f, 2f, -step * 3f);
            collider.transform.rotation = Quaternion.Euler(0f, step * 20f, 0f);
            Vector3 casterPosition = new Vector3(50f + step, 0f, 80f);
            Quaternion casterRotation = Quaternion.Euler(0f, 90f, 0f);
            Transform root = MonsterRushProjection.GetRoot(instance, collider);
            Vector3 center = MonsterRushProjection.Project(root, collider.transform.position, casterPosition, casterRotation);
            AssertNear(center, casterPosition);
            Vector3 projected = MonsterRushProjection.Project(root, collider.transform.TransformPoint(offset), casterPosition, casterRotation);
            AssertNear(projected, casterPosition + new Vector3(3f, 0f, -1f));
        }
    }

    private static void OrdinaryRushKeepsOffset()
    {
        var instance = new AbilityInstance();
        var collider = new DewCollider();
        instance.transform.position = new Vector3(10f, 0f, 20f);
        collider.transform.position = new Vector3(12f, 0f, 23f);
        Vector3 result = MonsterRushProjection.Project(MonsterRushProjection.GetRoot(instance, collider),
            collider.transform.position, new Vector3(30f, 0f, 40f), Quaternion.Euler(0f, 90f, 0f));
        AssertNear(result, new Vector3(33f, 0f, 38f));
    }

    private static void AssertNear(Vector3 actual, Vector3 expected)
    {
        if ((actual - expected).magnitude > 0.001f)
            throw new InvalidOperationException($"Expected {expected}, got {actual}");
    }
}
