using UnityEngine;

namespace DewGemSlotCount.ui;

// 只附着在技能 HUD，等待本帧官方插值和 DOTween 完成后更新镜像与位移位置。
public sealed class SkillHudLayout : MonoBehaviour
{
    private UI_InGame_SkillButtons _ui;
    private UI_InGame_SkillButton_GemGroup[] _groups;

    public static void Ensure(UI_InGame_SkillButtons ui)
    {
        var existing = ui.GetComponent<SkillHudLayout>();
        if (existing != null && existing.enabled) return;
        if (existing != null) Destroy(existing);
        var layout = ui.gameObject.AddComponent<SkillHudLayout>();
        layout._ui = ui;
        layout._groups = ui.GetComponentsInChildren<UI_InGame_SkillButton_GemGroup>(true);
        foreach (var group in layout._groups) GemSlotLayout.EnsureHud(group);
    }

    private void LateUpdate()
    {
        if (_ui == null || DewGemSlotCount.Instance == null) return;
        foreach (var group in _groups)
            if (group != null) GemSlotLayout.UpdateLayout(group.groups, 1);
        var edit = ManagerBase<EditSkillManager>.softInstance;
        if (edit != null) MovementEditLayout.Apply(_ui, edit.mode);
    }

    public static void Cleanup()
    {
        foreach (var layout in Resources.FindObjectsOfTypeAll<SkillHudLayout>())
        {
            layout.enabled = false;
            Destroy(layout);
        }
    }
}
