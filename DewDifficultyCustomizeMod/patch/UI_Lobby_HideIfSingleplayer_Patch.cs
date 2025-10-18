using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DewDifficultyCustomizeMod.patch;

[HarmonyPatch(typeof(UI_Lobby_HideIfSingleplayer))]
public class UI_Lobby_HideIfSingleplayer_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch("Start")]
    public static bool Start_Prefix(MonoBehaviour __instance)
    {
        if (DewNetworkManager.networkMode == DewNetworkManager.Mode.Singleplayer)
        {
            __instance.gameObject.SetActive(false);
        }
        else
        {
            __instance.StartCoroutine(WaitLobbyLoadEndCoroutine(__instance));
        }

        // 跳过原始 Start 方法
        return false;
    }

    private static IEnumerator WaitLobbyLoadEndCoroutine(MonoBehaviour __instance)
    {
        int count = -1;
        while (count < 0)
        {
            yield return new WaitForSeconds(0.1f);
            try
            {
                count = ManagerBase<LobbyManager>.instance?.service?.currentLobby?.maxPlayers ?? -1;
            }
            catch (Exception)
            {
                // ignored
            }
        }


        Transform playList = __instance.transform
            .Cast<Transform>()
            .FirstOrDefault(t => t.name == "Player List");
        if (playList == null)
        {
            yield break;
        }
        
        GameObject playListGameObject = playList.gameObject;

        UI_Lobby_PlayerListItem[] uiLobbyPlayerListItems = playListGameObject.GetComponentsInChildren<UI_Lobby_PlayerListItem>();
        int addCount = count - uiLobbyPlayerListItems.Length;
        
        for (int i = 0; i < addCount; i++)
        {
            GameObject addGameObject = Object.Instantiate(
                uiLobbyPlayerListItems[0].gameObject,
                playListGameObject.transform
            );

            addGameObject.name = $"UI_Lobby_PlayerListItem ({uiLobbyPlayerListItems.Length + i})";
            addGameObject.transform.SetParent(playListGameObject.transform, false);
            addGameObject.GetComponent<UI_Lobby_PlayerListItem>().index = playListGameObject.transform.childCount - 1;
        }

        yield return null;
    }
}