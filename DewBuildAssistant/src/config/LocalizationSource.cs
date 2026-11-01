using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using DewBuildAssistant.ui;
using TMPro;
using UnityEngine;

namespace DewBuildAssistant.config;

internal static class LocalizationSource
{
    private static readonly Dictionary<string, Dictionary<string, string>> Languages = new();

    // 翻译文件以 UTF-8 读取；缺少语言/键时回退英文。
    public static void Init(ModBehaviour mod)
    {
        Languages.Clear();
        string directory = Path.Combine(mod.mod.path, "i18n");
        if (!Directory.Exists(directory)) return;
        foreach (string file in Directory.GetFiles(directory, "*.json"))
        {
            try
            {
                var values = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(file, Encoding.UTF8));
                if (values != null) Languages[Path.GetFileNameWithoutExtension(file)] = values;
            }
            catch (Exception exception) { Debug.LogError("[BD 助手] 翻译文件读取失败：" + exception); }
        }
    }

    public static string GetLocalizationText(string key, params object[] args)
    {
        string language = DewSave.profileMain?.language ?? "zh-CN";
        if (!Languages.TryGetValue(language, out var values) || !values.TryGetValue(key, out var value))
        {
            if (!Languages.TryGetValue("en-US", out values) || !values.TryGetValue(key, out value)) value = key;
        }
        return args.Length == 0 ? value : string.Format(value, args);
    }

    public static void LocalizeUI(Transform root)
    {
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            string key = text.text;
            if (!Languages.Values.Any(values => values.ContainsKey(key))) continue;
            LocalizedTextBinding.Bind(text, () => GetLocalizationText(key));
        }
    }
}
