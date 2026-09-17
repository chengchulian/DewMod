using System.Collections;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace DewMorePlayers.patch;

[HarmonyPatch(typeof(UI_Lobby_HideIfSingleplayer))]
public class UI_Lobby_HideIfSingleplayer_Patch
{
    [HarmonyPostfix]
    [HarmonyPatch("Start")]
    public static void Start_Postfix(UI_Lobby_HideIfSingleplayer __instance)
    {
        if (DewNetworkManager.startSettings.networkMode != DewNetworkMode.Singleplayer)
        {
            __instance.StartCoroutine(WaitLobbyReady(__instance));
        }

    }
    /// <summary>
    /// 使用 WaitUntil 等待 LobbyManager 完成加载
    /// </summary>
    private static IEnumerator WaitLobbyReady(UI_Lobby_HideIfSingleplayer ui)
    {
        DewNetworkMode networkMode = DewNetworkManager.startSettings.networkMode;
        bool isHost = networkMode == DewNetworkMode.MultiplayerHost
                      || networkMode == DewNetworkMode.MultiplayerHostRestart;

        if (isHost)
        {
            // Keep the dialog value authoritative while Steam publishes the lobby limit.
            yield return new WaitUntil(() =>
            {
                LobbyInstance lobby = ManagerBase<LobbyManager>.instance?.service?.currentLobby;
                return lobby != null && lobby.maxPlayers == DewNetworkManager.startSettings.maxPlayers;
            });
        }
        else
        {
            // Joining clients start at 4 locally; use the host-published lobby limit.
            yield return new WaitUntil(() =>
            {
                LobbyInstance lobby = ManagerBase<LobbyManager>.instance?.service?.currentLobby;
                return lobby != null
                       && lobby.maxPlayers > 4
                       && lobby.maxPlayers <= Constant.MaxPlayerClamp;
            });
        }

        int maxPlayers = ManagerBase<LobbyManager>.instance.service.currentLobby.maxPlayers;
        if (!isHost)
        {
            DewNetworkManager.startSettings.maxPlayers = maxPlayers;
        }

        // 获取“Player List”容器
        Transform listRoot = ui.transform
            .Cast<Transform>()
            .FirstOrDefault(t => t.name == "Player List");

        if (listRoot == null)
            yield break;

        // 获取已有的 UI 项
        var items = listRoot.GetComponentsInChildren<UI_Lobby_PlayerListItem>(true);
        int currentCount = items.Length;

        // 差量添加
        int needAdd = maxPlayers - currentCount;
        
        Debug.Log($"[DewMorePlayers] Player list slots: {currentCount} -> {maxPlayers}.");
        if (needAdd <= 0) yield break;

        // 使用第一个作为模板
        var template = items[0].gameObject;

        for (int i = 0; i < needAdd; i++)
        {
            GameObject clone = Object.Instantiate(template, listRoot);
            clone.name = $"UI_Lobby_PlayerListItem ({currentCount + i})";

            var item = clone.GetComponent<UI_Lobby_PlayerListItem>();
            item.index = currentCount + i;
        }

        yield return null;
    }
}
