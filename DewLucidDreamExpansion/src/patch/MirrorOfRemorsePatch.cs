using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;

namespace DewLucidDreamExpansion.patch;

[HarmonyPatch]
internal static class MirrorOfRemorsePatch
{
    [ThreadStatic] private static int transformationDepth;
    internal static bool IsTransforming => transformationDepth > 0;

    // 转换发生在 CmdConfirm 的局部协程内；按迭代器定位，避开编译器生成的类名和编号。
    private static MethodBase TargetMethod()
    {
        return GetNestedTypes(typeof(Shrine_MirrorOfRemorse))
            .Where(t => typeof(IEnumerator).IsAssignableFrom(t))
            .Select(t => AccessTools.DeclaredMethod(t, nameof(IEnumerator.MoveNext)))
            .Single(m => m != null);
    }

    private static IEnumerable<Type> GetNestedTypes(Type parent)
    {
        foreach (Type type in parent.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            yield return type;
            foreach (Type nested in GetNestedTypes(type)) yield return nested;
        }
    }

    // 仅包围当前 MoveNext 的同步执行，等待动画期间的其他掉落仍遵循诅咒。
    private static void Prefix(out int __state)
    {
        __state = transformationDepth;
        if (NetworkServer.active) transformationDepth++;
    }

    // 异常路径同样恢复作用域，避免后续正常掉落受到影响。
    private static void Finalizer(int __state) => transformationDepth = __state;
}
