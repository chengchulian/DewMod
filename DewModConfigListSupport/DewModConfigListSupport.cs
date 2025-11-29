using System;
using System.Reflection;
using DewModConfigListSupport.config;
using UnityEngine;

namespace DewModConfigListSupport;

public class DewModConfigListSupport : ModBehaviour
{
    public static DewModConfigListSupport Instance;
    public readonly PluginConfig Config = new PluginConfig();

    public void Awake()
    {
        Instance = this;
    }
    
    public void Start()
    {
        Debug.Log($"[{mod.metadata.id}] 已加载: {mod.metadata.name} by {mod.metadata.author}");
        ListSupportHelper.InitListSupport();


    }

    public void OnDestroy()
    {
    }
}