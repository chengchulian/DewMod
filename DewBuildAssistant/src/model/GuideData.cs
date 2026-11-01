using System.Collections.Generic;
using System.Linq;

namespace DewBuildAssistant.model;

internal enum WorkshopFilter { Mine, Public, All }

// 工坊列表与详情使用独立模型，配装和推荐按资源 key 传递。
internal sealed class GuideSummary
{
    public string Page;
    public string Title;
    public string Hero;
    public string Author;
    public ulong OwnerSteamId;
    public bool IsPublic;
    public bool RequiresIdentityChange;
    public bool IsOwned;
    public int LocalUploadSlot = -1;
}

internal sealed class GuideItem
{
    public string Name;
    public string ResourceHint;
    public override string ToString() => Name;
}

// 技能与精粹各自保存核心标记；空精粹槽以 null 保留位置。
internal sealed class BuildGem
{
    public GuideItem Gem;
    public bool IsCore;
}

internal sealed class SkillBuild
{
    public string Slot;
    public GuideItem Skill;
    public bool IsCore;
    public List<BuildGem> GemSlots = new() { null, null, null, null };
}

internal sealed class BuildRecommendations
{
    public static readonly string[] SlotKeys = { "Q", "W", "E", "R", "D" };
    public List<SkillBuild> Skills = SlotKeys.Select(slot => new SkillBuild { Slot = slot }).ToList();
    public SkillBuild Get(string slot) => Skills.Single(s => s.Slot == slot);
}

internal sealed class GuideData
{
    public string Page;
    public string Title;
    public string Hero;
    public string Author;
    public string Summary;
    public string Details;
    public bool ClearDejavu;
    public bool RequiresIdentityChange;
    public BuildRecommendations Recommendations = new();
    public readonly Dictionary<string, List<GuideItem>> Items = new();
    public readonly HashSet<string> PresentFields = new();

    public List<GuideItem> GetItems(string field) =>
        Items.TryGetValue(field, out var items) ? items : new List<GuideItem>();
}
