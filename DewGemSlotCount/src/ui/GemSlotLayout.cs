using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DewGemSlotCount.ui;

public static class GemSlotLayout
{
    private sealed class LayoutState
    {
        public Transform icon;
        public readonly List<RowSlot> slots = new();
        public Transform[] fourSlots;
        public Vector3[] originalFourPositions;
    }

    private sealed class RowSlot
    {
        public Transform target;
        public Transform source;
        public bool mirror;
        public bool single;
    }

    private static readonly ConditionalWeakTable<GameObject, LayoutState> States = new();
    private static readonly System.Reflection.FieldInfo LastHudCount =
        HarmonyLib.AccessTools.Field(typeof(UI_InGame_SkillButton_GemGroup), "_lastAppliedGemCount");

    // 既供 Awake 使用，也供重载后接管已有 HUD；每个实例只初始化一次。
    public static bool EnsureHud(UI_InGame_SkillButton_GemGroup hud)
    {
        if (DewGemSlotCount.Instance == null || hud == null || hud.groups == null || hud.groups.Length < 4) return false;
        if (States.TryGetValue(hud.groups[0], out _) && hud.groups.Length >= Constant.MaxGemCount) return false;
        var button = hud.transform.parent.GetComponentInChildren<UI_InGame_SkillButton>(true);
        if (button == null || button.icon == null) return false;
        foreach (var group in hud.groups) group.SetActive(false);
        Extend<UI_InGame_GemSlot>(ref hud.groups, 1, button.icon.transform,
            slot => slot.slotIndex, (slot, index) => slot.slotIndex = index,
            symmetricFour: button.skillType == HeroSkillLocation.Movement);
        hud.activeGemSlots = Array.Empty<UI_InGame_GemSlot>();
        LastHudCount.SetValue(hud, int.MinValue);
        return true;
    }

    // 重载时 Awake 不会再触发，主动接管场景中已有界面并恢复当前槽位显示。
    public static void RebuildExisting()
    {
        ForEachScene<UI_InGame_SkillButton_GemGroup>(hud =>
        {
            if (EnsureHud(hud) && hud.isActiveAndEnabled) hud.LogicUpdate(0f);
        });
        ForEachScene<UI_InGame_SkillButtons>(SkillHudLayout.Ensure);
        ForEachScene<UI_InGame_Scoreboard_PlayerItem_Skill>(skill =>
        {
            Extend<UI_InGame_Scoreboard_PlayerItem_Skill_Gem>(ref skill.gemObjects234,
                2, skill.skillIcon.transform, slot => slot.index, (slot, index) => slot.index = index);
            if (skill.isActiveAndEnabled)
                HarmonyLib.AccessTools.Method(typeof(UI_InGame_Scoreboard_PlayerItem_Skill), "UpdateInfo").Invoke(skill, null);
        });
        ForEachScene<UI_InGame_Result_HeroSkillItem>(skill =>
        {
            patch.UI_InGame_Result_HeroSkillItem_Patch.EnsureGroups(skill);
            if (skill.currentData != null)
                skill.UpdateAndGetScore(skill.currentData, skill.currentPlayerIndex, 1f);
        });
    }

    // 五至八槽下排向上取整，上排沿技能中心镜像；位移四槽单独修正官方弧线的左右高度。
    public static void Extend<T>(ref GameObject[] groups, int firstCount, Transform icon,
        Func<T, int> getIndex, Action<T, int> setIndex, bool symmetricFour = false) where T : Component
    {
        // 接管旧程序集遗留的扩展组时重新建立绑定，避免静态缓存丢失后镜像不再更新。
        if (!States.TryGetValue(groups[0], out _) && groups.Length > 5 - firstCount)
        {
            bool owned = true;
            for (int i = 5 - firstCount; i < groups.Length; i++)
                owned &= groups[i] != null && groups[i].name == "GemSlotCount_" + (i + firstCount);
            if (owned)
            {
                for (int i = 5 - firstCount; i < groups.Length; i++)
                {
                    groups[i].SetActive(false);
                    Object.Destroy(groups[i]);
                }
                Array.Resize(ref groups, 5 - firstCount);
                Transform single = groups[0].transform.parent.Find("GemSlotCount_1");
                if (single != null)
                {
                    single.name = "GemSlotCount_1_Retired";
                    single.gameObject.SetActive(false);
                    Object.Destroy(single.gameObject);
                }
            }
        }
        LayoutState state = States.GetValue(groups[0], _ => new LayoutState());
        state.icon = icon;
        if (symmetricFour && state.fourSlots == null)
        {
            T[] slots = groups[4 - firstCount].GetComponentsInChildren<T>(true);
            Array.Sort(slots, (a, b) => getIndex(a).CompareTo(getIndex(b)));
            state.fourSlots = Array.ConvertAll(slots, slot => slot.transform);
            state.originalFourPositions = Array.ConvertAll(slots, slot => slot.transform.localPosition);
        }
        int originalLength = groups.Length;
        int length = Constant.MaxGemCount - firstCount + 1;
        if (originalLength < length)
        {
            Array.Resize(ref groups, length);
            for (int i = originalLength; i < length; i++)
                groups[i] = CreateGroup(groups, firstCount, state, i + firstCount, getIndex, setIndex);
        }
        UpdateLayout(groups, firstCount);
    }

