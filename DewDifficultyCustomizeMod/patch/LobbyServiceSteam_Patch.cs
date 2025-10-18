using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Steamworks;
using UnityEngine;

namespace DewDifficultyCustomizeMod.patch;

[HarmonyPatch(typeof(LobbyServiceSteam))]
public class LobbyServiceSteam_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch("ApplyLobbyData")]
    public static bool ApplyLobbyData_Prefix(LobbyServiceSteam __instance,CSteamID lobbyId, ref LobbyInstanceSteam data, ref bool __result)
    {
        try
        {
            // 使用 AccessTools.Field 来访问所有 internal 字段
            var idField = AccessTools.Field(typeof(LobbyInstanceSteam), "_id");
            var nameField = AccessTools.Field(typeof(LobbyInstanceSteam), "_name");
            var difficultyField = AccessTools.Field(typeof(LobbyInstanceSteam), "_difficulty");
            var hasGameStartedField = AccessTools.Field(typeof(LobbyInstanceSteam), "_hasGameStarted");
            var gameStartTimestampField = AccessTools.Field(typeof(LobbyInstanceSteam), "_gameStartTimestamp");
            var allowJoinField = AccessTools.Field(typeof(LobbyInstanceSteam), "_allowJoin");
            var currentPlayersField = AccessTools.Field(typeof(LobbyInstanceSteam), "_currentPlayers");
            var maxPlayersField = AccessTools.Field(typeof(LobbyInstanceSteam), "_maxPlayers");
            var versionField = AccessTools.Field(typeof(LobbyInstanceSteam), "_version");
            var isInviteOnlyField = AccessTools.Field(typeof(LobbyInstanceSteam), "_isInviteOnly");
            var shortCodeField = AccessTools.Field(typeof(LobbyInstanceSteam), "_shortCode");

            // 设置 data 对象的各个字段值
            idField.SetValue(data, lobbyId);
            nameField.SetValue(data, SteamMatchmaking.GetLobbyData(lobbyId, "name"));
            difficultyField.SetValue(data, SteamMatchmaking.GetLobbyData(lobbyId, "difficulty"));

            // 安全解析布尔值
            bool parsedBool;
            string boolStr = SteamMatchmaking.GetLobbyData(lobbyId, "hasGameStarted");
            bool.TryParse(boolStr, out parsedBool);
            hasGameStartedField.SetValue(data, parsedBool);

            // 安全解析长整型
            long parsedLong;
            string longStr = SteamMatchmaking.GetLobbyData(lobbyId, "gameStartTimestamp");
            long.TryParse(longStr, out parsedLong);
            gameStartTimestampField.SetValue(data, parsedLong);

            // 安全解析布尔值
            boolStr = SteamMatchmaking.GetLobbyData(lobbyId, "allowJoin");
            bool.TryParse(boolStr, out parsedBool);
            allowJoinField.SetValue(data, parsedBool);

            // 设置玩家数量
            currentPlayersField.SetValue(data, SteamMatchmaking.GetNumLobbyMembers(lobbyId));
            
            // 设置最大玩家数（关键修改：从 4 改为 1024）
            int memberLimit = SteamMatchmaking.GetLobbyMemberLimit(lobbyId);

            maxPlayersField.SetValue(data, Mathf.Clamp(memberLimit, 0, 1024));

            // 设置版本信息
            versionField.SetValue(data, SteamMatchmaking.GetLobbyData(lobbyId, "version"));

            // 安全解析布尔值
            boolStr = SteamMatchmaking.GetLobbyData(lobbyId, "isInviteOnly");
            bool.TryParse(boolStr, out parsedBool);
            isInviteOnlyField.SetValue(data, parsedBool);

            // 设置短代码
            shortCodeField.SetValue(data, SteamMatchmaking.GetLobbyData(lobbyId, "shortCode"));

            // 保持原逻辑 - 检查当前大厅并调用相关方法
            var lobbyServiceType = typeof(LobbyServiceSteam);
            var currentLobbyField = AccessTools.Field(lobbyServiceType, "_currentLobby");
            
            if (currentLobbyField != null)
            {
                var currentLobby = currentLobbyField.GetValue(__instance);
                if (currentLobby != null)
                {
                    // 获取 currentLobby 的 _id 字段值
                    var currentLobbyId = idField.GetValue(currentLobby);
                    if (currentLobbyId is CSteamID currentLobbySteamId && currentLobbySteamId.Equals(lobbyId))
                    {
                        // 调用 InvokeOnCurrentLobbyChanged 方法
                        var invokeMethod = AccessTools.Method(lobbyServiceType, "InvokeOnCurrentLobbyChanged");
                        if (invokeMethod != null)
                        {
                            invokeMethod.Invoke(__instance, null);
                        }
                    }
                }
            }

            __result = true;
            return false; // 跳过原方法
        }
        catch
        {
            __result = false;
            return false;
        }
    }
}
