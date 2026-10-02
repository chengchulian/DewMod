using System.Linq;
using DewLimboConfinedAbolish.config;
using HarmonyLib;
using Mirror;

namespace DewLimboConfinedAbolish.patch;

[HarmonyPatch(typeof(GameMod_Limbo), nameof(GameMod_Limbo.OnStartServerLobby))]
internal static class GameMod_Limbo_OnStartServerLobby_Patch
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        PluginConfig config = DewLimboConfinedAbolish.Instance?.config;
        if (config == null)
        {
            return true;
        }

        // 保留迷失域深度进度门槛，但不添加恶清醒梦数量门槛。
        if (DewNetworkManager.startSettings.continueData == null && GameMod_Limbo.Lobby_GetDepth() == -1)
        {
            GameMod_Limbo.Lobby_SetDepth(1);
        }

        ManagerBase<PlayLobbyManager>.instance.AddStartGameCondition(() =>
            GameMod_Limbo.Lobby_GetLimboDepthState(GameMod_Limbo.Lobby_GetDepth()) == LimboDepthState.Locked
                ? GameMod_Limbo.Lobby_GetLimboDepthUnavailableReason(GameMod_Limbo.Lobby_GetDepth())
                : null);

        if (!config.AllowAllLucidDreams)
        {
            ManagerBase<PlayLobbyManager>.instance.AddStartGameCondition(delegate
            {
                int evilDreamCount = NetworkedManagerBase<GameSettingsManager>.instance.activeLucidDreams.Count(delegate(string name)
                {
                    LucidDream dream = DewResources.GetByShortTypeName<LucidDream>(name, ResourceLoadSettings.Light);
                    return dream != null && dream.type == LucidDreamType.Evil;
                });
                return evilDreamCount != GameMod_Limbo.Lobby_GetDepth()
                    ? string.Format("{0} ({1}/{2})", DewLocalization.GetUIValue("Limbo_NeedToEnableEvilLucidDreams"), evilDreamCount, GameMod_Limbo.Lobby_GetDepth())
                    : null;
            });
        }

        NetworkedManagerBase<GameSettingsManager>.instance.difficulty = "diffLimbo";
        NetworkedManagerBase<GameSettingsManager>.instance.allowDejavu = config.AllowDejavu;
        if (!config.AllowObliteration)
        {
            NetworkedManagerBase<GameSettingsManager>.instance.bannedGameItems.Clear();
        }

        GameSettingsManager settings = NetworkedManagerBase<GameSettingsManager>.instance;
        if (config.AllowMidGameJoin)
        {
            settings.allowMidJoins = AllowMidJoinType.AllowAll;
        }
        else if (settings.allowMidJoins == AllowMidJoinType.AllowAll)
        {
            settings.allowMidJoins = AllowMidJoinType.RejoinOnly;
        }

        if (config.AllowAllLucidDreams)
        {
            NetworkedManagerBase<GameSettingsManager>.instance.UpdateAvailableLucidDreams();
        }

        return false;
    }
}

[HarmonyPatch(typeof(GameMod_Limbo), "EnforceGameRules")]
internal static class GameMod_Limbo_EnforceGameRules_Patch
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        PluginConfig config = DewLimboConfinedAbolish.Instance?.config;
        if (config == null)
        {
            return true;
        }

        if (!NetworkServer.active)
        {
            return false;
        }

        GameSettingsManager settings = NetworkedManagerBase<GameSettingsManager>.instance;
        if (!config.AllowDejavu && settings.allowDejavu)
        {
            settings.allowDejavu = false;
            ManagerBase<MessageManager>.instance.ShowMessageLocalized("Limbo_SettingsError_CantUseDejavu");
        }

        if (!config.AllowObliteration && settings.bannedGameItems.Count > 0)
        {
            settings.bannedGameItems.Clear();
        }

        if (config.AllowMidGameJoin && settings.allowMidJoins != AllowMidJoinType.AllowAll)
        {
            settings.allowMidJoins = AllowMidJoinType.AllowAll;
        }
        else if (!config.AllowMidGameJoin && settings.allowMidJoins == AllowMidJoinType.AllowAll)
        {
            settings.allowMidJoins = AllowMidJoinType.RejoinOnly;
            ManagerBase<MessageManager>.instance.ShowMessageLocalized("Limbo_SettingsError_NewPlayerCantMidJoin");
        }

        if (!config.AllowAllLucidDreams)
        {
            var dreams = settings.activeLucidDreams;
            for (int i = dreams.Count - 1; i >= 0; i--)
            {
                LucidDream dream = DewResources.GetByShortTypeName<LucidDream>(dreams[i], ResourceLoadSettings.Light);
                if (dream != null && dream.type != LucidDreamType.Evil)
                {
                    dreams.RemoveAt(i);
                }
            }
        }

        return false;
    }
}

[HarmonyPatch(typeof(GameSettingsManager), nameof(GameSettingsManager.UpdateAvailableLucidDreams))]
internal static class GameSettingsManager_UpdateAvailableLucidDreams_Patch
{
    [HarmonyPostfix]
    private static void Postfix(GameSettingsManager __instance)
    {
        PluginConfig config = DewLimboConfinedAbolish.Instance?.config;
        if (config == null || !config.AllowAllLucidDreams || !NetworkServer.active || __instance.difficulty != "diffLimbo")
        {
            return;
        }

        // Limbo 中开放当前游戏内容包含的所有清醒梦，不受个人解锁状态限制。
        foreach (var dreamType in Dew.allLucidDreams)
        {
            if (Dew.IsLucidDreamIncludedInGame(dreamType.Name) && !__instance.availableLucidDreams.Contains(dreamType.Name))
            {
                __instance.availableLucidDreams.Add(dreamType.Name);
            }
        }
    }
}

[HarmonyPatch(typeof(UI_Lobby_ObliterationView), nameof(UI_Lobby_ObliterationView.ShowIfServer))]
internal static class UI_Lobby_ObliterationView_ShowIfServer_Patch
{
    [HarmonyPrefix]
    private static bool Prefix(UI_Lobby_ObliterationView __instance)
    {
        PluginConfig config = DewLimboConfinedAbolish.Instance?.config;
        if (config == null || !config.AllowObliteration || !NetworkServer.active || NetworkedManagerBase<GameSettingsManager>.instance.difficulty != "diffLimbo")
        {
            return true;
        }

        ManagerBase<UIManager>.instance.SetState(__instance.showOn[0]);
        return false;
    }
}

[HarmonyPatch(typeof(LimboObject), nameof(LimboObject.OnLimboGame))]
internal static class LimboObject_OnLimboGame_Patch
{
    [HarmonyPrefix]
    private static bool Prefix(LimboObject __instance)
    {
        PluginConfig config = DewLimboConfinedAbolish.Instance?.config;
        string objectName = __instance.gameObject.name;
        bool showObliterationItems = config != null && config.AllowObliteration && objectName == "UI_Lobby_Obliteration_BannedItems";
        bool showOtherLucidDreams = config != null && config.AllowAllLucidDreams &&
                                     (objectName == "Good Parent" || objectName == "Good Title" ||
                                      objectName == "Chaotic Parent" || objectName == "Chaotic Title");
        if (showObliterationItems || showOtherLucidDreams)
        {
            __instance.gameObject.SetActive(true);
            return false;
        }

        return true;
    }
}
