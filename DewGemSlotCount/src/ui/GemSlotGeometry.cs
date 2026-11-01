using UnityEngine;

namespace DewGemSlotCount.ui;

// 布局的纯几何计算，可脱离 Unity 场景进行回归检查。
public static class GemSlotGeometry
{
    public static Vector3 MirrorAbove(Vector3 lower, Vector3 center)
    {
        lower.y = 2f * center.y - lower.y;
        return lower;
    }

    // 原版位移四槽的右侧端点高度与左侧不同，按左右成对平均修正弧线。
    public static void MakeSymmetric(ref Vector3 left, ref Vector3 right, float centerX)
    {
        float halfWidth = Mathf.Abs(right.x - left.x) * 0.5f;
        left.x = centerX - halfWidth;
        right.x = centerX + halfWidth;
        left.y = right.y = (left.y + right.y) * 0.5f;
    }
}
