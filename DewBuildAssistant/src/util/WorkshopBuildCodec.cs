using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DewBuildAssistant.model;
using DewBuildAssistant.config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DewBuildAssistant.util;

// 当前格式仅保存资源 key、位置与核心标记，排版和本地目标栏位不进入工坊 JSON。
internal static class WorkshopBuildCodec
{
    public const string Tag = "build";
    public const string Format = "BD-Assistant-Build-v1";
    public const int Version = 5;
    public const string IdentityChangeTag = "identitychange";
    public const string FileName = "build.json";
    public const int MaxBytes = 128 * 1024;
    private static readonly Dictionary<string, string> Fields = new()
    {
        ["memoryQ"] = "旅行者记忆1", ["memoryR"] = "旅行者记忆2", ["identity"] = "旅行者身份记忆", ["dejavu"] = "既视感",
        ["destruction"] = "破坏星座", ["life"] = "生命星座", ["imagination"] = "幻想星座", ["flexible"] = "灵活星座"
    };

    internal sealed class Envelope
    {
        public string Format;
        public int Version;
        public BuildDocument Build;
    }

    internal sealed class BuildDocument
    {
        public string HeroKey;
        public string Title;
        public string Author;
        public string Description;
        public Dictionary<string, List<string>> Items;
        public bool ClearDejavu;
        public bool RequiresIdentityChange;
        public List<SkillDocument> Skills;
    }

    internal sealed class SkillDocument
    {
        public string Slot;
        public string SkillKey;
        public bool IsCore;
        public List<GemDocument> GemSlots;
    }

    internal sealed class GemDocument
    {
        public string GemKey;
        public bool IsCore;
    }

    public static byte[] Encode(GuideData guide)
    {
        Validate(guide);
        var document = new BuildDocument
        {
            HeroKey = guide.Hero, Title = guide.Title, Author = guide.Author, Description = guide.Summary,
            ClearDejavu = guide.ClearDejavu, RequiresIdentityChange = guide.RequiresIdentityChange,
            Items = new Dictionary<string, List<string>>(),
            Skills = BuildRecommendations.SlotKeys.Select(slot => guide.Recommendations.Get(slot)).Select(s => new SkillDocument
            {
                Slot = s.Slot, SkillKey = s.Skill?.ResourceHint, IsCore = s.IsCore,
                GemSlots = s.GemSlots.Select(g => g == null ? null : new GemDocument { GemKey = g.Gem.ResourceHint, IsCore = g.IsCore }).ToList()
            }).ToList()
        };
        foreach (var field in Fields)
            if (guide.PresentFields.Contains(field.Value))
                document.Items[field.Key] = guide.GetItems(field.Value).Select(i => i.ResourceHint).ToList();
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(JsonConvert.SerializeObject(new Envelope { Format = Format, Version = Version, Build = document }, Formatting.Indented));
        if (bytes.Length > MaxBytes) throw new FormatException("Build exceeds 128 KiB.");
        return bytes;
    }

