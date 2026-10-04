using System;
using System.Collections.Generic;
using DewRoomGuidance.config;
using UnityEngine;

namespace DewRoomGuidance;

internal sealed class RoomGuidanceOverlay : MonoBehaviour
{
    private const float ScanInterval = 0.3f;
    private const float IconSize = 38f;
    private const float ScreenPadding = 30f;
    private const int MaxMarkers = 16;
    private const int ThumbnailLayer = 31;
    private const int ThumbnailResolution = 128;

    private readonly List<Marker> _markers = new();
    private readonly Dictionary<int, RenderTexture> _modelThumbnails = new();
    private readonly Dictionary<int, Quaternion> _thumbnailViewRotations = new();
    private readonly HashSet<int> _failedThumbnailActors = new();
    private float _nextScanTime;
    private GUIStyle _labelStyle;
    private Camera _thumbnailCamera;
    private Light _thumbnailKeyLight;
    private Light _thumbnailFillLight;
    private static Texture2D _circleTexture;
    private static bool _circleTextureSearched;
    private static Texture2D _fallbackDotTexture;
    private static Texture2D _arrowTexture;

    private struct Marker
    {
        public Actor Actor;
        public Texture BadgeTexture;
        public Sprite Icon;
        public Texture SceneTexture;
        public string Kind;
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextScanTime)
        {
            _nextScanTime = Time.unscaledTime + ScanInterval;
            RefreshTargets();
        }
    }

    // 只缓存当前本地玩家仍可使用/拾取的场景目标，不改变游戏对象状态。
    private void RefreshTargets()
    {
        _markers.Clear();
        ActorManager actorManager = NetworkedManagerBase<ActorManager>.softInstance;
        Hero hero = DewPlayer.local != null ? DewPlayer.local.hero : null;
        PluginConfig config = DewRoomGuidance.Instance != null ? DewRoomGuidance.Instance.Config : null;
        if (actorManager == null || hero == null || config == null)
        {
            return;
        }

        foreach (Actor actor in actorManager.allActors)
        {
            if (actor == null || !actor.isActiveAndEnabled)
            {
                continue;
            }

            if (actor is Shrine shrine && config.IsShrineEnabled(shrine.GetType().Name) && shrine.isAvailable && (shrine.GetType().Name == "Shrine_LoopCat" || !shrine.isLocked) && shrine.CanInteract(hero))
            {
                _markers.Add(new Marker
                {
                    Actor = shrine,
                    BadgeTexture = FindNodeBadgeTexture(shrine),
                    Icon = GetThumbnail(shrine),
                    SceneTexture = FindSceneTexture(shrine),
                    Kind = GetShrineName(shrine)
                });
            }
            else if (config.GuideEssences && actor is Gem gem && gem.owner == null && gem.handOwner == null && (gem.tempOwner == null || gem.tempOwner == DewPlayer.local))
            {
                _markers.Add(new Marker { Actor = gem, BadgeTexture = FindNodeBadgeTexture(gem), Icon = GetThumbnail(gem), SceneTexture = FindSceneTexture(gem), Kind = DewLocalization.GetGemName(gem) });
            }
            else if (config.GuideSkills && actor is SkillTrigger skill && skill.owner == null && skill.handOwner == null && (skill.tempOwner == null || skill.tempOwner == DewPlayer.local))
            {
                _markers.Add(new Marker { Actor = skill, BadgeTexture = FindNodeBadgeTexture(skill), Icon = GetThumbnail(skill), SceneTexture = FindSceneTexture(skill), Kind = DewLocalization.GetSkillName(skill, 0) });
            }
            else if (config.GuideArtifacts && actor is Artifact artifact && HasNoArtifact() && artifact.CanInteract(hero))
            {
                _markers.Add(new Marker { Actor = artifact, BadgeTexture = FindNodeBadgeTexture(artifact), Icon = GetThumbnail(artifact), SceneTexture = FindSceneTexture(artifact), Kind = artifact.nameRawText });
            }
            else if (config.GuideStones && actor is PropEntity prop && !prop.isDead && actor.GetType().Name.StartsWith("PropEnt_Stone_", StringComparison.Ordinal))
            {
                _markers.Add(new Marker { Actor = prop, BadgeTexture = FindNodeBadgeTexture(prop), Icon = GetThumbnail(prop), SceneTexture = FindSceneTexture(prop), Kind = DewLocalization.GetUIValue(prop.GetType().Name + "_Name") });
            }
            else if (config.GuideGoldenLizards && actor is Monster monster && monster.isAlive && actor.GetType().Name.StartsWith("Mon_GoldenLizard_", StringComparison.Ordinal))
            {
                _markers.Add(new Marker { Actor = monster, BadgeTexture = FindNodeBadgeTexture(monster), Icon = GetThumbnail(monster), SceneTexture = FindSceneTexture(monster), Kind = DewLocalization.GetUIValue(monster.GetType().Name + "_Name") });
            }
        }

        PruneModelThumbnails();
    }

    private void OnDestroy()
    {
        foreach (RenderTexture texture in _modelThumbnails.Values)
        {
            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }
        }

        _modelThumbnails.Clear();
        _thumbnailViewRotations.Clear();
        _failedThumbnailActors.Clear();
        if (_thumbnailCamera != null)
        {
            Destroy(_thumbnailCamera.gameObject);
        }

        if (_thumbnailKeyLight != null)
        {
            Destroy(_thumbnailKeyLight.gameObject);
        }

        if (_thumbnailFillLight != null)
        {
            Destroy(_thumbnailFillLight.gameObject);
        }
    }

    private static bool HasNoArtifact()
    {
        QuestManager questManager = NetworkedManagerBase<QuestManager>.softInstance;
        return questManager == null || questManager.currentArtifact == null;
    }

    private static string GetShrineName(Shrine shrine)
    {
        if (shrine is IShrineCustomName customName)
        {
            string name = customName.GetRawName();
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }
        }

        if (shrine is ICustomInteractable customInteractable)
        {
            string name = customInteractable.nameRawText;
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }
        }

        return DewLocalization.GetUIValue(shrine.GetType().Name + "_Name");
    }

    private static Sprite GetThumbnail(Actor actor)
    {
        if (actor is Gem gem && gem.icon != null)
        {
            return gem.icon;
        }

        if (actor is SkillTrigger skill)
        {
            Sprite skillIcon = GetSkillIcon(skill);
            if (skillIcon != null)
            {
                return skillIcon;
            }
        }

        if (actor is Artifact artifact && artifact.icon != null)
        {
            return artifact.icon;
        }

        return FindSprite(actor);
    }

    private static Sprite FindSprite(Component component)
    {
        if (component == null)
        {
            return null;
        }

        if (component is Shrine shrine && shrine.model != null)
        {
            Sprite modelSprite = FindSprite(shrine.model.transform);
            if (modelSprite != null)
            {
                return modelSprite;
            }
        }

        SpriteRenderer[] renderers = component.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer != null && renderer.sprite != null)
            {
                return renderer.sprite;
            }
        }

        return null;
    }

    private static Texture FindSceneTexture(Actor actor)
    {
        if (actor == null)
        {
            return null;
        }

        if (actor is Shrine shrine && shrine.model != null)
        {
            Texture modelTexture = FindRendererTexture(shrine.model.GetComponentsInChildren<Renderer>(true));
            if (modelTexture != null)
            {
                return modelTexture;
            }
        }

        return FindRendererTexture(actor.GetComponentsInChildren<Renderer>(true));
    }

    private static Texture FindNodeBadgeTexture(Actor actor)
    {
        if (actor == null)
        {
            return null;
        }

        foreach (ItemWorldModel model in actor.GetComponentsInChildren<ItemWorldModel>(true))
        {
            Texture texture = GetRendererTexture(model != null ? model.iconQuad : null);
            if (texture != null)
            {
                return texture;
            }
        }

        foreach (ArtifactWorldModel model in actor.GetComponentsInChildren<ArtifactWorldModel>(true))
        {
            Texture texture = GetRendererTexture(model != null ? model.icon : null);
            if (texture != null)
            {
                return texture;
            }
        }

        return null;
    }

    private static Texture GetRendererTexture(Renderer renderer)
    {
        if (renderer == null)
        {
            return null;
        }

        foreach (Material material in renderer.sharedMaterials)
        {
            Texture texture = GetMaterialTexture(material);
            if (texture != null && texture != Texture2D.whiteTexture)
            {
                return texture;
            }
        }

        return null;
    }

    private static Texture FindRendererTexture(Renderer[] renderers)
    {
        foreach (Renderer renderer in renderers)
        {
            Texture texture = GetRendererTexture(renderer);
            if (texture != null)
            {
                return texture;
            }
        }

        return null;
    }

    private static Texture GetMaterialTexture(Material material)
    {
        if (material == null)
        {
            return null;
        }

        foreach (string propertyName in material.GetTexturePropertyNames())
        {
            Texture texture = material.GetTexture(propertyName);
            if (texture != null && texture != Texture2D.whiteTexture)
            {
                return texture;
            }
        }

        return null;
    }

    private static Sprite GetSkillIcon(SkillTrigger skill)
    {
        if (skill.configs == null || skill.configs.Length == 0)
        {
            return null;
        }

        return skill.configs[0].triggerIcon;
    }

    private void OnGUI()
    {
        Camera camera = DewCamera.softInstance != null ? DewCamera.softInstance.mainCamera : Camera.main;
        if (camera == null || _markers.Count == 0)
        {
            return;
        }

        EnsureStyles();
        List<ProjectedMarker> projected = new();
        Vector2 screenCenter = new(Screen.width * 0.5f, Screen.height * 0.5f);
        Rect bounds = new(ScreenPadding, ScreenPadding, Screen.width - ScreenPadding * 2f, Screen.height - ScreenPadding * 2f);
        Vector3 playerPosition = DewPlayer.local != null && DewPlayer.local.hero != null
            ? DewPlayer.local.hero.transform.position
            : Vector3.zero;

        foreach (Marker marker in _markers)
        {
            if (marker.Actor == null)
            {
                continue;
            }

            Vector3 screen = camera.WorldToScreenPoint(marker.Actor.transform.position);
            Vector2 point = new(screen.x, Screen.height - screen.y);
            Vector2 direction = point - screenCenter;
            if (screen.z < 0f)
            {
                direction = -direction;
            }

            bool onScreen = screen.z > 0f && point.x >= 0f && point.x <= Screen.width && point.y >= 0f && point.y <= Screen.height;
            if (onScreen)
            {
                continue;
            }

            if (direction.sqrMagnitude < 0.01f)
            {
                direction = Vector2.up;
            }

            Vector2 edgePoint = IntersectEdge(screenCenter, direction.normalized, bounds);
            projected.Add(new ProjectedMarker
            {
                Marker = marker,
                Point = edgePoint,
                Distance = Vector3.Distance(playerPosition, marker.Actor.transform.position)
            });
        }

        projected.Sort((left, right) => left.Distance.CompareTo(right.Distance));
        int drawCount = Mathf.Min(projected.Count, MaxMarkers);
        for (int i = 0; i < drawCount; i++)
        {
            projected[i] = MoveAwayFromExistingMarkers(projected[i], projected, i, bounds);
            DrawMarker(projected[i]);
        }
    }

    private static ProjectedMarker MoveAwayFromExistingMarkers(ProjectedMarker marker, List<ProjectedMarker> markers, int markerIndex, Rect bounds)
    {
        bool onHorizontalEdge = Mathf.Min(Mathf.Abs(marker.Point.y - bounds.yMin), Mathf.Abs(marker.Point.y - bounds.yMax))
            < Mathf.Min(Mathf.Abs(marker.Point.x - bounds.xMin), Mathf.Abs(marker.Point.x - bounds.xMax));
        Vector2 originalPoint = marker.Point;
        for (int attempt = 0; attempt <= markerIndex; attempt++)
        {
            int step = attempt == 0 ? 0 : (attempt + 1) / 2 * (attempt % 2 == 1 ? 1 : -1);
            Vector2 candidate = originalPoint;
            if (onHorizontalEdge)
            {
                candidate.x = Mathf.Clamp(originalPoint.x + step * (IconSize + 6f), bounds.xMin, bounds.xMax);
            }
            else
            {
                candidate.y = Mathf.Clamp(originalPoint.y + step * (IconSize + 6f), bounds.yMin, bounds.yMax);
            }

            bool overlaps = false;
            for (int previous = 0; previous < markerIndex; previous++)
            {
                if (Vector2.Distance(candidate, markers[previous].Point) < IconSize + 6f)
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps)
            {
                marker.Point = candidate;
                return marker;
            }
        }

        return marker;
    }

    private static Vector2 IntersectEdge(Vector2 center, Vector2 direction, Rect bounds)
    {
        float halfWidth = Mathf.Max(1f, bounds.width * 0.5f);
        float halfHeight = Mathf.Max(1f, bounds.height * 0.5f);
        float scale = Mathf.Min(halfWidth / Mathf.Max(Mathf.Abs(direction.x), 0.0001f),
            halfHeight / Mathf.Max(Mathf.Abs(direction.y), 0.0001f));
        Vector2 point = center + direction * scale;
        return new Vector2(Mathf.Clamp(point.x, bounds.xMin, bounds.xMax), Mathf.Clamp(point.y, bounds.yMin, bounds.yMax));
    }

    private void DrawMarker(ProjectedMarker projected)
    {
        const float size = IconSize;
        Rect rect = new(projected.Point.x - size * 0.5f, projected.Point.y - size * 0.5f, size, size);
        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.98f);
        GUI.DrawTexture(new Rect(rect.x - 4.5f, rect.y - 4.5f, size + 9f, size + 9f), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.72f, 0.12f, 1f);
        GUI.DrawTexture(new Rect(rect.x - 2.5f, rect.y - 2.5f, size + 5f, size + 5f), Texture2D.whiteTexture);
        GUI.color = new Color(0.02f, 0.52f, 0.68f, 1f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        Texture modelThumbnail = GetModelThumbnail(projected.Marker.Actor);
        if (modelThumbnail != null)
        {
            GUI.DrawTexture(new Rect(rect.x + 3f, rect.y + 3f, size - 6f, size - 6f), modelThumbnail, ScaleMode.ScaleToFit, true);
        }
        else if (projected.Marker.Icon != null && projected.Marker.Icon.texture != null)
        {
            Rect iconRect = new(rect.x + 3f, rect.y + 3f, size - 6f, size - 6f);
            Rect textureRect = projected.Marker.Icon.textureRect;
            Rect uv = new(textureRect.x / projected.Marker.Icon.texture.width,
                textureRect.y / projected.Marker.Icon.texture.height,
                textureRect.width / projected.Marker.Icon.texture.width,
                textureRect.height / projected.Marker.Icon.texture.height);
            GUI.DrawTextureWithTexCoords(iconRect, projected.Marker.Icon.texture, uv, true);
        }
        else if (projected.Marker.BadgeTexture != null)
        {
            GUI.DrawTexture(new Rect(rect.x + 3f, rect.y + 3f, size - 6f, size - 6f), projected.Marker.BadgeTexture, ScaleMode.ScaleToFit, true);
        }
        else if (projected.Marker.SceneTexture != null)
        {
            GUI.DrawTexture(new Rect(rect.x + 3f, rect.y + 3f, size - 6f, size - 6f), projected.Marker.SceneTexture, ScaleMode.ScaleToFit, true);
        }
        else if (GetCircleTexture() != null)
        {
            GUI.DrawTexture(new Rect(rect.x + 8f, rect.y + 8f, size - 16f, size - 16f), _circleTexture, ScaleMode.ScaleToFit, true);
        }
        else
        {
            GUI.DrawTexture(new Rect(rect.x + 8f, rect.y + 8f, size - 16f, size - 16f), GetFallbackDotTexture(), ScaleMode.ScaleToFit, true);
        }

        Vector2 outward = (projected.Point - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)).normalized;
        Vector2 arrowCenter = projected.Point + outward * 29f;
        Matrix4x4 oldMatrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(outward.y, outward.x) * Mathf.Rad2Deg + 90f, arrowCenter);
        GUI.DrawTexture(new Rect(arrowCenter.x - 5f, arrowCenter.y - 7f, 10f, 14f), GetArrowTexture(), ScaleMode.ScaleToFit, true);
        GUI.matrix = oldMatrix;

        GUI.color = new Color(1f, 1f, 1f, 0.95f);
        float labelY = projected.Point.y > Screen.height * 0.5f ? rect.y - 21f : rect.y + size + 1f;
        string label = $"{projected.Marker.Kind} {projected.Distance:0}m";
        float labelWidth = Mathf.Min(Screen.width - 16f, Mathf.Max(size + 56f, _labelStyle.CalcSize(new GUIContent(label)).x + 12f));
        float labelX = Mathf.Clamp(projected.Point.x - labelWidth * 0.5f, 8f, Screen.width - labelWidth - 8f);
        GUI.Label(new Rect(labelX, labelY, labelWidth, 20f), label, _labelStyle);
        GUI.color = oldColor;
    }

    private Texture GetModelThumbnail(Actor actor)
    {
        // 精粹和技能本身有明确的游戏图标，避免把世界模型中的图标平面当成立体单位渲染。
        if (actor == null || actor is Gem || actor is SkillTrigger)
        {
            return null;
        }

        int actorId = actor.GetInstanceID();
        Quaternion viewRotation = GetThumbnailViewRotation();
        if (_modelThumbnails.TryGetValue(actorId, out RenderTexture cached) && cached != null
            && _thumbnailViewRotations.TryGetValue(actorId, out Quaternion cachedRotation)
            && Quaternion.Angle(cachedRotation, viewRotation) < 0.1f)
        {
            return cached;
        }

        if (_modelThumbnails.TryGetValue(actorId, out RenderTexture existing) && existing != null)
        {
            existing.Release();
            Destroy(existing);
            _modelThumbnails.Remove(actorId);
            _thumbnailViewRotations.Remove(actorId);
        }

        if (_failedThumbnailActors.Contains(actorId))
        {
            return null;
        }

        RenderTexture thumbnail = RenderModelThumbnail(actor, viewRotation);
        if (thumbnail == null)
        {
            _failedThumbnailActors.Add(actorId);
            return null;
        }

        _modelThumbnails[actorId] = thumbnail;
        _thumbnailViewRotations[actorId] = viewRotation;
        return thumbnail;
    }

    private static Quaternion GetThumbnailViewRotation()
    {
        Camera gameCamera = DewCamera.softInstance != null ? DewCamera.softInstance.mainCamera : Camera.main;
        if (gameCamera == null)
        {
            return Quaternion.identity;
        }

        Vector3 euler = gameCamera.transform.rotation.eulerAngles;
        float pitch = Mathf.Round(euler.x / 10f) * 10f;
        float yaw = Mathf.Round(euler.y / 10f) * 10f;
        return Quaternion.Euler(pitch, yaw, 0f);
    }

    private RenderTexture RenderModelThumbnail(Actor actor, Quaternion viewRotation)
    {
        Transform sourceRoot = actor is Shrine shrine && shrine.model != null ? shrine.model.transform : actor.transform;
        GameObject previewRoot = new("DewRoomGuidance_Thumbnail")
        {
            hideFlags = HideFlags.HideAndDontSave,
            layer = ThumbnailLayer
        };
        if (sourceRoot.parent != null)
        {
            previewRoot.transform.position = sourceRoot.parent.position;
            previewRoot.transform.rotation = sourceRoot.parent.rotation;
            previewRoot.transform.localScale = sourceRoot.parent.lossyScale;
        }

        List<Renderer> previewRenderers = new();
        List<Mesh> bakedMeshes = new();
        CloneVisualHierarchy(sourceRoot, previewRoot.transform, previewRenderers, bakedMeshes);

        if (previewRenderers.Count == 0)
        {
            Destroy(previewRoot);
            foreach (Mesh mesh in bakedMeshes)
            {
                Destroy(mesh);
            }

            return null;
        }

        Bounds bounds = previewRenderers[0].bounds;
        for (int i = 1; i < previewRenderers.Count; i++)
        {
            bounds.Encapsulate(previewRenderers[i].bounds);
        }

        previewRoot.transform.position -= bounds.center;
        Vector3 extents = bounds.extents;
        Vector3 cameraRight = viewRotation * Vector3.right;
        Vector3 cameraUp = viewRotation * Vector3.up;
        Vector3 cameraForward = viewRotation * Vector3.forward;
        float projectedWidth = Mathf.Abs(cameraRight.x) * extents.x + Mathf.Abs(cameraRight.y) * extents.y + Mathf.Abs(cameraRight.z) * extents.z;
        float projectedHeight = Mathf.Abs(cameraUp.x) * extents.x + Mathf.Abs(cameraUp.y) * extents.y + Mathf.Abs(cameraUp.z) * extents.z;
        float depthExtent = Mathf.Abs(cameraForward.x) * extents.x + Mathf.Abs(cameraForward.y) * extents.y + Mathf.Abs(cameraForward.z) * extents.z;
        float halfSize = Mathf.Max(projectedWidth, projectedHeight, 0.05f) * 1.3f;
        EnsureThumbnailCamera();

        RenderTexture thumbnail = new(ThumbnailResolution, ThumbnailResolution, 16, RenderTextureFormat.ARGB32)
        {
            name = "DewRoomGuidance_Thumbnail_" + actor.GetInstanceID(),
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        thumbnail.Create();
        _thumbnailCamera.orthographicSize = halfSize;
        _thumbnailCamera.nearClipPlane = 0.01f;
        float cameraDistance = depthExtent + halfSize * 2f;
        _thumbnailCamera.farClipPlane = Mathf.Max(10f, cameraDistance + depthExtent + 1f);
        _thumbnailCamera.transform.rotation = viewRotation;
        _thumbnailCamera.transform.position = -cameraForward * cameraDistance;
        _thumbnailCamera.targetTexture = thumbnail;
        _thumbnailCamera.Render();
        _thumbnailCamera.targetTexture = null;

        previewRoot.SetActive(false);
        Destroy(previewRoot);
        foreach (Mesh mesh in bakedMeshes)
        {
            Destroy(mesh);
        }

        return thumbnail;
    }

    private void EnsureThumbnailCamera()
    {
        if (_thumbnailCamera != null)
        {
            return;
        }

        GameObject cameraObject = new("DewRoomGuidance_ThumbnailCamera")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        _thumbnailCamera = cameraObject.AddComponent<Camera>();
        _thumbnailCamera.enabled = false;
        _thumbnailCamera.clearFlags = CameraClearFlags.SolidColor;
        _thumbnailCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        _thumbnailCamera.cullingMask = 1 << ThumbnailLayer;
        _thumbnailCamera.orthographic = true;
        _thumbnailCamera.allowHDR = false;

        _thumbnailKeyLight = CreateThumbnailLight("DewRoomGuidance_ThumbnailKeyLight", new Color(1f, 0.91f, 0.78f), 1.25f, new Vector3(38f, -32f, 0f));
        _thumbnailFillLight = CreateThumbnailLight("DewRoomGuidance_ThumbnailFillLight", new Color(0.62f, 0.82f, 1f), 0.55f, new Vector3(18f, 148f, 0f));
    }

    private static Light CreateThumbnailLight(string name, Color color, float intensity, Vector3 eulerAngles)
    {
        GameObject lightObject = new(name)
        {
            hideFlags = HideFlags.HideAndDontSave,
            layer = ThumbnailLayer
        };
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = color;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        light.cullingMask = 1 << ThumbnailLayer;
        light.transform.rotation = Quaternion.Euler(eulerAngles);
        return light;
    }

    private static void CloneVisualHierarchy(Transform source, Transform parent, List<Renderer> renderers, List<Mesh> bakedMeshes)
    {
        GameObject clone = new(source.name)
        {
            hideFlags = HideFlags.HideAndDontSave,
            layer = ThumbnailLayer
        };
        Transform cloneTransform = clone.transform;
        cloneTransform.SetParent(parent, false);
        cloneTransform.localPosition = source.localPosition;
        cloneTransform.localRotation = source.localRotation;
        cloneTransform.localScale = source.localScale;

        MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
        MeshRenderer sourceMeshRenderer = source.GetComponent<MeshRenderer>();
        if (sourceFilter != null && sourceFilter.sharedMesh != null && sourceMeshRenderer != null)
        {
            clone.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
            MeshRenderer renderer = clone.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = sourceMeshRenderer.sharedMaterials;
            renderer.enabled = sourceMeshRenderer.enabled;
            renderers.Add(renderer);
        }

        SkinnedMeshRenderer sourceSkinnedRenderer = source.GetComponent<SkinnedMeshRenderer>();
        if (sourceSkinnedRenderer != null && sourceSkinnedRenderer.sharedMesh != null)
        {
            Mesh bakedMesh = new();
            sourceSkinnedRenderer.BakeMesh(bakedMesh);
            bakedMeshes.Add(bakedMesh);
            clone.AddComponent<MeshFilter>().sharedMesh = bakedMesh;
            MeshRenderer renderer = clone.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = sourceSkinnedRenderer.sharedMaterials;
            renderer.enabled = sourceSkinnedRenderer.enabled;
            renderers.Add(renderer);
        }

        SpriteRenderer sourceSpriteRenderer = source.GetComponent<SpriteRenderer>();
        if (sourceSpriteRenderer != null && sourceSpriteRenderer.sprite != null)
        {
            SpriteRenderer renderer = clone.AddComponent<SpriteRenderer>();
            renderer.sprite = sourceSpriteRenderer.sprite;
            renderer.color = sourceSpriteRenderer.color;
            renderer.flipX = sourceSpriteRenderer.flipX;
            renderer.flipY = sourceSpriteRenderer.flipY;
            renderer.sortingOrder = sourceSpriteRenderer.sortingOrder;
            renderer.sharedMaterial = sourceSpriteRenderer.sharedMaterial;
            renderer.enabled = sourceSpriteRenderer.enabled;
            renderers.Add(renderer);
        }

        for (int i = 0; i < source.childCount; i++)
        {
            CloneVisualHierarchy(source.GetChild(i), cloneTransform, renderers, bakedMeshes);
        }

        clone.SetActive(source.gameObject.activeSelf);
    }

    private void PruneModelThumbnails()
    {
        HashSet<int> activeActors = new();
        foreach (Marker marker in _markers)
        {
            if (marker.Actor != null)
            {
                activeActors.Add(marker.Actor.GetInstanceID());
            }
        }

        List<int> staleIds = new();
        foreach (int actorId in _modelThumbnails.Keys)
        {
            if (!activeActors.Contains(actorId))
            {
                staleIds.Add(actorId);
            }
        }

        foreach (int actorId in staleIds)
        {
            RenderTexture texture = _modelThumbnails[actorId];
            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }

            _modelThumbnails.Remove(actorId);
            _thumbnailViewRotations.Remove(actorId);
        }

        _failedThumbnailActors.RemoveWhere(actorId => !activeActors.Contains(actorId));
    }

    private void EnsureStyles()
    {
        if (_labelStyle != null)
        {
            return;
        }

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
    }

    private static Texture2D GetCircleTexture()
    {
        if (_circleTextureSearched)
        {
            return _circleTexture;
        }

        _circleTextureSearched = true;
        foreach (Texture2D texture in Resources.FindObjectsOfTypeAll<Texture2D>())
        {
            if (texture != null && texture.name == "Circle_1")
            {
                _circleTexture = texture;
                return texture;
            }
        }

        foreach (Sprite sprite in Resources.FindObjectsOfTypeAll<Sprite>())
        {
            if (sprite != null && (sprite.name == "Circle" || sprite.name == "Circle_1"))
            {
                _circleTexture = sprite.texture;
                return _circleTexture;
            }
        }

        return null;
    }

    private static Texture2D GetFallbackDotTexture()
    {
        if (_fallbackDotTexture != null)
        {
            return _fallbackDotTexture;
        }

        _fallbackDotTexture = CreateShapeTexture(32, (x, y) =>
        {
            float dx = (x + 0.5f) / 16f - 1f;
            float dy = (y + 0.5f) / 16f - 1f;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = Mathf.Clamp01((1f - distance) * 16f);
            return new Color(1f, 1f, 1f, alpha);
        });
        return _fallbackDotTexture;
    }

    private static Texture2D GetArrowTexture()
    {
        if (_arrowTexture != null)
        {
            return _arrowTexture;
        }

        _arrowTexture = CreateShapeTexture(32, (x, y) =>
        {
            float center = 15.5f;
            float halfWidth = 2f + (y / 31f) * 13f;
            bool inside = y >= 3 && y <= 29 && Mathf.Abs(x - center) <= halfWidth;
            return inside ? Color.white : Color.clear;
        });
        return _arrowTexture;
    }

    private static Texture2D CreateShapeTexture(int size, Func<int, int, Color> pixel)
    {
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                pixels[y * size + x] = pixel(x, y);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private struct ProjectedMarker
    {
        public Marker Marker;
        public Vector2 Point;
        public float Distance;
    }
}
