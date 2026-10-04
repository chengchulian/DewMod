using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;

namespace DewRoomGuidance.config;

public static class LocalizationSource
{
    private static readonly Dictionary<string, Dictionary<string, string>> LocalizationSourceMap =
        new Dictionary<string, Dictionary<string, string>>();

    public static void Init(ModBehaviour modBehaviour)
    {
        LocalizationSourceMap.Clear();
        string i18nPath = Path.Combine(modBehaviour.mod.path, "i18n");
        if (!Directory.Exists(i18nPath))
        {
            Debug.LogWarning($"[DewRoomGuidance.Localization] 找不到本地化目录：{i18nPath}");
            return;
        }

        foreach (string file in Directory.GetFiles(i18nPath, "*.json"))
        {
            try
            {
                string language = Path.GetFileNameWithoutExtension(file);
                string json = File.ReadAllText(file, Encoding.UTF8);
                Dictionary<string, string> languageMap =
                    Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (languageMap != null)
                {
                    LocalizationSourceMap[language] = languageMap;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[DewRoomGuidance.Localization] 读取本地化文件失败：{file}\n{exception}");
            }
        }
    }

    public static string GetLocalizationText(string key, params object[] args)
    {
        const string dewUiPrefix = "DewUI:";
        if (key.StartsWith(dewUiPrefix, StringComparison.Ordinal))
        {
            return DewLocalization.GetUIValue(key.Substring(dewUiPrefix.Length));
        }

        string language = DewSave.profileMain != null ? DewSave.profileMain.language : null;
        if (string.IsNullOrEmpty(language))
        {
            language = "en-US";
        }

        if (!LocalizationSourceMap.TryGetValue(language, out Dictionary<string, string> languageMap) ||
            !languageMap.TryGetValue(key, out string value))
        {
            if (!LocalizationSourceMap.TryGetValue("en-US", out languageMap) ||
                !languageMap.TryGetValue(key, out value))
            {
                return key;
            }
        }

        return args == null || args.Length == 0 ? value : string.Format(value, args);
    }

    public static void LocalizeUI(Transform root)
    {
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            text.text = GetLocalizationText(text.text);
        }
    }
}
