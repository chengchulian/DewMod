using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;

namespace DewLanMode.config;

public static class LocalizationSource
{
    private static readonly Dictionary<string, Dictionary<string, string>> Sources =
        new Dictionary<string, Dictionary<string, string>>();

    public static void Init(ModBehaviour modBehaviour)
    {
        Sources.Clear();
        string i18nPath = Path.Combine(modBehaviour.mod.path, "i18n");
        if (!Directory.Exists(i18nPath))
        {
            Debug.LogWarning($"[DewLanMode] i18n folder does not exist: {i18nPath}");
            return;
        }

        foreach (string file in Directory.GetFiles(i18nPath, "*.json"))
        {
            try
            {
                string language = Path.GetFileNameWithoutExtension(file);
                string json = File.ReadAllText(file, Encoding.UTF8);
                Sources[language] = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                                    ?? new Dictionary<string, string>();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[DewLanMode] Failed to load localization file {file}\n{exception}");
            }
        }
    }

    // 查询当前语言的文本。
    public static string GetLocalizationText(string key, params object[] args)
    {
        string language = DewSave.profileMain?.language ?? "en-US";
        if (!Sources.TryGetValue(language, out Dictionary<string, string> source) &&
            !Sources.TryGetValue("en-US", out source))
        {
            return key;
        }

        return source.TryGetValue(key, out string value) ? string.Format(value, args) : key;
    }

    // 保留旧版公开方法，兼容引用此类的已有代码。
    public static string Get(string key, params object[] args)
    {
        return GetLocalizationText(key, args);
    }

    public static void LocalizeUI(Transform root)
    {
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            text.text = GetLocalizationText(text.text);
        }
    }
}
