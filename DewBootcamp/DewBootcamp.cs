using DewBootcamp.config;
using UnityEngine;

namespace DewBootcamp;

public class DewBootcamp : ModBehaviour
{
    public static DewBootcamp Instance;

    public PluginConfig config = new PluginConfig();

    private void Awake()
    {
        Instance = this;
    }
    private void Start()
    {
        LocalizationSource.Init();
        Debug.Log($"[{mod.metadata.id}] 已加载: {mod.metadata.name} by {mod.metadata.author}");
    }

    private void Update()
    {
        // 没按任意键直接 return
        if (!Input.anyKeyDown) return;

        // 必须按住 Ctrl
        if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
            return;

        // ------------ 按键触发逻辑在此 ------------
        bool spawnToCreep = Input.GetKeyDown(config.SpawnToCreepKey);
        bool spawnToLocal = Input.GetKeyDown(config.SpawnToLocalLey);

        // ----------- 开始判空逻辑（只有按键触发时才检查） -----------  

        var gameManager = NetworkedManagerBase<GameManager>.instance;
        if (gameManager == null || !gameManager.isServer)
            return;

        var zoneManager = NetworkedManagerBase<ZoneManager>.instance;
        if (zoneManager == null)
            return;

        var room = SingletonDewNetworkBehaviour<Room>.instance;
        if (room == null)
            return;

        // ------------- 按键触发逻辑 -----------------

        var spawnPos = DewConsoleCommands.GetCursorWorldPos();
        var actor = NetworkedManagerBase<ActorManager>.instance.serverActor;
        var ambient = gameManager.ambientLevel;

        if (spawnToCreep)
        {
            Dew.SpawnEntity(
                DewResources.GetByType<Mon_RedGiant>(),
                spawnPos,
                Quaternion.identity,
                actor,
                DewPlayer.creep,
                ambient);
        }
        else if (spawnToLocal)
        {
            Dew.SpawnEntity(
                DewResources.GetByType<Mon_RedGiant>(),
                spawnPos,
                Quaternion.identity,
                actor,
                DewPlayer.local,
                ambient);
        }
    }
    
}
