using System.Collections.Generic;
using System.Linq;
using DewBuildAssistant.model;

namespace DewBuildAssistant.util;

internal static class WorkshopGuideFilter
{
    // 两路查询合并时优先后返回的个人作品信息，避免重复展示或沿用过时的公开状态。
    public static List<GuideSummary> Select(IEnumerable<GuideSummary> guides, WorkshopFilter filter, ulong owner)
    {
        return guides.GroupBy(g => g.Page).Select(g => g.Last()).Where(g => filter switch
        {
            WorkshopFilter.Mine => g.OwnerSteamId == owner,
            WorkshopFilter.Public => g.IsPublic,
            _ => g.IsPublic || g.OwnerSteamId == owner
        }).ToList();
    }
}
