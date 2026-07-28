using System.Collections.Generic;
using DewSuperSmart.config;
using UnityEngine;
using UnityEngine.Rendering;

namespace DewSuperSmart;

internal sealed class MonsterThreatRangeDisplay : MonoBehaviour
{
    private const int CircleSegmentCount = 32;
    private const int ConeSegmentCount = 24;
    private const float ThreatCollectInterval = 0.04f;
    private const float ThreatDrawInterval = 0.033f;
    private const float GroundOffset = 0.16f;
    private const float LineWidth = 0.065f;
    private const float ProjectileLineWidth = 0.075f;
    private const float RedDistanceToHero = 0.8f;
    private const float YellowDistanceToHero = 1.8f;
    private const float RedTimeToImpact = 0.8f;
    private const float YellowTimeToImpact = 1.8f;
    private const int TopRenderQueue = 5000;

    private static readonly Color RedThreatColor = new Color(1f, 0.04f, 0.02f, 0.95f);
    private static readonly Color YellowThreatColor = new Color(1f, 0.76f, 0.06f, 0.9f);
    private static readonly Color GreenThreatColor = new Color(0.18f, 1f, 0.42f, 0.82f);
    private static readonly Vector3[] UnitCirclePoints = BuildUnitCirclePoints();

    private readonly ThreatAnalyzer _threatAnalyzer = new ThreatAnalyzer();
    private readonly List<ThreatZone> _threats = new List<ThreatZone>(128);
    private readonly ThreatMeshLayer[] _layers = new ThreatMeshLayer[3];

    private Material[] _lineMaterials;
    private float _nextThreatCollectTime = float.NegativeInfinity;
    private float _nextThreatDrawTime = float.NegativeInfinity;
    private bool _hasThreatSnapshot;
    private bool _hasThreatMesh;

    private enum ThreatLevel
    {
        Green = 0,
        Yellow = 1,
        Red = 2
    }

    private void Awake()
    {
        Shader shader = Shader.Find("Hidden/Internal-Colored") ??
                        Shader.Find("Sprites/Default") ??
                        Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
        {
            return;
        }

        _lineMaterials =
        [
            CreateLineMaterial(shader, "DewSuperSmart Monster Threat Green", GreenThreatColor, TopRenderQueue - 2),
            CreateLineMaterial(shader, "DewSuperSmart Monster Threat Yellow", YellowThreatColor, TopRenderQueue - 1),
            CreateLineMaterial(shader, "DewSuperSmart Monster Threat Red", RedThreatColor, TopRenderQueue)
        ];
    }

    private void LateUpdate()
    {
        DewSuperSmart instance = DewSuperSmart.Instance;
        if (instance == null || !TryGetLocalHero(out Hero hero))
        {
            InvalidateThreatSnapshot();
            HideAll();
            return;
        }

        PluginConfig config = instance.Config;
        if (!config.ShowMonsterThreatRanges && !config.ShowProjectileThreatRanges)
        {
            InvalidateThreatSnapshot();
            HideAll();
            return;
        }

        Vector3 heroPosition = hero.agentPosition;
        float heroRadius = GetHeroThreatRadius(hero, config);

        float now = Time.unscaledTime;
        bool snapshotUpdated = false;
        if (!_hasThreatSnapshot || now >= _nextThreatCollectTime)
        {
            _threatAnalyzer.CollectThreats(hero, config, _threats, forAutoDodge: false);
            SortThreats(heroPosition, heroRadius);
            _hasThreatSnapshot = true;
            _nextThreatCollectTime = now + ThreatCollectInterval;
            snapshotUpdated = true;
        }

        if (snapshotUpdated || !_hasThreatMesh || now >= _nextThreatDrawTime)
        {
            RebuildThreatMeshes(config, heroPosition, heroRadius);
            _hasThreatMesh = true;
            _nextThreatDrawTime = now + ThreatDrawInterval;
        }
    }

    private static bool TryGetLocalHero(out Hero hero)
    {
        hero = DewPlayer.local?.hero;
        return hero != null && !hero.IsNullInactiveDeadOrKnockedOut();
    }

