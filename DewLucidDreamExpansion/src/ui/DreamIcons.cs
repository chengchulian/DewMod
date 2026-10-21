using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DewLucidDreamExpansion.ui;

internal static class DreamIcons
{
    private static readonly Dictionary<Type, Sprite> Icons = new Dictionary<Type, Sprite>();

    // 图标嵌入程序集，部署 DLL 时同步携带，避免外部素材路径遗漏。
    internal static Sprite Get(Type type)
    {
        if (Icons.TryGetValue(type, out Sprite icon)) return icon;
        string name = "DewLucidDreamExpansion.Icons." + type.Name + ".png";
        using Stream stream = typeof(DreamIcons).Assembly.GetManifestResourceStream(name);
        if (stream == null) throw new FileNotFoundException("清醒梦图标资源缺失", name);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            name = type.Name + "_Texture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        Type conversion = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule", true);
        MethodInfo loadImage = conversion.GetMethod("LoadImage", BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) }, null);
        if (!(bool)loadImage.Invoke(null, new object[] { texture, buffer.ToArray(), true }))
        {
            Object.Destroy(texture);
            throw new InvalidDataException("清醒梦图标 PNG 加载失败：" + name);
        }
        icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        icon.name = type.Name + "_Icon";
        icon.hideFlags = HideFlags.HideAndDontSave;
        Icons.Add(type, icon);
        return icon;
    }

    internal static void Clear()
    {
        foreach (Sprite icon in Icons.Values)
        {
            Object.Destroy(icon.texture);
            Object.Destroy(icon);
        }
        Icons.Clear();
    }
}
