using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DewLobbyListEnhance.ui;
using DewLobbyListEnhance.patch;

namespace DewLobbyListEnhance;

/// <summary>大厅列表增强入口，负责安装补丁并释放动态控件。</summary>
public sealed class DewLobbyListEnhance : ModBehaviour
{
    internal static readonly Dictionary<int, LobbyFilterState> States = new Dictionary<int, LobbyFilterState>();

    private void Start()
    {
        harmony.PatchAll();
        // 支持模组在大厅视图已创建/显示后加载的情况，补挂筛选栏。
        foreach (UI_Title_FindLobbyView view in Resources.FindObjectsOfTypeAll<UI_Title_FindLobbyView>())
        {
            if (view != null && view.gameObject.scene.IsValid() && view.gameObject.scene.isLoaded)
            {
                FindLobbyViewPatch.EnsureStateForModLoad(view);
            }
        }
        Debug.Log($"[{mod.metadata.id}] 大厅筛选增强已加载（1.4）");
    }

    private void OnDestroy()
    {
        foreach (LobbyFilterState state in States.Values.ToArray()) state.Dispose();
        States.Clear();
        harmony.UnpatchAll(harmony.Id);
    }
}
