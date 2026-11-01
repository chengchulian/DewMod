using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

// 保留真实 Harmony 注册过程；这里只替代结算界面所需的 Unity 对象。
namespace UnityEngine
{
    public class Component
    {
        public Transform transform = new();
    }

    public class Transform
    {
        public Transform parent;
        public bool IsChildOf(Transform ancestor)
        {
            for (var current = this; current != null; current = current.parent)
                if (current == ancestor) return true;
            return false;
        }
    }

    public class GameObject
    {
        public T[] GetComponentsInChildren<T>(bool includeInactive) => new T[0];
    }
}

public class DewGameResult
{
    public class PlayerData
    {
        public List<int> maxGemCounts = new() { 8, 8, 8, 8, 0, 0 };
        public Dictionary<string, string> capturedStarTooltipFields = new();
    }
    public List<PlayerData> players = new() { new PlayerData() };
}

public class GameResultManager
{
    public DewGameResult tracked = new();
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void UpdateGameResult() { }
}

public class UI_InGame_Result_HeroGemItem : UnityEngine.Component
{
    public int index;
    public double UpdateAndGetScore(DewGameResult data, int playerIndex, float scoreMultiplier) => 0;
}

public class UI_InGame_Result_HeroSkillItem : UnityEngine.Component
{
    public HeroSkillLocation type;
    public UnityEngine.GameObject[] gemObjects234 = { new(), new(), new() };
    public UnityEngine.Component skillIcon = new();
    public int originalCalls;
    public int renderedCount;
    public bool throwOnUpdate;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public double UpdateAndGetScore(DewGameResult data, int playerIndex, float scoreMultiplier)
    {
        originalCalls++;
        renderedCount = data.players[playerIndex].maxGemCounts[(int)type];
        if (throwOnUpdate) throw new InvalidOperationException("结算异常回归夹具");
        return 0;
    }
}

public class UI_InGame_ResultView : UnityEngine.Component
{
    public UI_InGame_Result_HeroSkillItem[] skills = { new() };
    public int groupsAtRefresh;
    public T[] GetComponentsInChildren<T>(bool includeInactive) => skills.Cast<T>().ToArray();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Refresh()
    {
        groupsAtRefresh = skills[0].gemObjects234.Length;
    }
}

namespace DewGemSlotCount.ui
{
    public static class GemSlotLayout
    {
        public static int extendCalls;

        public static void Extend<T>(ref UnityEngine.GameObject[] groups, int firstCount, UnityEngine.Transform icon,
            Func<T, int> getIndex, Action<T, int> setIndex) where T : UnityEngine.Component
        {
            extendCalls++;
            int previous = groups.Length;
            Array.Resize(ref groups, Constant.MaxGemCount - firstCount + 1);
            for (int i = previous; i < groups.Length; i++) groups[i] = new UnityEngine.GameObject();
        }

        public static UnityEngine.GameObject ShowSingle<T>(UnityEngine.GameObject[] groups,
            UnityEngine.Transform icon, bool show, Func<T, int> getIndex, Action<T, int> setIndex)
            where T : UnityEngine.Component => null;
    }
}
