using System;

// 动画桩立即完成移动；验证生产位移编辑代码的定位、显隐、重复调用与还原。
namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z = 0) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 up => new(0, 1);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator *(Vector3 a, float scale) => new(a.x * scale, a.y * scale, a.z * scale);
    }
    public class RectTransform : Transform { public Vector3 anchoredPosition3D; }
    public class CanvasGroup : Component
    {
        public float alpha;
        public bool interactable, blocksRaycasts;
    }
}

namespace DG.Tweening
{
    public class Tween
    {
        public void Kill() { }
        public Tween SetUpdate(bool isIndependentUpdate) => this;
    }
    public static class ShortcutExtensions
    {
        public static int moves;
        public static void DOKill(this UnityEngine.CanvasGroup target) { }
        public static Tween DOAnchorPos3D(this UnityEngine.RectTransform target, UnityEngine.Vector3 position, float duration)
        {
            moves++;
            target.anchoredPosition3D = position;
            return new Tween();
        }
    }
}

public class UI_InGame_SkillButton : UnityEngine.Component { public HeroSkillLocation skillType; }
public class UI_InGame_SkillButtons
{
    public UI_InGame_SkillButton[] skillButtons;
    public UnityEngine.CanvasGroup[] hiddenWhenExpanded;
    public float hiddenWhenExpandedDuration = 0.1f;
}
public class Shrine_CorruptedChaos { }
public class EditSkillManager
{
    public enum ModeType { None, Regular, EditSkillShrine }
    public ModeType mode;
    public object currentProvider;
}
public static class ManagerBase<T> { public static T softInstance; }

namespace DewGemSlotCount
{
    public class DewGemSlotCount
    {
        public static DewGemSlotCount Instance;
        public class Settings
        {
            public int SkillQGemCount = 3, SkillWGemCount = 3, SkillEGemCount = 3, SkillRGemCount = 3;
            public int SkillIdentityGemCount, SkillMovementGemCount;
            public int SkillQCorruptedChaosMaxGemCount = 8, SkillWCorruptedChaosMaxGemCount = 8;
            public int SkillECorruptedChaosMaxGemCount = 8, SkillRCorruptedChaosMaxGemCount = 8;
            public int SkillIdentityCorruptedChaosMaxGemCount = 8, SkillMovementCorruptedChaosMaxGemCount = 8;
            public bool EditIdentitySkill, GemNoMerge;
            public bool OptimizeUI = true;
            public bool EditMovementSkill;
            public bool AllowMovementCorruptedChaos;
        }
        public readonly Settings Config = new();
        public Settings GameplayConfig => Config;
    }
}
