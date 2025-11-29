using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

namespace DewGemSlotCount.config
{
    public static class LocalizationSource
    {
        private static Dictionary<string, Dictionary<string, string>> LocalizationSourceMap
            = new Dictionary<string, Dictionary<string, string>>();

        private static string GetLanguage()
        {
            return DewSave.profileMain.language;
        }

        /// <summary>
        /// 初始化：从 MOD 文件夹加载 i18n JSON 文件
        /// </summary>
        public static void Init(ModBehaviour modBehaviour)
        {
            string modPath = modBehaviour.mod.path;

            string i18nPath = Path.Combine(modPath, "i18n");

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
                    // 解析JSON文本为字典
                    Dictionary<string, string> langDict =
                        Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonText);

                    // 将语言字典添加到本地化源映射中
                    LocalizationSourceMap[lang] = langDict;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Localization] Failed to load {file}\n{e}");
                }
            }
        }

        /// <summary>
        /// 查询翻译
        /// </summary>
        public static string GetLocalizationText(string key, params object[] args)
        {
            var lang = GetLanguage();
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
                ReplaceText(text);
            }
        }

        private static void ReplaceText(TMP_Text text)
        {
            string key = text.text;
            text.text = GetLocalizationText(key);
        }
    }
}