    public static GuideData Decode(byte[] bytes)
    {
        if (bytes == null || bytes.Length > MaxBytes) throw new FormatException("Invalid Build size.");
        var data = JsonConvert.DeserializeObject<JObject>(new UTF8Encoding(false, true).GetString(bytes),
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 16 });
        if (data == null || data["Format"]?.Type != JTokenType.String || (string)data["Format"] != Format ||
            data["Version"]?.Type != JTokenType.Integer || (long)data["Version"] != Version)
            throw new FormatException("Unsupported Build format/version; expected Version " + Version + ".");
        if (data["Build"] is not JObject payload || payload["HeroKey"]?.Type != JTokenType.String ||
            payload["Title"]?.Type != JTokenType.String || payload["Items"] is not JObject wireItems ||
            payload["ClearDejavu"]?.Type != JTokenType.Boolean || payload["RequiresIdentityChange"]?.Type != JTokenType.Boolean ||
            payload["Skills"] is not JArray skills || skills.Count != 5 || wireItems.Properties().Any(p => !IsKeyArray(p.Value)))
            throw new FormatException("Invalid current Build structure.");
        foreach (var token in skills)
        {
            if (token is not JObject skill || skill["Slot"]?.Type != JTokenType.String ||
                skill["SkillKey"] == null || (skill["SkillKey"].Type != JTokenType.String && skill["SkillKey"].Type != JTokenType.Null) ||
                skill["IsCore"]?.Type != JTokenType.Boolean || skill["GemSlots"] is not JArray gems || gems.Count != 4)
                throw new FormatException("Invalid QWERD skill structure.");
            foreach (var gem in gems)
                if (gem.Type != JTokenType.Null && (gem is not JObject value || value["GemKey"]?.Type != JTokenType.String || value["IsCore"]?.Type != JTokenType.Boolean))
                    throw new FormatException("Invalid gem structure.");
        }
        var build = payload.ToObject<BuildDocument>();
        if (build.Items.Count > Fields.Count) throw new FormatException("Invalid Build items.");
        var guide = new GuideData { Hero = build.HeroKey, Title = build.Title, Author = build.Author,
            Summary = build.Description, ClearDejavu = build.ClearDejavu, RequiresIdentityChange = build.RequiresIdentityChange };
        foreach (var pair in build.Items)
        {
            if (!Fields.TryGetValue(pair.Key, out string field) || pair.Value == null || pair.Value.Count > 64)
                throw new FormatException("Invalid Build field.");
            guide.PresentFields.Add(field);
            guide.Items[field] = pair.Value.Select(Item).ToList();
        }
        guide.Recommendations.Skills = build.Skills.Select(s => new SkillBuild
        {
            Slot = s.Slot, Skill = s.SkillKey == null ? null : Item(s.SkillKey), IsCore = s.IsCore,
            GemSlots = s.GemSlots.Select(g => g == null ? null : new BuildGem { Gem = Item(g.GemKey), IsCore = g.IsCore }).ToList()
        }).ToList();
        Validate(guide);
        guide.Recommendations.Skills = BuildRecommendations.SlotKeys.Select(slot => guide.Recommendations.Get(slot)).ToList();
        guide.Details = Describe(guide);
        return guide;
    }

    private static GuideItem Item(string key) => new() { Name = key, ResourceHint = key };

    private static void Validate(GuideData guide)
    {
        if (guide == null || !ValidKey(guide.Hero, "Hero_") || string.IsNullOrWhiteSpace(guide.Title) ||
            guide.Hero.Length > 128 || guide.Title.Length > 120 || (guide.Summary?.Length ?? 0) > 8000 ||
            (guide.Author?.Length ?? 0) > 256 || guide.Items.Count > Fields.Count || guide.PresentFields.Count > Fields.Count)
            throw new FormatException("Invalid Build metadata.");
        foreach (string field in guide.PresentFields)
            if (!Fields.ContainsValue(field)) throw new FormatException("Unknown Build field.");
        foreach (var pair in guide.Items)
        {
            if (!Fields.ContainsValue(pair.Key) || !guide.PresentFields.Contains(pair.Key) || pair.Value == null || pair.Value.Count > 64)
                throw new FormatException("Invalid Build items.");
            foreach (var item in pair.Value)
                if (item == null || !(pair.Key == "既视感"
                        ? ValidKey(item.ResourceHint, "Gem_") || ValidKey(item.ResourceHint, "St_")
                        : ValidKey(item.ResourceHint, pair.Key.StartsWith("旅行者", StringComparison.Ordinal) ? "St_" : "Se_")))
                    throw new FormatException("Invalid Build resource.");
        }
        var skills = guide.Recommendations?.Skills;
        if (skills == null || skills.Count != 5 || skills.Any(s => s == null || Array.IndexOf(BuildRecommendations.SlotKeys, s.Slot) < 0) ||
            skills.Select(s => s.Slot).Distinct().Count() != 5)
            throw new FormatException("Build requires exactly Q, W, E, R and D skill positions.");
        var usedGems = new HashSet<string>(StringComparer.Ordinal);
        foreach (var skill in skills)
        {
            if (skill.GemSlots == null || skill.GemSlots.Count != 4)
                throw new FormatException("Each skill requires four gem positions.");
            if (skill.Skill == null)
            {
                if (skill.IsCore) throw new FormatException("Empty skills must have no skill core marker.");
            }
            else if (!ValidKey(skill.Skill.ResourceHint, "St_")) throw new FormatException("Invalid skill resource key.");
            var gems = skill.GemSlots.Where(g => g != null).ToList();
            if (gems.Any(g => g.Gem == null || !ValidKey(g.Gem.ResourceHint, "Gem_")) || gems.Select(g => g.Gem.ResourceHint).Distinct().Count() != gems.Count)
                throw new FormatException("Each skill supports up to four distinct gems.");
            if (gems.Any(g => !usedGems.Add(g.Gem.ResourceHint)))
                throw new FormatException("Each gem may appear only once across QWERD.");
        }
    }

    private static bool ValidKey(string key, string prefix) => key != null && key.Length > prefix.Length && key.Length <= 256 &&
        key.StartsWith(prefix, StringComparison.Ordinal) && key.All(c => char.IsLetterOrDigit(c) || c == '_');
    private static bool IsKeyArray(JToken value) => value is JArray array && array.All(k => k.Type == JTokenType.String);

    public static string FieldLabel(string field) => LocalizationSource.GetLocalizationText(field switch
    {
        "旅行者记忆1" => "MemoryQ", "旅行者记忆2" => "MemoryR", "旅行者身份记忆" => "IdentityMemory", "既视感" => "Dejavu",
        "破坏星座" => "DestructionStars", "生命星座" => "LifeStars", "幻想星座" => "ImaginationStars", "灵活星座" => "FlexibleStars",
        _ => field
    });

    // 描述按当前语言生成，技能与每个精粹分别显示其核心状态。
    public static string Describe(GuideData guide)
    {
        var text = new StringBuilder().AppendLine(guide.Title).AppendLine(guide.Author).AppendLine().AppendLine(guide.Summary);
        if (guide.RequiresIdentityChange) text.AppendLine().AppendLine(LocalizationSource.GetLocalizationText("IdentityChangeRequired"));
        foreach (string field in Fields.Values)
            if (guide.PresentFields.Contains(field))
                text.AppendLine().Append(FieldLabel(field)).Append(": ").AppendLine(string.Join("、", guide.GetItems(field).Select(i => i.Name)));
        text.AppendLine().AppendLine(LocalizationSource.GetLocalizationText("SkillBuild"));
        foreach (string slot in BuildRecommendations.SlotKeys)
        {
            var skill = guide.Recommendations.Get(slot);
            if (skill.Skill == null && skill.GemSlots.All(g => g == null)) continue;
            text.Append(slot);
            if (skill.Skill == null) text.Append(" ").AppendLine(LocalizationSource.GetLocalizationText("EmptySlot"));
            else text.Append(" [").Append(CoreLabel(skill.IsCore)).Append("] ").AppendLine(skill.Skill.Name);
            for (int i = 0; i < 4; i++)
            {
                var gem = skill.GemSlots[i];
                if (gem == null) continue;
                text.Append("  ").Append(LocalizationSource.GetLocalizationText("GemSlot", i + 1))
                    .Append(" [").Append(CoreLabel(gem.IsCore)).Append("] ").AppendLine(gem.Gem.Name);
            }
        }
        return text.ToString();
    }

    public static string CoreLabel(bool core) => LocalizationSource.GetLocalizationText(core ? "CoreTag" : "NonCoreTag");
}
