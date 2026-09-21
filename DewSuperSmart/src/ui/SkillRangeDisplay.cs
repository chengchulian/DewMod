using DewSuperSmart.config;
using UnityEngine;
using UnityEngine.Rendering;

namespace DewSuperSmart;

internal sealed class SkillRangeDisplay : MonoBehaviour
{
    private const int SegmentCount = 48;
    private const float LineWidth = 0.045f;
    private const float GroundOffset = 0.08f;
    private const float MinVisibleRange = 0.25f;
    private const int AttackRangeIndex = 0;
    private const int SkillRangeStartIndex = 1;

    private static readonly HeroSkillLocation[] Locations =
    [
        HeroSkillLocation.Q,
        HeroSkillLocation.W,
        HeroSkillLocation.E,
        HeroSkillLocation.R,
        HeroSkillLocation.Movement,
        HeroSkillLocation.Identity
    ];

    private static readonly Color[] Colors =
    [
        new Color(1f, 1f, 1f, 0.86f),
        new Color(0.25f, 0.72f, 1f, 0.72f),
        new Color(0.32f, 0.95f, 0.58f, 0.72f),
        new Color(1f, 0.86f, 0.28f, 0.72f),
        new Color(1f, 0.32f, 0.32f, 0.72f),
        new Color(0.72f, 0.52f, 1f, 0.72f),
        new Color(1f, 0.48f, 0.92f, 0.72f)
    ];

    private readonly LineRenderer[] _renderers = new LineRenderer[Locations.Length + SkillRangeStartIndex];
    private readonly bool[] _rangeGeometryValid = new bool[Locations.Length + SkillRangeStartIndex];
    private readonly float[] _rangeGeometryRadius = new float[Locations.Length + SkillRangeStartIndex];
    private Material _lineMaterial;

    private void Awake()
    {
        Shader shader = Shader.Find("Sprites/Default") ??
                        Shader.Find("Universal Render Pipeline/Unlit") ??
                        Shader.Find("Hidden/Internal-Colored");

        if (shader == null)
        {
            return;
        }

        _lineMaterial = new Material(shader)
        {
            name = "DewSuperSmart Range Display",
            hideFlags = HideFlags.HideAndDontSave
        };
        _lineMaterial.renderQueue = 5000;
    }

    private void LateUpdate()
    {
        if (!TryGetLocalHero(out Hero hero))
        {
            HideAll();
            return;
        }

        PluginConfig config = DewSuperSmart.Instance.Config;
        Vector3 center = hero.agentPosition;

        if (config.ShowAttackRange && TryGetAttackRange(hero, out float attackRange))
        {
            ShowRange(AttackRangeIndex, center, attackRange);
        }
        else
        {
            SetVisible(AttackRangeIndex, false);
        }

        for (int i = 0; i < Locations.Length; i++)
        {
            int rendererIndex = i + SkillRangeStartIndex;
            HeroSkillLocation location = Locations[i];

            if (!IsSkillRangeEnabled(config, location) ||
                !TryGetSkillRange(hero, location, out float range))
            {
                SetVisible(rendererIndex, false);
                continue;
            }

            ShowRange(rendererIndex, center, range);
        }
    }

    private static bool TryGetLocalHero(out Hero hero)
    {
        hero = DewPlayer.local?.hero;
        return hero != null && !hero.IsNullInactiveDeadOrKnockedOut();
    }

    private static bool IsSkillRangeEnabled(PluginConfig config, HeroSkillLocation location)
    {
        switch (location)
        {
            case HeroSkillLocation.Q:
                return config.ShowQRange;
            case HeroSkillLocation.W:
                return config.ShowWRange;
            case HeroSkillLocation.E:
                return config.ShowERange;
            case HeroSkillLocation.R:
                return config.ShowRRange;
            case HeroSkillLocation.Movement:
                return config.ShowMovementRange;
            case HeroSkillLocation.Identity:
                return config.ShowIdentityRange;
            default:
                return false;
        }
    }

    private static bool TryGetAttackRange(Hero hero, out float range)
    {
        range = 0f;

        AbilityTrigger attack = hero.Ability?.attackAbility;
        TriggerConfig attackConfig = attack?.currentConfig;
        if (attackConfig == null)
        {
            return false;
        }

        range = attackConfig.effectiveRange;
        return IsVisibleRange(range);
    }

    private static bool TryGetSkillRange(Hero hero, HeroSkillLocation location, out float range)
    {
        range = 0f;

        if (hero.Skill == null || !hero.Skill.TryGetSkill(location, out SkillTrigger skill) || skill == null)
        {
            return false;
        }

        TriggerConfig skillConfig = skill.currentConfig;
        if (skillConfig == null)
        {
            return false;
        }

        range = skillConfig.effectiveRange;
        return IsVisibleRange(range);
    }

    private static bool IsVisibleRange(float range)
    {
        return range > MinVisibleRange && !float.IsNaN(range) && !float.IsInfinity(range);
    }

    private void ShowRange(int index, Vector3 center, float range)
    {
        LineRenderer line = GetRenderer(index);
        line.enabled = true;
        line.startColor = Colors[index];
        line.endColor = Colors[index];

        center.y += GroundOffset;
        line.transform.position = center;

        if (!_rangeGeometryValid[index] || !Mathf.Approximately(_rangeGeometryRadius[index], range))
        {
            UpdateCircle(line, range);
            _rangeGeometryValid[index] = true;
            _rangeGeometryRadius[index] = range;
        }
    }

    private LineRenderer GetRenderer(int index)
    {
        if (_renderers[index] != null)
        {
            return _renderers[index];
        }

        string rangeName = index == AttackRangeIndex
            ? "Attack"
            : Locations[index - SkillRangeStartIndex].ToString();
        GameObject rangeObject = new GameObject($"DewSuperSmart_{rangeName}_Range");
        rangeObject.transform.SetParent(transform, false);
        rangeObject.hideFlags = HideFlags.HideAndDontSave;

        LineRenderer line = rangeObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = SegmentCount;
        line.widthMultiplier = LineWidth;
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = 5000;

        if (_lineMaterial != null)
        {
            line.sharedMaterial = _lineMaterial;
        }

        _renderers[index] = line;
        return line;
    }

    private static void UpdateCircle(LineRenderer line, float range)
    {
        for (int i = 0; i < SegmentCount; i++)
        {
            float angle = i * Mathf.PI * 2f / SegmentCount;
            line.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * range,
                0f,
                Mathf.Sin(angle) * range));
        }
    }

    private void HideAll()
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            SetVisible(i, false);
        }
    }

    private void SetVisible(int index, bool visible)
    {
        if (_renderers[index] != null)
        {
            _renderers[index].enabled = visible;
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
            {
                Destroy(_renderers[i].gameObject);
                _renderers[i] = null;
                _rangeGeometryValid[i] = false;
            }
        }

        if (_lineMaterial != null)
        {
            Destroy(_lineMaterial);
            _lineMaterial = null;
        }
    }
}
