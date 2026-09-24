using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace DewUnLock.config;

/// <summary>加载模组 i18n 文件并提供语言回退。</summary>
public static class LocalizationSource
{
    private static readonly Dictionary<string, Dictionary<string, string>> Languages = new();

    public static void Init(ModBehaviour mod)
    {
        var path = Path.Combine(mod.mod.path, "i18n");
        if (!Directory.Exists(path)) return;
        foreach (var file in Directory.GetFiles(path, "*.json"))
        {
            try
            {
                var language = Path.GetFileNameWithoutExtension(file);
                var json = File.ReadAllText(file, new UTF8Encoding(false));
                Languages[language] = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{mod.mod.metadata.id}] 本地化加载失败: {file}\n{exception}");
            }
        }
    }

    public static string Get(string key)
    {
        var language = DewSave.profileMain?.language;
        if (!Languages.TryGetValue(language ?? string.Empty, out var values) && !Languages.TryGetValue("en-US", out values)) return key;
        return values.TryGetValue(key, out var value) ? value : key;
    }
}
