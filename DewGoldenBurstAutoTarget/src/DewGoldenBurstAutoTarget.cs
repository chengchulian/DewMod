using System.IO;
using System.Reflection;
using DewGoldenBurstAutoTarget.config;
using UnityEngine;

namespace DewGoldenBurstAutoTarget;

public class DewGoldenBurstAutoTarget : ModBehaviour
{
    public static DewGoldenBurstAutoTarget Instance { get; private set; }
    public PluginConfig Config = new PluginConfig();

    private GoldenBurstAutoTargetController _controller;

    // 项目和入口类改名后继续使用原配置文件，保留已有按键与目标筛选设置。
    public override string GetModConfigFilePath(FieldInfo modConfigInfo)
    {
        string directory = Path.GetDirectoryName(base.GetModConfigFilePath(modConfigInfo));
        return Path.Combine(directory, "GoldenBurstAutoTarget." + modConfigInfo.Name + ".json");
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        LocalizationSource.Init(this);
        _controller = new GoldenBurstAutoTargetController(
            new GoldenBurstCastInput(Config),
            new GoldenBurstSkillProvider(),
            new GoldenBurstTargetSelector(new GoldenBurstTargetClassifier(Config)),
            new GoldenBurstCaster());

        Debug.Log($"[{mod.metadata.id}] Golden Burst Auto Target loaded.");
    }

    private void Update()
    {
        _controller?.Update(Time.time);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        Debug.Log($"[{mod.metadata.id}] Golden Burst Auto Target destroyed.");
    }
}
