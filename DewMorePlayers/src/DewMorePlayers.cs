using DewMorePlayers.config;
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
        LocalizationSource.Init(this);
        harmony.PatchAll();
        Debug.Log($"[{mod.metadata.id}] 已加载: {mod.metadata.name} by {mod.metadata.author}");
    }

    private void OnDestroy()
    {
        DewMorePlayersUiCleanup.CleanupAll();
        harmony.UnpatchAll(harmony.Id);
    }
}
