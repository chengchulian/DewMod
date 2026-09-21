using System;
using System.IO;
using System.Reflection;
using DewVascularThief.config;
using UnityEngine;

namespace DewVascularThief.util;

internal static class VascularThiefSkillIcons
{
    private const string ReadyIconRelativePath = "assets/icons/vascular_thief_ready.png";
    private const string StolenIconRelativePath = "assets/icons/vascular_thief_stolen.png";

    private static Sprite _readyIcon;
    private static Sprite _stolenIcon;

    public static void Initialize(ModBehaviour modBehaviour)
    {
        string modPath = modBehaviour?.mod?.path;
        if (string.IsNullOrWhiteSpace(modPath))
        {
            Debug.LogWarning($"[{VascularThiefText.ModKey}] Icon init skipped: mod path is empty.");
            return;
        }

        _readyIcon = LoadSprite(Path.Combine(modPath, ReadyIconRelativePath), "VascularThief_ReadyIcon");
        _stolenIcon = LoadSprite(Path.Combine(modPath, StolenIconRelativePath), "VascularThief_StolenIcon");
    }

    public static void ApplyIcons(SkillTrigger skill)
    {
        ApplyIcon(skill, VascularThiefSkillMode.Steal, _readyIcon);
        ApplyIcon(skill, VascularThiefSkillMode.Stolen, _stolenIcon ?? _readyIcon);
    }

    private static void ApplyIcon(SkillTrigger skill, int configIndex, Sprite icon)
    {
        if (skill?.configs == null ||
            configIndex < 0 ||
            configIndex >= skill.configs.Length ||
            skill.configs[configIndex] == null ||
            icon == null)
        {
            return;
        }

        skill.configs[configIndex].triggerIcon = icon;
    }

    private static Sprite LoadSprite(string path, string name)
    {
        try
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[{VascularThiefText.ModKey}] Icon file does not exist: {path}");
                return null;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = name + "_Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            if (!LoadImage(texture, File.ReadAllBytes(path)))
            {
                UnityEngine.Object.Destroy(texture);
                Debug.LogWarning($"[{VascularThiefText.ModKey}] Failed to load icon image: {path}");
                return null;
            }

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[{VascularThiefText.ModKey}] Failed to load icon {path}\n{exception}");
            return null;
        }
    }

    private static bool LoadImage(Texture2D texture, byte[] bytes)
    {
        Type imageConversionType = FindImageConversionType();
        MethodInfo loadImage = imageConversionType?.GetMethod(
            "LoadImage",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) },
            null);
        if (loadImage == null)
        {
            Debug.LogWarning($"[{VascularThiefText.ModKey}] Unity image loader is unavailable.");
            return false;
        }

        return loadImage.Invoke(null, new object[] { texture, bytes, false }) is true;
    }

    private static Type FindImageConversionType()
    {
        Type type = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
        if (type != null)
        {
            return type;
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType("UnityEngine.ImageConversion");
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }
}