    private void RebuildThreatMeshes(PluginConfig config, Vector3 heroPosition, float heroRadius)
    {
        EnsureLayers();
        if (_layers[0] == null)
        {
            return;
        }

        for (int i = 0; i < _layers.Length; i++)
        {
            _layers[i].Begin();
        }

        int maxDrawCount = Mathf.Max(config.MaxThreatRenderers, 0);
        float renderY = heroPosition.y + GroundOffset;
        int drawCount = 0;

        for (int i = 0; i < _threats.Count && drawCount < maxDrawCount; i++)
        {
            ThreatZone threat = _threats[i];
            ThreatLevel level = GetThreatLevel(threat, heroPosition, heroRadius);
            float width = threat.IsProjectile ? ProjectileLineWidth : LineWidth;
            _layers[(int)level].AddThreat(threat, renderY, width);
            drawCount++;
        }

        for (int i = 0; i < _layers.Length; i++)
        {
            _layers[i].Apply();
        }
    }

    private static ThreatLevel GetThreatLevel(ThreatZone threat, Vector3 heroPosition, float heroRadius)
    {
        if (!IsActiveThreat(threat))
        {
            return ThreatLevel.Green;
        }

        float distanceToHero = threat.SignedDistance(heroPosition, heroRadius);
        if (IsWithinThreatLevel(distanceToHero, RedDistanceToHero) ||
            IsWithinThreatLevel(threat.TimeToImpact, RedTimeToImpact))
        {
            return ThreatLevel.Red;
        }

        if (IsWithinThreatLevel(distanceToHero, YellowDistanceToHero) ||
            IsWithinThreatLevel(threat.TimeToImpact, YellowTimeToImpact))
        {
            return ThreatLevel.Yellow;
        }

        return ThreatLevel.Green;
    }

