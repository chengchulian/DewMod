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
        // 等待 lobby 创建、service 初始化、currentLobby 可用
        yield return new WaitUntil(() =>
        {
            var lobby = ManagerBase<LobbyManager>.instance?.service?.currentLobby;
            return lobby != null && lobby.maxPlayers == DewNetworkManager.startSettings.maxPlayers;
        });

        var lobby = ManagerBase<LobbyManager>.instance.service.currentLobby;
        int maxPlayers = lobby.maxPlayers;

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
        
        Debug.Log( $"当前人数：{currentCount}，需要添加：{needAdd}  添加后人数：{maxPlayers}");
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