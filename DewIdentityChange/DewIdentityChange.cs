using System.Collections.Generic;
using DewIdentityChange.config;
using UnityEngine;

namespace DewIdentityChange;

public class DewIdentityChange : ModBehaviour
{
    public static DewIdentityChange Instance;

    public readonly PluginConfig config = new PluginConfig();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        instance.isAlteringGameplay = true;
        LocalizationSource.Init(this);
        harmony.PatchAll();
        Debug.Log($"[{mod.metadata.id}] 已加载: {mod.metadata.name} by {mod.metadata.author}");
        // 缓存所有HeroSkill
        foreach (var heroSkill in Resources.FindObjectsOfTypeAll<HeroSkill>())
        {
            LoadoutSnapshot.Capture(heroSkill);
            LoadoutSnapshot.Switch(config.enable);
        }
    }

    private void OnDestroy()
    {
        harmony.UnpatchAll(harmony.Id);
    }
}