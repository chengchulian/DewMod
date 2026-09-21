using GoldenBurstAutoTarget.config;
using UnityEngine;

namespace GoldenBurstAutoTarget;

internal sealed class GoldenBurstCastInput
{
    private const float HoldCastIntervalSeconds = 0.02f;

    private readonly PluginConfig _config;
    private float _lastHoldCastTime = float.NegativeInfinity;

    public GoldenBurstCastInput(PluginConfig config)
    {
        _config = config;
    }

    public bool ShouldCast(float time)
    {
        KeyCode key = _config.CastKey;
        if (key == KeyCode.None)
        {
            return false;
        }

        if (!_config.HoldToCast)
        {
            return Input.GetKeyDown(key);
        }

        if (!Input.GetKey(key) || time - _lastHoldCastTime < HoldCastIntervalSeconds)
        {
            return false;
        }

        _lastHoldCastTime = time;
        return true;
    }
}
