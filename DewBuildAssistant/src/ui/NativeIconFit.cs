using System;

namespace DewBuildAssistant.ui;

// 缩放可见图形的整体边界，包含偏心布局；容器布局尚未完成时等待有效尺寸。
internal static class NativeIconFit
{
    public static bool Calculate(float width, float height, float minX, float minY, float maxX, float maxY,
        out float scale, out float x, out float y)
    {
        scale = 0;
        x = y = 0;
        float contentWidth = maxX - minX, contentHeight = maxY - minY;
        if (width <= 4 || height <= 4 || contentWidth <= 0 || contentHeight <= 0) return false;
        scale = Math.Min((width - 4) / contentWidth, (height - 4) / contentHeight);
        if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0) return false;
        x = -(minX + maxX) * 0.5f * scale;
        y = -(minY + maxY) * 0.5f * scale;
        return true;
    }
}
