using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

namespace GoldenBurstAutoTarget.config;

public static class LocalizationSource
{
    private static readonly Dictionary<string, Dictionary<string, string>> LocalizationSourceMap = new Dictionary<string, Dictionary<string, string>>();

    public static void Init(ModBehaviour modBehaviour)
    {
        string i18nPath = Path.Combine(modBehaviour.mod.path, "i18n");
        if (!Directory.Exists(i18nPath))
        {
            Debug.LogWarning($"[GoldenBurstAutoTarget.Localization] i18n folder does not exist: {i18nPath}");
            return;
        }

        foreach (string file in Directory.GetFiles(i18nPath, "*.json"))
        {
            try
            {
                string language = Path.GetFileNameWithoutExtension(file);
                string json = File.ReadAllText(file);
                Dictionary<string, string> languageMap =
                    Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

                LocalizationSourceMap[language] = languageMap;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[GoldenBurstAutoTarget.Localization] Failed to load {file}\n{exception}");
            }
        }
    }

    public static string GetLocalizationText(string key, params object[] args)
    {
        string language = DewSave.profileMain.language;
        if (!LocalizationSourceMap.ContainsKey(language))
        {
            language = "en-US";
        }

        if (!LocalizationSourceMap.TryGetValue(language, out Dictionary<string, string> dictionary) ||
            !dictionary.TryGetValue(key, out string value))
        {
            return key;
        }

        return string.Format(value, args);
    }

    public static void LocalizeUI(Transform root)
    {
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            text.text = GetLocalizationText(text.text);
        }
    }
}
