using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;

namespace DewShieldModDetection.config;

public static class LocalizationSource
{
    private static readonly Dictionary<string, Dictionary<string, string>> Sources =
        new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    // 从 Mod 根目录读取 UTF-8 翻译，每次初始化清除上次加载的数据。
    public static void Init(ModBehaviour modBehaviour)
    {
        Sources.Clear();
        string modPath = modBehaviour?.mod?.path;
        if (string.IsNullOrWhiteSpace(modPath))
        {
            Debug.LogWarning("[DewShieldModDetection] 本地化初始化缺少 Mod 路径。");
            return;
        }

        string directory = Path.Combine(modPath, "i18n");
        if (!Directory.Exists(directory))
        {
            Debug.LogWarning($"[DewShieldModDetection] 本地化目录不存在：{directory}");
            return;
        }

        foreach (string file in Directory.GetFiles(directory, "*.json"))
        {
            try
            {
                string json = File.ReadAllText(file, Encoding.UTF8);
                Dictionary<string, string> values = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (values != null)
                {
                    Sources[Path.GetFileNameWithoutExtension(file)] = values;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[DewShieldModDetection] 本地化文件加载失败：{file}\n{exception}");
            }
        }
    }

    // 缺失语言或键时回退到英语，再回退到键本身。
    public static string GetLocalizationText(string key, params object[] args)
    {
        string language = DewSave.profileMain?.language ?? "en-US";
        string value;
        if (!TryGetValue(language, key, out value) && !TryGetValue("en-US", key, out value))
        {
            return key;
        }

        if (args == null || args.Length == 0)
        {
            return value;
        }

        try
        {
            return string.Format(value, args);
        }
        catch (FormatException)
        {
            Debug.LogWarning($"[DewShieldModDetection] 本地化格式占位符有误：{key}");
            return value;
        }
    }

    // 查询单一语言，JSON 中的空值按缺失键处理。
    private static bool TryGetValue(string language, string key, out string value)
    {
        value = null;
        return !string.IsNullOrWhiteSpace(language) && key != null &&
               Sources.TryGetValue(language, out Dictionary<string, string> source) &&
               source.TryGetValue(key, out value) && value != null;
    }

    // 配置控件生成后，将其文本键替换为当前语言。
    public static void LocalizeUI(Transform root)
    {
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            text.text = GetLocalizationText(text.text);
        }
    }
}
