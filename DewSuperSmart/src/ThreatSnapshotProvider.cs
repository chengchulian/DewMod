using System.Collections.Generic;
using DewSuperSmart.config;
using UnityEngine;

namespace DewSuperSmart;

internal sealed class ThreatSnapshotProvider : MonoBehaviour
{
    private const float RefreshInterval = 0.02f;

    private readonly ThreatAnalyzer _analyzer = new ThreatAnalyzer();
    private readonly List<ThreatZone> _threats = new List<ThreatZone>(192);

    private Hero _hero;
    private float _nextRefreshTime = float.NegativeInfinity;

    public IReadOnlyList<ThreatZone> GetThreats(Hero hero, PluginConfig config)
    {
        if (hero == null || hero.IsNullInactiveDeadOrKnockedOut() || config == null)
        {
            Invalidate();
            return _threats;
        }

        float now = Time.unscaledTime;
        if (_hero != hero || now >= _nextRefreshTime)
        {
            _hero = hero;
            _analyzer.CollectThreats(hero, config, _threats);
            _nextRefreshTime = now + RefreshInterval;
        }

        return _threats;
    }

    public void Invalidate()
    {
        _hero = null;
        _threats.Clear();
        _nextRefreshTime = float.NegativeInfinity;
    }
}