    // 从官方组复制容器布局，保持 HUD、TAB 和结算各自的父级与缩放。
    private static GameObject CreateGroup<T>(GameObject[] groups, int firstCount, LayoutState state,
        int count, Func<T, int> getIndex, Action<T, int> setIndex) where T : Component
    {
        var template = (RectTransform)groups[4 - firstCount].transform;
        var group = new GameObject("GemSlotCount_" + count, typeof(RectTransform));
        group.SetActive(false);
        var target = (RectTransform)group.transform;
        target.SetParent(template.parent, false);
        target.anchorMin = template.anchorMin;
        target.anchorMax = template.anchorMax;
        target.pivot = template.pivot;
        target.sizeDelta = template.sizeDelta;
        target.anchoredPosition3D = template.anchoredPosition3D;
        target.localRotation = template.localRotation;
        target.localScale = template.localScale;

        int lowerCount = count <= 4 ? count : (count + 1) / 2;
        AddRow(groups, firstCount, state, target, lowerCount, 0, false, getIndex, setIndex);
        if (count > 4)
            AddRow(groups, firstCount, state, target, count / 2, lowerCount, true, getIndex, setIndex);
        return group;
    }

    // 克隆时父级保持禁用，索引设置完成后才激活，避免 OnEnable 读取旧索引。
    private static void AddRow<T>(GameObject[] groups, int firstCount, LayoutState state, RectTransform target,
        int count, int startIndex, bool mirror, Func<T, int> getIndex, Action<T, int> setIndex) where T : Component
    {
        GameObject source = groups[Mathf.Max(count, firstCount) - firstCount];
        T[] slots = source.GetComponentsInChildren<T>(true);
        Array.Sort(slots, (a, b) => getIndex(a).CompareTo(getIndex(b)));
        for (int i = 0; i < count; i++)
        {
            T slot = Object.Instantiate(slots[i], target, false);
            slot.name = "Gem_" + (startIndex + i);
            setIndex(slot, startIndex + i);
            var binding = new RowSlot
            {
                target = slot.transform, source = slots[i].transform,
                mirror = mirror, single = count == 1 && firstCount == 2
            };
            state.slots.Add(binding);
            ApplySlot(binding, state.icon);
        }
    }

    // 展开时缩放轴心发生偏移，使用当前图标和官方下排的位置重新计算镜像。
    public static void UpdateLayout(GameObject[] groups, int firstCount)
    {
        if (DewGemSlotCount.Instance == null || groups == null || groups.Length == 0 || !States.TryGetValue(groups[0], out LayoutState state)) return;
        bool optimized = DewGemSlotCount.Instance.Config.OptimizeUI;
        if (state.fourSlots != null)
        {
            for (int i = 0; i < state.fourSlots.Length; i++)
                state.fourSlots[i].localPosition = state.originalFourPositions[i];
            if (optimized)
            {
                Transform row = state.fourSlots[0].parent;
                float centerX = row.InverseTransformPoint(GetCenter(state.icon)).x;
                for (int i = 0; i < state.fourSlots.Length / 2; i++)
                {
                    Transform left = state.fourSlots[i];
                    Transform right = state.fourSlots[state.fourSlots.Length - 1 - i];
                    Vector3 a = row.InverseTransformPoint(GetCenter(left));
                    Vector3 b = row.InverseTransformPoint(GetCenter(right));
                    GemSlotGeometry.MakeSymmetric(ref a, ref b, centerX);
                    SetCenter(left, row.TransformPoint(a));
                    SetCenter(right, row.TransformPoint(b));
                }
            }
        }
        if (optimized)
        {
            foreach (RowSlot slot in state.slots) ApplySlot(slot, state.icon);
            return;
        }
        var template = (RectTransform)groups[4 - firstCount].transform.GetChild(0);
        float spacing = template.rect.width * Mathf.Abs(template.localScale.x);
        for (int i = 5 - firstCount; i < groups.Length; i++)
        {
            Transform group = groups[i].transform;
            Vector3 center = group.InverseTransformPoint(GetCenter(state.icon));
            Vector3 baseline = group.InverseTransformPoint(GetCenter(template));
            for (int j = 0; j < group.childCount; j++)
            {
                Transform slot = group.GetChild(j);
                Vector3 position = baseline;
                position.x = center.x + (j - (group.childCount - 1) * 0.5f) * spacing;
                SetCenter(slot, group.TransformPoint(position));
            }
        }
    }

