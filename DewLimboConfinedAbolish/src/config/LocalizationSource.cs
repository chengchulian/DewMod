using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;

namespace DewLimboConfinedAbolish.config;

public static class LocalizationSource
{
    private static readonly Dictionary<string, Dictionary<string, string>> Sources =
        new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    public static void Init(ModBehaviour modBehaviour)
    {
        Sources.Clear();
        string modPath = modBehaviour?.mod?.path;
        if (string.IsNullOrWhiteSpace(modPath))
        {
            Debug.LogWarning("[DewLimboConfinedAbolish] 缺少 Mod 路径，配置文本将显示本地化键。");
            return;
        }

        string directory = Path.Combine(modPath, "i18n");
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (string file in Directory.GetFiles(directory, "*.json"))
        {
            try
            {
                Dictionary<string, string> values = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                    File.ReadAllText(file, Encoding.UTF8));
                if (values != null)
                {
                    Sources[Path.GetFileNameWithoutExtension(file)] = values;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[DewLimboConfinedAbolish] 本地化文件加载失败：{file}\n{exception}");
            }
        }
    }

    public static string GetLocalizationText(string key)
    {
        string language = DewSave.profileMain?.language ?? "en-US";
        if (Sources.TryGetValue(language, out Dictionary<string, string> localized) && localized.TryGetValue(key, out string value))
        {
            return value;
        }

        if (Sources.TryGetValue("en-US", out Dictionary<string, string> english) && english.TryGetValue(key, out value))
        {
            return value;
        }

        return key;
    }

    public static void LocalizeUI(Transform root)
    {
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            text.text = GetLocalizationText(text.text);
        }
    }
}
