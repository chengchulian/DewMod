using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;

namespace DewMoreChaosChoices.config;

public static class LocalizationSource
{
    private static readonly Dictionary<string, Dictionary<string, string>> Sources =
        new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    // 从 Mod 根目录读取 UTF-8 翻译，重复初始化时清除旧数据。
    public static void Init(ModBehaviour modBehaviour)
    {
        Sources.Clear();
        string directory = Path.Combine(modBehaviour.mod.path, "i18n");
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (string file in Directory.GetFiles(directory, "*.json"))
        {
            try
            {
                var values = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                    File.ReadAllText(file, Encoding.UTF8));
                if (values != null)
                {
                    Sources[Path.GetFileNameWithoutExtension(file)] = values;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[DewMoreChaosChoices] 翻译加载失败：{file}\n{exception}");
            }
        }
    }

    // 缺失语言或键时逐项回退到英文，最后保留原文本。
    public static string GetLocalizationText(string key)
    {
        string language = DewSave.profileMain?.language ?? "en-US";
        if (Sources.TryGetValue(language, out var localized) && localized.TryGetValue(key, out string value))
        {
            return value;
        }

        return Sources.TryGetValue("en-US", out var english) && english.TryGetValue(key, out value)
            ? value
            : key;
    }

    // 仅替换已知翻译键，保留原版数字输入内容。
    public static void LocalizeUI(Transform root)
    {
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            text.text = GetLocalizationText(text.text);
        }
    }
}