    private static bool IsWithinThreatLevel(float value, float threshold)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && Mathf.Max(value, 0f) <= threshold;
    }

    private static bool IsActiveThreat(ThreatZone threat)
    {
        return threat.IsProjectile || threat.Trigger == null || threat.Trigger.Network_isCasting;
    }

    private static float GetHeroThreatRadius(Hero hero, PluginConfig config)
    {
        float radius = hero.Control != null ? hero.Control.outerRadius : 0.45f;
        return Mathf.Max(radius + Mathf.Max(config.ThreatPadding, 0f), 0.1f);
    }

    private void SortThreats(Vector3 heroPosition, float heroRadius)
    {
        _threats.Sort((left, right) => CompareThreats(left, right, heroPosition, heroRadius));
    }

    private static int CompareThreats(ThreatZone left, ThreatZone right, Vector3 heroPosition, float heroRadius)
    {
        if (left.IsProjectile != right.IsProjectile)
        {
            return right.IsProjectile.CompareTo(left.IsProjectile);
        }

        float leftRisk = left.RiskAt(heroPosition, heroRadius);
        float rightRisk = right.RiskAt(heroPosition, heroRadius);
        int riskCompare = rightRisk.CompareTo(leftRisk);
        if (riskCompare != 0)
        {
            return riskCompare;
        }

        int timeCompare = CompareTimeToImpact(left.TimeToImpact, right.TimeToImpact);
        if (timeCompare != 0)
        {
            return timeCompare;
        }

        int weightCompare = right.Weight.CompareTo(left.Weight);
        if (weightCompare != 0)
        {
            return weightCompare;
        }

        float leftDistance = Vector2.Distance(left.ClosestPoint(heroPosition).ToXY(), heroPosition.ToXY());
        float rightDistance = Vector2.Distance(right.ClosestPoint(heroPosition).ToXY(), heroPosition.ToXY());
        return leftDistance.CompareTo(rightDistance);
    }

    private static int CompareTimeToImpact(float left, float right)
    {
        bool leftFinite = !float.IsNaN(left) && !float.IsInfinity(left);
        bool rightFinite = !float.IsNaN(right) && !float.IsInfinity(right);
        if (leftFinite != rightFinite)
        {
            return rightFinite.CompareTo(leftFinite);
        }

        return leftFinite ? left.CompareTo(right) : 0;
    }

    private void EnsureLayers()
    {
        if (_lineMaterials == null)
        {
            return;
        }

        EnsureLayer(ThreatLevel.Green, "Green");
        EnsureLayer(ThreatLevel.Yellow, "Yellow");
        EnsureLayer(ThreatLevel.Red, "Red");
    }

    private void EnsureLayer(ThreatLevel level, string name)
    {
        int index = (int)level;
        if (_layers[index] == null)
        {
            _layers[index] = new ThreatMeshLayer($"DewSuperSmart_MonsterThreat_{name}", _lineMaterials[index], GetThreatColor(level));
        }

        _layers[index].Ensure(transform);
    }

    private void HideAll()
    {
        for (int i = 0; i < _layers.Length; i++)
        {
            _layers[i]?.Hide();
        }
    }

    private void InvalidateThreatSnapshot()
    {
        _threats.Clear();
        _hasThreatSnapshot = false;
        _hasThreatMesh = false;
        _nextThreatCollectTime = float.NegativeInfinity;
        _nextThreatDrawTime = float.NegativeInfinity;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _layers.Length; i++)
        {
            _layers[i]?.Destroy();
            _layers[i] = null;
        }

        if (_lineMaterials != null)
        {
            for (int i = 0; i < _lineMaterials.Length; i++)
            {
                if (_lineMaterials[i] != null)
                {
                    Destroy(_lineMaterials[i]);
                    _lineMaterials[i] = null;
                }
            }

            _lineMaterials = null;
        }
    }

    private static Color GetThreatColor(ThreatLevel level)
    {
        switch (level)
        {
            case ThreatLevel.Red:
                return RedThreatColor;
            case ThreatLevel.Yellow:
                return YellowThreatColor;
            default:
                return GreenThreatColor;
        }
    }

    private static Vector3[] BuildUnitCirclePoints()
    {
        Vector3[] points = new Vector3[CircleSegmentCount];
        for (int i = 0; i < CircleSegmentCount; i++)
        {
            float angle = i * Mathf.PI * 2f / CircleSegmentCount;
            points[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        return points;
    }

    private static Vector3 RenderPoint(Vector3 point, float renderY)
    {
        point.y = renderY;
        return point;
    }

    private static Vector3 NormalizeFlat(Vector3 value)
    {
        value.y = 0f;
        if (value.sqrMagnitude <= 0.0001f)
        {
            return Vector3.forward;
        }

        return value.normalized;
    }

    private static Material CreateLineMaterial(Shader shader, string name, Color color, int renderQueue)
    {
        Material material = new Material(shader)
        {
            name = name,
            hideFlags = HideFlags.HideAndDontSave
        };
        ConfigureTopMostMaterial(material, renderQueue);
        SetMaterialColor(material, color);
        return material;
    }

    private static void ConfigureTopMostMaterial(Material material, int renderQueue)
    {
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetOverrideTag("Queue", "Overlay");
        SetIntIfPresent(material, "_SrcBlend", (int)BlendMode.SrcAlpha);
        SetIntIfPresent(material, "_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        SetIntIfPresent(material, "_Cull", (int)CullMode.Off);
        SetIntIfPresent(material, "_ZWrite", 0);
        SetIntIfPresent(material, "_ZTest", (int)CompareFunction.Always);
        material.renderQueue = Mathf.Clamp(renderQueue, 0, TopRenderQueue);
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
    }

    private static void SetIntIfPresent(Material material, string propertyName, int value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetInt(propertyName, value);
        }
    }

    private sealed class ThreatMeshLayer
    {
        private readonly string _name;
        private readonly Material _material;
        private readonly Color _color;
        private readonly List<Vector3> _vertices = new List<Vector3>(4096);
        private readonly List<Color> _colors = new List<Color>(4096);
        private readonly List<int> _triangles = new List<int>(6144);

        private GameObject _root;
        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private Mesh _mesh;

        public ThreatMeshLayer(string name, Material material, Color color)
        {
            _name = name;
            _material = material;
            _color = color;
        }

        public void Ensure(Transform parent)
        {
            if (_renderer != null)
            {
                if (_material != null && _renderer.sharedMaterial != _material)
                {
                    _renderer.sharedMaterial = _material;
                }

                return;
            }

            _root = new GameObject(_name);
            _root.transform.SetParent(null, false);
            _root.transform.position = Vector3.zero;
            _root.transform.rotation = Quaternion.identity;
            _root.transform.localScale = Vector3.one;
            _root.hideFlags = HideFlags.HideAndDontSave;

            _filter = _root.AddComponent<MeshFilter>();
            _renderer = _root.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.sortingOrder = TopRenderQueue;

            if (_material != null)
            {
                _renderer.sharedMaterial = _material;
            }

            _mesh = new Mesh
            {
                name = _name,
                hideFlags = HideFlags.HideAndDontSave,
                indexFormat = IndexFormat.UInt32
            };
            _mesh.MarkDynamic();
            _filter.sharedMesh = _mesh;
        }

        public void Begin()
        {
            _vertices.Clear();
            _colors.Clear();
            _triangles.Clear();
        }

        public void AddThreat(ThreatZone threat, float renderY, float width)
        {
            switch (threat.Kind)
            {
                case ThreatZoneKind.Circle:
                    AddCircle(threat, renderY, width);
                    break;
                case ThreatZoneKind.Cone:
                    AddCone(threat, renderY, width);
                    break;
                case ThreatZoneKind.Line:
                    AddBox(threat, renderY, width);
                    break;
            }
        }

        public void Apply()
        {
            if (_mesh == null || _renderer == null)
            {
                return;
            }

            if (_vertices.Count == 0)
            {
                _mesh.Clear();
                _renderer.enabled = false;
                return;
            }

            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.RecalculateBounds();
            _renderer.enabled = true;
        }

        public void Hide()
        {
            if (_renderer != null)
            {
                _renderer.enabled = false;
            }
        }

        public void Destroy()
        {
            if (_mesh != null)
            {
                UnityEngine.Object.Destroy(_mesh);
                _mesh = null;
            }

            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
                _filter = null;
                _renderer = null;
            }
        }

        private void AddCircle(ThreatZone threat, float renderY, float width)
        {
            Vector3 center = RenderPoint(threat.Center, renderY);
            float radius = Mathf.Max(threat.Radius, 0.01f);

            for (int i = 0; i < CircleSegmentCount; i++)
            {
                Vector3 start = center + UnitCirclePoints[i] * radius;
                Vector3 end = center + UnitCirclePoints[(i + 1) % CircleSegmentCount] * radius;
                AddSegment(start, end, width);
            }
        }

        private void AddCone(ThreatZone threat, float renderY, float width)
        {
            float angle = Mathf.Clamp(threat.Angle, 0.01f, 360f);
            if (angle >= 359.5f)
            {
                AddCircle(
                    ThreatZone.Circle(
                        threat.Source,
                        threat.Trigger,
                        threat.Origin,
                        threat.Radius,
                        threat.IsReady,
                        threat.Weight,
                        threat.TimeToImpact),
                    renderY,
                    width);
                return;
            }

            Vector3 origin = RenderPoint(threat.Origin, renderY);
            Vector3 direction = NormalizeFlat(threat.Direction);
            float radius = Mathf.Max(threat.Radius, 0.01f);
            int segmentCount = Mathf.Max(2, Mathf.CeilToInt(ConeSegmentCount * angle / 360f));
            float halfAngle = angle * 0.5f;
            float baseAngle = Mathf.Atan2(direction.z, direction.x);

            Vector3 previousArc = origin;
            for (int i = 0; i <= segmentCount; i++)
            {
                float offset = (-halfAngle + angle * i / segmentCount) * Mathf.Deg2Rad;
                Vector3 arc = origin + new Vector3(Mathf.Cos(baseAngle + offset) * radius, 0f, Mathf.Sin(baseAngle + offset) * radius);
                if (i == 0)
                {
                    AddSegment(origin, arc, width);
                }
                else
                {
                    AddSegment(previousArc, arc, width);
                }

                previousArc = arc;
            }

            AddSegment(previousArc, origin, width);
        }

        private void AddBox(ThreatZone threat, float renderY, float width)
        {
            Vector3 direction = NormalizeFlat(threat.Direction);
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            if (right.sqrMagnitude <= 0.0001f)
            {
                right = Vector3.right;
            }

            right.Normalize();

            float length = Mathf.Max(threat.Length, 0.01f);
            float halfWidth = Mathf.Max(threat.Width, 0.01f) * 0.5f;
            Vector3 start = RenderPoint(threat.Origin, renderY);
            Vector3 end = RenderPoint(threat.Origin + direction * length, renderY);
            Vector3 half = right * halfWidth;

            Vector3 a = start - half;
            Vector3 b = start + half;
            Vector3 c = end + half;
            Vector3 d = end - half;
            AddSegment(a, b, width);
            AddSegment(b, c, width);
            AddSegment(c, d, width);
            AddSegment(d, a, width);
        }

        private void AddSegment(Vector3 start, Vector3 end, float width)
        {
            Vector3 delta = end - start;
            delta.y = 0f;
            float sqrLength = delta.sqrMagnitude;
            if (sqrLength <= 0.0001f)
            {
                return;
            }

            float scale = Mathf.Max(width, 0.01f) * 0.5f / Mathf.Sqrt(sqrLength);
            Vector3 normal = new Vector3(-delta.z * scale, 0f, delta.x * scale);
            int index = _vertices.Count;

            _vertices.Add(start + normal);
            _vertices.Add(start - normal);
            _vertices.Add(end - normal);
            _vertices.Add(end + normal);

            _colors.Add(_color);
            _colors.Add(_color);
            _colors.Add(_color);
            _colors.Add(_color);

            _triangles.Add(index);
            _triangles.Add(index + 1);
            _triangles.Add(index + 2);
            _triangles.Add(index);
            _triangles.Add(index + 2);
            _triangles.Add(index + 3);
        }
    }
}
