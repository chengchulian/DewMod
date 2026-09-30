using System;
using System.Collections.Generic;
using DewRoomGuidance.config;
using UnityEngine;

namespace DewRoomGuidance;

internal sealed class RoomGuidanceOverlay : MonoBehaviour
{
    private const float ScanInterval = 0.3f;
    private const float IconSize = 44f;
    private const float ScreenPadding = 56f;
    private const int MaxMarkers = 16;

    private readonly List<Marker> _markers = new();
    private float _nextScanTime;
    private GUIStyle _labelStyle;

    private struct Marker
    {
        public Actor Actor;
        public Sprite Icon;
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
        if (actorManager == null || hero == null)
        {
            return;
        }

        foreach (Actor actor in actorManager.allActors)
        {
            if (actor == null || !actor.isActiveAndEnabled)
            {
                continue;
            }

            if (actor is Shrine shrine && shrine.isAvailable && (shrine.GetType().Name == "Shrine_LoopCat" || !shrine.isLocked) && shrine.CanInteract(hero))
            {
                bool isBlackCat = shrine.GetType().Name == "Shrine_LoopCat";
                _markers.Add(new Marker
                {
                    Actor = shrine,
                    Icon = FindSprite(shrine),
                    Kind = LocalizationSource.GetLocalizationText(isBlackCat ? "Target.BlackCat" : "Target.Shrine")
                });
            }
            else if (actor is Gem gem && gem.owner == null && gem.handOwner == null && gem.tempOwner == null && gem.CanInteract(hero))
            {
                _markers.Add(new Marker { Actor = gem, Icon = gem.icon, Kind = LocalizationSource.GetLocalizationText("Target.Gem") });
            }
            else if (actor is SkillTrigger skill && skill.owner == null && skill.handOwner == null && skill.tempOwner == null && skill.CanInteract(hero))
            {
                _markers.Add(new Marker { Actor = skill, Icon = GetSkillIcon(skill), Kind = LocalizationSource.GetLocalizationText("Target.Skill") });
            }
            else if (actor is Artifact artifact && HasNoArtifact() && artifact.CanInteract(hero))
            {
                _markers.Add(new Marker { Actor = artifact, Icon = artifact.icon, Kind = LocalizationSource.GetLocalizationText("Target.Artifact") });
            }
            else if (actor is PropEntity prop && !prop.isDead && actor.GetType().Name.StartsWith("PropEnt_Stone_", StringComparison.Ordinal))
            {
                _markers.Add(new Marker { Actor = prop, Icon = FindSprite(prop), Kind = LocalizationSource.GetLocalizationText("Target.Stone") });
            }
            else if (actor is Monster monster && monster.isAlive && actor.GetType().Name.StartsWith("Mon_GoldenLizard_", StringComparison.Ordinal))
            {
                _markers.Add(new Marker { Actor = monster, Icon = FindSprite(monster), Kind = LocalizationSource.GetLocalizationText("Target.GoldenLizard") });
            }
        }
    }

    private static bool HasNoArtifact()
    {
        QuestManager questManager = NetworkedManagerBase<QuestManager>.softInstance;
        return questManager == null || questManager.currentArtifact == null;
    }

    private static Sprite FindSprite(Component component)
    {
        SpriteRenderer renderer = component.GetComponentInChildren<SpriteRenderer>();
        return renderer != null ? renderer.sprite : null;
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
        GUI.color = new Color(0.04f, 0.05f, 0.06f, 0.88f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (projected.Marker.Icon != null && projected.Marker.Icon.texture != null)
        {
            Rect iconRect = new(rect.x + 4f, rect.y + 4f, size - 8f, size - 8f);
            Rect textureRect = projected.Marker.Icon.textureRect;
            Rect uv = new(textureRect.x / projected.Marker.Icon.texture.width,
                textureRect.y / projected.Marker.Icon.texture.height,
                textureRect.width / projected.Marker.Icon.texture.width,
                textureRect.height / projected.Marker.Icon.texture.height);
            GUI.DrawTextureWithTexCoords(iconRect, projected.Marker.Icon.texture, uv, true);
        }
        else
        {
            GUI.Label(rect, projected.Marker.Kind.Substring(0, 1), _labelStyle);
        }

        Vector2 outward = (projected.Point - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)).normalized;
        Vector2 arrowCenter = projected.Point + outward * 29f;
        Matrix4x4 oldMatrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(outward.y, outward.x) * Mathf.Rad2Deg + 90f, arrowCenter);
        GUI.Label(new Rect(arrowCenter.x - 10f, arrowCenter.y - 11f, 20f, 22f), "▲", _labelStyle);
        GUI.matrix = oldMatrix;

        GUI.color = new Color(1f, 1f, 1f, 0.95f);
        float labelY = projected.Point.y > Screen.height * 0.5f ? rect.y - 21f : rect.y + size + 1f;
        GUI.Label(new Rect(rect.x - 28f, labelY, size + 56f, 20f),
            $"{projected.Marker.Kind} {projected.Distance:0}m", _labelStyle);
        GUI.color = oldColor;
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

    private struct ProjectedMarker
    {
        public Marker Marker;
        public Vector2 Point;
        public float Distance;
    }
}
