using System;
using DewMorePlayers.config;
using DewMorePlayers.patch;
using UnityEngine;

namespace DewMorePlayers;

public class DewMorePlayers : ModBehaviour
{
    public static int MaxPlayers { get; private set; } = 4;

    private void Awake()
    {
        MaxPlayers = 4;
    }

    public static void SetMaxPlayers(int maxPlayers)
    {
        MaxPlayers = Mathf.Clamp(maxPlayers, Constant.MinPlayerClamp, Constant.MaxPlayerClamp);
    }

    private void Start()
    {
        // required by the modding docs for server-side mods that change gameplay: puts a MOD
        // icon on the hosted lobby and warns joining clients. every other gameplay mod in
        // this repo sets it, this one didn't. cross-play is already off whenever any mod is
        // active, so this only affects the badge and the notice.
        instance.isAlteringGameplay = true;

        LocalizationSource.Init(this);

        // if one patch breaks on a game update the whole mod shouldn't die here
        try
        {
            harmony.PatchAll();
        }
        catch (Exception e)
        {
            Debug.LogError($"[{mod.metadata.id}] PatchAll failed: {e}");
        }

        LobbyServiceEOS_MaxPlayers_Patch.Apply(harmony);

        Debug.Log($"[{mod.metadata.id}] 已加载: {mod.metadata.name} by {mod.metadata.author}");
    }

    private void OnDestroy()
    {
        DewMorePlayersUiCleanup.CleanupAll();
        harmony.UnpatchAll(harmony.Id);
    }
}
