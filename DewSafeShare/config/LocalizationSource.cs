using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

namespace DewSafeShare.config
{
    public static class LocalizationSource
    {
        private static readonly Dictionary<string, Dictionary<string, string>> LocalizationSourceMap =
            new Dictionary<string, Dictionary<string, string>>();

        private static string GetLanguage()
        {
            return DewSave.profileMain.language;
        }

        public static void Init(ModBehaviour modBehaviour)
        {
            string i18nPath = Path.Combine(modBehaviour.mod.path, "i18n");
            if (!Directory.Exists(i18nPath))
            {
                Debug.LogWarning($"[Localization] i18n folder does not exist: {i18nPath}");
                return;
            }

            foreach (string file in Directory.GetFiles(i18nPath, "*.json"))
            {
                try
                {
                    string lang = Path.GetFileNameWithoutExtension(file);
                    string jsonText = File.ReadAllText(file);
                    Dictionary<string, string> langDict =
                        Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonText);

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
            string lang = GetLanguage();
            if (!LocalizationSourceMap.ContainsKey(lang))
            {
                lang = "en-US";
            }

            if (!LocalizationSourceMap.TryGetValue(lang, out Dictionary<string, string> dict))
            {
                return key;
            }

            if (!dict.TryGetValue(key, out string val))
            {
                return key;
            }

            return string.Format(val, args);
        }

        public static void LocalizeUI(Transform root)
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.text = GetLocalizationText(text.text);
            }
        }
    }
}
