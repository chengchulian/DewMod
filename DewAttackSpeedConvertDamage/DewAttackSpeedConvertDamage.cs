using DewAttackSpeedConvertDamage.config;
using UnityEngine;

namespace DewAttackSpeedConvertDamage;

public class DewAttackSpeedConvertDamage : ModBehaviour
{
    public static DewAttackSpeedConvertDamage Instance;
    public readonly PluginConfig config = new PluginConfig();
    private void Awake()
    {
        Instance = this;

    }   
    private void Start()
    {
        LocalizationSource.Init(this);
        harmony.PatchAll();
        Debug.Log($"[{mod.metadata.id}] 已加载: {mod.metadata.name} by {mod.metadata.author}");
    }
}