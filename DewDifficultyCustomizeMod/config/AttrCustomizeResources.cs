using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DewDifficultyCustomizeMod.config;

public static class AttrCustomizeResources
{

    private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AttrCustomizeConfig.json");

    
    private static volatile AttrCustomizeConfig _config;
    private static readonly object Lock = new();

    public static AttrCustomizeConfig Config
    {
        get
        {
            if (_config != null)
                lock (Lock)
                {
                    return _config;
                }

            lock (Lock)
            {
                if (_config != null) return _config;

                try
                {
                    if (!File.Exists(FilePath))
                    {
                        _config = AttrCustomizeConfig.DefaultConfig;
                        SaveConfig();
                        return _config;
                    }

                    string fileContent = File.ReadAllText(FilePath);
                    string baseJson = JsonConvert.SerializeObject(AttrCustomizeConfig.DefaultConfig, Formatting.Indented);
                    string mergedJson = MergeJson(baseJson, fileContent);

                    _config = JsonConvert.DeserializeObject<AttrCustomizeConfig>(mergedJson) ?? AttrCustomizeConfig.DefaultConfig;
                }
                catch
                {
                    _config = AttrCustomizeConfig.DefaultConfig;
                }

                SaveConfig();
                return _config;
            }
        }
    }

    public static void SaveConfig()
    {
        lock (Lock)
        {
            string json = JsonConvert.SerializeObject(_config, Formatting.Indented);
            File.WriteAllText(FilePath, json);
        }
    }

    public static void ResetConfig()
    {
        lock (Lock)
        {
            _config.Reset();
            SaveConfig();
        }
    }


    private static string MergeJson(string baseJson, string overrideJson)
    {
        JObject baseObj = JObject.Parse(baseJson);
        JObject overrideObj = JObject.Parse(overrideJson);

        baseObj.Merge(overrideObj, new JsonMergeSettings
        {
            MergeArrayHandling = MergeArrayHandling.Replace
        });

        return baseObj.ToString();
    }
}