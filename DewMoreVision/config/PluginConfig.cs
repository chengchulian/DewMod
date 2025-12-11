using UnityEngine;

namespace DewMoreVision.config;

public class PluginConfig : ModConfig
{
    // 缩放步长
    [LabelText("LabelText.ZoomSteps")]
    [Description("Description.ZoomSteps")]
    public int ZoomSteps = 6;

    // 视野倍数
    [LabelText("LabelText.ZoomMultiple")]
    [Description("Description.ZoomMultiple")]
    public float ZoomMultiple = 1f;


    public override void BuildWidgets(Transform parent, out SafeAction onChanged, out SafeAction requestUpdate)
    {
        base.BuildWidgets(parent, out onChanged, out requestUpdate);
        // 本地化UI
        LocalizationSource.LocalizeUI(parent);
    }

    public override void CopyTo(ModConfig other)
    {
        if (CameraManager.instance != null)
        {
            CameraManager.instance.zoomSteps = ZoomSteps;
            CameraManager.instance.SetZoomLevel(0);
        }

        base.CopyTo(other);
    }
}