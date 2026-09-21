using System.Text;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DewVascularThief.config;

internal static class LocalizationSource
{
    private const string FallbackLanguage = "en-US";

    private static readonly Dictionary<string, Dictionary<string, string>> TextByLanguage =
        new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    private static bool _initialized;

    // 从 Mod 根目录初始化技能翻译。
    public static void Init(ModBehaviour modBehaviour)
    {
        TextByLanguage.Clear();
        _initialized = true;

        string modPath = modBehaviour?.mod?.path;
        if (string.IsNullOrWhiteSpace(modPath))
        {
            Debug.LogWarning($"[{VascularThiefText.ModKey}] 跳过本地化初始化：Mod 路径为空。");
            return;
        }

        string i18nPath = Path.Combine(modPath, "i18n");
        if (!Directory.Exists(i18nPath))
        {
            Debug.LogWarning($"[{VascularThiefText.ModKey}] 本地化目录不存在： {i18nPath}");
            return;
        }

        foreach (string file in Directory.GetFiles(i18nPath, "*.json"))
        {
            LoadLanguageFile(file);
        }
    }

    // 获取文本模板，缺失时显示键。
    private static string Get(string key)
    {
        return Get(key, key);
    }

    // 依次查询当前语言与英语回退。
    private static string Get(string key, string fallback)
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

    // 使用统一入口查询并格式化技能文本。
    public static string GetLocalizationText(string key, params object[] args)
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

    // 加载单个语言文件，错误不影响其他语言。
    private static void LoadLanguageFile(string file)
    {
        try
        {
            string language = Path.GetFileNameWithoutExtension(file);
            string jsonText = File.ReadAllText(file, Encoding.UTF8);
            Dictionary<string, string> values = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonText);
            if (values == null)
            {
                Debug.LogWarning($"[{VascularThiefText.ModKey}] 本地化文件为空： {file}");
                return;
            }

            TextByLanguage[language] = values;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[{VascularThiefText.ModKey}] 本地化文件加载失败： {file}\n{exception}");
        }
    }

    // 空值视为缺失，交由上层继续回退。
    private static bool TryGetValue(string language, string key, out string value)
    {
        value = null;
        return !string.IsNullOrWhiteSpace(language) &&
               TextByLanguage.TryGetValue(language, out Dictionary<string, string> texts) &&
               texts.TryGetValue(key, out value) &&
               value != null;
    }

    // 存档尚未就绪时使用英语。
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
            Debug.LogWarning($"[{VascularThiefText.ModKey}] 读取游戏语言失败： {exception.Message}");
        }

        return FallbackLanguage;
    }
}
