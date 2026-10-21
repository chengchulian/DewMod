using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace DewLucidDreamExpansion.config;

internal static class LocalizationSource
{
    private static readonly Dictionary<string, Dictionary<string, string>> Sources =
        new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    // 显式使用 UTF-8，按当前语言查询并回退英文。
    public static void Init(ModBehaviour mod)
    {
        Sources.Clear();
        foreach (string file in Directory.GetFiles(Path.Combine(mod.mod.path, "i18n"), "*.json"))
        {
            try
            {
                Sources[Path.GetFileNameWithoutExtension(file)] =
                    JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(file, Encoding.UTF8));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[LucidDreamExpansion] 本地化加载失败：{file}\n{exception}");
            }
        }
    }

    public static bool TryGet(string key, out string value)
    {
        string language = DewSave.profileMain?.language ?? "en-US";
        if (Sources.TryGetValue(language, out var source) && source.TryGetValue(key, out value)) return true;
        if (Sources.TryGetValue("en-US", out source) && source.TryGetValue(key, out value)) return true;
        value = null;
        return false;
    }

    public static string GetLocalizationText(string key) => TryGet(key, out string text) ? text : key;
}
