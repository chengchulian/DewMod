using System;
using System.Collections.Generic;
using System.IO;
using DewVascularThief.config;
using Newtonsoft.Json;
using UnityEngine;

namespace DewVascularThief.localization;

internal static class VascularThiefI18n
{
    private const string FallbackLanguage = "en-US";

    private static readonly Dictionary<string, Dictionary<string, string>> TextByLanguage =
        new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    private static bool _initialized;

    public static void Initialize(ModBehaviour modBehaviour)
    {
        TextByLanguage.Clear();
        _initialized = true;

        string modPath = modBehaviour?.mod?.path;
        if (string.IsNullOrWhiteSpace(modPath))
        {
            Debug.LogWarning($"[{VascularThiefText.ModKey}] i18n init skipped: mod path is empty.");
            return;
        }

        string i18nPath = Path.Combine(modPath, "i18n");
        if (!Directory.Exists(i18nPath))
        {
            Debug.LogWarning($"[{VascularThiefText.ModKey}] i18n folder does not exist: {i18nPath}");
            return;
        }

        foreach (string file in Directory.GetFiles(i18nPath, "*.json"))
        {
            LoadLanguageFile(file);
        }
    }

    public static string Get(string key)
    {
        return Get(key, key);
    }

    public static string Get(string key, string fallback)
    {
        if (!_initialized)
        {
            return fallback ?? key;
        }

        if (TryGetValue(GetCurrentLanguage(), key, out string value) ||
            TryGetValue(FallbackLanguage, key, out value))
        {
            return value;
        }

        return fallback ?? key;
    }

    public static string Format(string key, params object[] args)
    {
        string template = Get(key);
        try
        {
            return string.Format(template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    private static void LoadLanguageFile(string file)
    {
        try
        {
            string language = Path.GetFileNameWithoutExtension(file);
            string jsonText = File.ReadAllText(file);
            Dictionary<string, string> values = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonText);
            if (values == null)
            {
                Debug.LogWarning($"[{VascularThiefText.ModKey}] i18n file is empty: {file}");
                return;
            }

            TextByLanguage[language] = values;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[{VascularThiefText.ModKey}] Failed to load i18n file {file}\n{exception}");
        }
    }

    private static bool TryGetValue(string language, string key, out string value)
    {
        value = null;
        return !string.IsNullOrWhiteSpace(language) &&
               TextByLanguage.TryGetValue(language, out Dictionary<string, string> texts) &&
               texts.TryGetValue(key, out value) &&
               value != null;
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
            Debug.LogWarning($"[{VascularThiefText.ModKey}] Failed to read game language: {exception.Message}");
        }

        return FallbackLanguage;
    }
}