    // 以可见矩形中心定位，避免父级和图标使用不同轴心时偏斜。
    public static Vector3 GetCenter(Transform target)
    {
        return target is RectTransform rect ? rect.TransformPoint(rect.rect.center) : target.position;
    }

    private static void SetCenter(Transform target, Vector3 center)
    {
        target.position += center - GetCenter(target);
    }

    // 每次均从官方槽读取当前坐标，不依赖上一次结果，重复展开不会累计位移。
    private static void ApplySlot(RowSlot slot, Transform icon)
    {
        Transform row = slot.target.parent;
        Vector3 center = row.InverseTransformPoint(GetCenter(icon));
        Vector3 position = row.InverseTransformPoint(GetCenter(slot.source));
        if (slot.single) position.x = center.x;
        if (slot.mirror) position = GemSlotGeometry.MirrorAbove(position, center);
        SetCenter(slot.target, row.TransformPoint(position));
    }

    // TAB 和结算的原版数组从两槽开始，单槽另建同级容器。
    public static GameObject ShowSingle<T>(GameObject[] groups, Transform icon, bool show,
        Func<T, int> getIndex, Action<T, int> setIndex) where T : Component
    {
        Transform existing = groups[0].transform.parent.Find("GemSlotCount_1");
        GameObject single = existing == null ? null : existing.gameObject;
        if (show && single == null)
            single = CreateGroup(groups, 2, States.GetValue(groups[0], _ => new LayoutState { icon = icon }), 1, getIndex, setIndex);
        if (single != null) single.SetActive(show);
        return single;
    }

    // 卸载时移除扩展组并恢复官方容量，避免留下失去补丁管理的 UI 对象。
    public static void Cleanup()
    {
        SkillHudLayout.Cleanup();
        ForEachScene<UI_InGame_SkillButton_GemGroup>(hud =>
        {
            if (!RestoreGroups(ref hud.groups, 1)) return;
            hud.activeGemSlots = new UI_InGame_GemSlot[0];
            LastHudCount.SetValue(hud, int.MinValue);
        });
        ForEachScene<UI_InGame_Scoreboard_PlayerItem_Skill>(skill => RestoreGroups(ref skill.gemObjects234, 2));
        ForEachScene<UI_InGame_Result_HeroSkillItem>(skill => RestoreGroups(ref skill.gemObjects234, 2));
    }

    // 场景切换时单个已销毁界面出错，也继续初始化或清理其他实例。
    private static void ForEachScene<T>(Action<T> action) where T : Component
    {
        foreach (T component in Resources.FindObjectsOfTypeAll<T>())
        {
            if (component == null || !component.gameObject.scene.IsValid()) continue;
            try { action(component); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    // 只处理本 Mod 登记过的实例，保留原版组与其他对象。
    private static bool RestoreGroups(ref GameObject[] groups, int firstCount)
    {
        if (groups == null || groups.Length == 0 || !States.TryGetValue(groups[0], out LayoutState state)) return false;
        if (state.fourSlots != null)
            for (int i = 0; i < state.fourSlots.Length; i++)
                state.fourSlots[i].localPosition = state.originalFourPositions[i];
        States.Remove(groups[0]);
        for (int i = 5 - firstCount; i < groups.Length; i++)
        {
            groups[i].SetActive(false);
            Object.Destroy(groups[i]);
        }
        Transform single = groups[0].transform.parent.Find("GemSlotCount_1");
        if (single != null)
        {
            single.gameObject.SetActive(false);
            Object.Destroy(single.gameObject);
        }
        Array.Resize(ref groups, 5 - firstCount);
        return true;
    }
}
