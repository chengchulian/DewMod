using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

namespace DewIdentityChange.config;

public static class LocalizationSource
{
    private static readonly Dictionary<string, Dictionary<string, string>> LocalizationSourceMap = new();

    public static void Init(ModBehaviour modBehaviour)
    {
        string i18nPath = Path.Combine(modBehaviour.mod.path, "i18n");

        if (!Directory.Exists(i18nPath))
        {
            Debug.LogWarning($"[Localization] i18n folder does not exist: {i18nPath}");
            return;
        }

        var files = Directory.GetFiles(i18nPath, "*.json");
        foreach (var file in files)
        {
            try
            {
                string lang = Path.GetFileNameWithoutExtension(file);
                string jsonText = File.ReadAllText(file);
                var langDict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonText);
                LocalizationSourceMap[lang] = langDict;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Localization] Failed to load {file}\n{e}");
            }
        }
    }

    public static string GetLocalizationText(string key, params object[] args)
    {
        var lang = DewSave.profileMain.language;
        if (!LocalizationSourceMap.ContainsKey(lang))
        {
            lang = "en-US";
        }

        if (!LocalizationSourceMap.TryGetValue(lang, out var dict))
        {
            return key;
        }

        if (!dict.TryGetValue(key, out var val))
        {
            return key;
        }

        return string.Format(val, args);
    }

    public static void LocalizeUI(Transform root)
    {
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            text.text = GetLocalizationText(text.text);
        }
    }
}
