using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

namespace DewSuperSmart.config;

public static class LocalizationSource
{
    private const string FallbackLanguage = "en-US";

    private static readonly Dictionary<string, Dictionary<string, string>> TextByLanguage =
        new Dictionary<string, Dictionary<string, string>>();

    public static void Init(ModBehaviour modBehaviour)
    {
        TextByLanguage.Clear();

        string i18nPath = Path.Combine(modBehaviour.mod.path, "i18n");
        if (!Directory.Exists(i18nPath))
        {
            Debug.LogWarning($"[DewSuperSmart.Localization] i18n folder does not exist: {i18nPath}");
            return;
        }

        foreach (string file in Directory.GetFiles(i18nPath, "*.json"))
        {
            LoadLanguageFile(file);
        }
    }

    public static string GetLocalizationText(string key, params object[] args)
    {
        if (TryGetValue(GetCurrentLanguage(), key, out string value) ||
            TryGetValue(FallbackLanguage, key, out value))
        {
            return args.Length > 0 ? string.Format(value, args) : value;
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

    private static void LoadLanguageFile(string file)
    {
        try
        {
            string language = Path.GetFileNameWithoutExtension(file);
            string json = File.ReadAllText(file);
            Dictionary<string, string> values =
                Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

            if (values == null)
            {
                Debug.LogWarning($"[DewSuperSmart.Localization] i18n file is empty: {file}");
                return;
            }

            TextByLanguage[language] = values;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[DewSuperSmart.Localization] Failed to load i18n file {file}\n{exception}");
        }
    }

    private static bool TryGetValue(string language, string key, out string value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(language) ||
            !TextByLanguage.TryGetValue(language, out Dictionary<string, string> texts))
        {
            return false;
        }

        return texts.TryGetValue(key, out value);
    }

    private static string GetCurrentLanguage()
    {
        try
        {
            string language = DewSave.profileMain?.language;
            if (!string.IsNullOrWhiteSpace(language))
            {
                return language;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[DewSuperSmart.Localization] Failed to read game language: {exception.Message}");
        }

        return FallbackLanguage;
    }
}
