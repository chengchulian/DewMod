using GoldenBurstAutoTarget.config;
using UnityEngine;

namespace GoldenBurstAutoTarget;

public class GoldenBurstAutoTarget : ModBehaviour
{
    public static GoldenBurstAutoTarget Instance { get; private set; }
    public PluginConfig Config = new PluginConfig();

    private GoldenBurstAutoTargetController _controller;

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
