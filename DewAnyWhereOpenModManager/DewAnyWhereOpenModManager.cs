using DewAnyWhereOpenModManager.config;
using UnityEngine;

namespace DewAnyWhereOpenModManager;

public class DewAnyWhereOpenModManager : ModBehaviour
{
    public static DewAnyWhereOpenModManager Instance;
    
    
    public PluginConfig config = new PluginConfig();

    private void Start()
    {
        Instance = this;
        LocalizationSource.Init();
    }
    private void Update()
    {
        if (!Input.GetKeyDown(config.OpenKey)) return;
        
        var active = UI_ModManager.instance.gameObject.activeSelf;
        UI_ModManager.instance.gameObject.SetActive(!active);
    }
}