using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DewLucidDreamExpansion.ui;

internal static class LimboDepthSelector
{
    internal const int MaxDepth = 9;
    private static readonly List<UI_Lobby_Limbo_DepthSelect_Item> OwnedItems =
        new List<UI_Lobby_Limbo_DepthSelect_Item>();

    // 原生列表只生成一次；按实际条目补齐，兼容已经打开过的大厅窗口。
    internal static void EnsureItems(UI_Lobby_Limbo_DepthSelect selector)
    {
        if (selector.prefab == null || selector.parent == null) return;
        var present = new HashSet<int>(selector.parent
            .GetComponentsInChildren<UI_Lobby_Limbo_DepthSelect_Item>(true).Select(item => item.depth));
        for (int depth = 1; depth <= MaxDepth; depth++)
        {
            if (present.Contains(depth)) continue;
            var item = Object.Instantiate(selector.prefab, selector.parent);
            item.Setup(depth);
            OwnedItems.Add(item);
        }
    }

    internal static void RefreshExisting()
    {
        foreach (var selector in Resources.FindObjectsOfTypeAll<UI_Lobby_Limbo_DepthSelect>())
        {
            if (!selector.gameObject.scene.IsValid() || selector.parent == null) continue;
            if (selector.parent.GetComponentsInChildren<UI_Lobby_Limbo_DepthSelect_Item>(true).Length > 0)
                EnsureItems(selector);
        }
    }

    internal static void Clear()
    {
        foreach (var item in OwnedItems)
            if (item != null) Object.Destroy(item.gameObject);
        OwnedItems.Clear();
    }
}
