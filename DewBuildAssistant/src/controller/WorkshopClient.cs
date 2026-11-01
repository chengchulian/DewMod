using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DewBuildAssistant.config;
using DewBuildAssistant.model;
using DewBuildAssistant.util;
using Steamworks;
using UnityEngine;

namespace DewBuildAssistant.controller;

// Steam 回调由游戏自身泵送；本类持有并释放 CallResult，不创建第二个 Steam 初始化器。
internal sealed class WorkshopClient : IDisposable
{
    private readonly List<IDisposable> _pending = new();
    private readonly HashSet<UGCQueryHandle_t> _queries = new();
    private static string Root => Path.Combine(Application.persistentDataPath, "BD-Assistant", SteamUser.GetSteamID().m_SteamID.ToString());

    // 重新上传时从上次本地上传内容恢复推荐，栏位仍只用于本地关联。
    public BuildRecommendations LoadRecommendations(string hero, int slot)
    {
        string path = Path.Combine(Root, hero + "-" + slot, "content", WorkshopBuildCodec.FileName);
        if (!File.Exists(path)) return new BuildRecommendations();
        if (new FileInfo(path).Length > WorkshopBuildCodec.MaxBytes) throw new FormatException("Oversized local Build.");
        var guide = WorkshopBuildCodec.Decode(File.ReadAllBytes(path));
        BuildExporter.LocalizeForDisplay(guide);
        return guide.Recommendations;
    }

    public static void RequireSteam()
    {
        if (!DewSteam.isInitialized) throw new InvalidOperationException("Steam Workshop is not initialized.");
    }

    // 公开作品查询与当前账号发布列表分开读取，后者包含自己的私密草稿。
    public IEnumerator LoadList(WorkshopFilter filter, Action<List<GuideSummary>, string> complete)
    {
        var all = new List<GuideSummary>();
        string error = null;
        if (filter != WorkshopFilter.Mine)
        {
            yield return LoadScope(false, all, e => error = e);
            if (error != null) { complete(null, error); yield break; }
        }
        if (filter != WorkshopFilter.Public)
        {
            yield return LoadScope(true, all, e => error = e);
            if (error != null) { complete(null, error); yield break; }
        }
        var selected = WorkshopGuideFilter.Select(all, filter, SteamUser.GetSteamID().m_SteamID);
        UpdateLocalPages(selected);
        complete(selected, null);
    }

    public void UpdateLocalPages(IEnumerable<GuideSummary> guides) => WorkshopLocalPages.Annotate(guides, Root, SteamUser.GetSteamID().m_SteamID);

    // 两类查询都只读取 build 标签，并使用各自返回的总数顺序翻页。
    private IEnumerator LoadScope(bool mine, List<GuideSummary> all, Action<string> failed)
    {
        uint received = 0;
        for (uint page = 1; ; page++)
        {
            var query = mine
                ? SteamUGC.CreateQueryUserUGCRequest(SteamUser.GetSteamID().GetAccountID(), EUserUGCList.k_EUserUGCList_Published,
                    EUGCMatchingUGCType.k_EUGCMatchingUGCType_Items, EUserUGCListSortOrder.k_EUserUGCListSortOrder_CreationOrderDesc,
                    SteamUtils.GetAppID(), SteamUtils.GetAppID(), page)
                : SteamUGC.CreateQueryAllUGCRequest(EUGCQuery.k_EUGCQuery_RankedByPublicationDate,
                EUGCMatchingUGCType.k_EUGCMatchingUGCType_Items, SteamUtils.GetAppID(), SteamUtils.GetAppID(), page);
            _queries.Add(query);
            if (!SteamUGC.AddRequiredTag(query, WorkshopBuildCodec.Tag))
            { Release(query); failed("Steam rejected the build tag filter."); yield break; }
            SteamUGC.SetAllowCachedResponse(query, 30);
            SteamUGCQueryCompleted_t result = default;
            string error = null;
            yield return Wait<SteamUGCQueryCompleted_t>(SteamUGC.SendQueryUGCRequest(query), r => result = r, e => error = e);
            if (error != null || result.m_eResult != EResult.k_EResultOK)
            { Release(query); failed(error ?? result.m_eResult.ToString()); yield break; }
            for (uint i = 0; i < result.m_unNumResultsReturned; i++)
            {
                if (!SteamUGC.GetQueryUGCResult(query, i, out var item) || item.m_eResult != EResult.k_EResultOK) continue;
                string[] tags = item.m_rgchTags.Split(',').Select(t => t.Trim()).ToArray();
                if (Array.IndexOf(tags, WorkshopBuildCodec.Tag) < 0) continue;
                string hero = tags.FirstOrDefault(t => t.StartsWith("Hero_", StringComparison.Ordinal)) ?? "";
                all.Add(new GuideSummary { Page = "workshop:" + item.m_nPublishedFileId.m_PublishedFileId,
                    Title = item.m_rgchTitle, Hero = hero, Author = item.m_ulSteamIDOwner.ToString(),
                    OwnerSteamId = item.m_ulSteamIDOwner,
                    IsPublic = item.m_eVisibility == ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic,
                    RequiresIdentityChange = Array.IndexOf(tags, WorkshopBuildCodec.IdentityChangeTag) >= 0 });
            }
            Release(query);
            received += result.m_unNumResultsReturned;
            if (result.m_unNumResultsReturned == 0 || received >= result.m_unTotalMatchingResults) break;
        }
    }

    // 点击作品才订阅和下载；等待安装/更新完成后只读取根目录的固定 JSON。
    public IEnumerator LoadDetail(GuideSummary summary, Action<GuideData, string> complete)
    {
        var id = ParseId(summary.Page);
        string error = null;
        RemoteStorageSubscribePublishedFileResult_t subscribed = default;
        yield return Wait<RemoteStorageSubscribePublishedFileResult_t>(SteamUGC.SubscribeItem(id), r => subscribed = r, e => error = e);
        if (error != null || subscribed.m_eResult != EResult.k_EResultOK)
        { complete(null, error ?? subscribed.m_eResult.ToString()); yield break; }
        if (!IsInstalled(id) && !SteamUGC.DownloadItem(id, true))
        { complete(null, "Steam rejected the download."); yield break; }
        float started = Time.realtimeSinceStartup;
        while (!IsInstalled(id))
        {
            if (Time.realtimeSinceStartup - started > 120) { complete(null, "Workshop download timed out; refresh to retry."); yield break; }
            yield return null;
        }
        GuideData guide = null;
        try
        {
            if (!SteamUGC.GetItemInstallInfo(id, out _, out string folder, 4096, out _)) throw new IOException("Missing Workshop install folder.");
            string path = Path.Combine(folder, WorkshopBuildCodec.FileName);
            if (!File.Exists(path) || new FileInfo(path).Length > WorkshopBuildCodec.MaxBytes) throw new FormatException("Missing or oversized build.json.");
            guide = WorkshopBuildCodec.Decode(File.ReadAllBytes(path));
            if (guide.Hero != summary.Hero) throw new FormatException("Build hero does not match Workshop tags.");
            // 工坊标签用于列表提示，应用限制始终以下载后的 JSON 为准。
            summary.RequiresIdentityChange = guide.RequiresIdentityChange;
            guide.Page = summary.Page;
            BuildExporter.LocalizeForDisplay(guide);
        }
        catch (Exception ex) { error = ex.Message; }
        complete(guide, error);
    }

    // 栏位只用于本地查找要更新的作品 ID；上传 JSON、订阅和下载不携带栏位值。
    // 首次创建私密草稿，后续上传同一英雄同一栏位会更新该作品并保留可见性。
    public IEnumerator Upload(GuideData guide, int slot, string storageHero, Action<ulong, string> complete)
    {
        string folder = Path.Combine(Root, storageHero + "-" + slot);
        string content = Path.Combine(folder, "content");
        string idFile = Path.Combine(folder, "publishedfileid.txt");
        Directory.CreateDirectory(content);
        File.WriteAllBytes(Path.Combine(content, WorkshopBuildCodec.FileName), WorkshopBuildCodec.Encode(guide));
        ulong number = 0;
        if (File.Exists(idFile)) ulong.TryParse(File.ReadAllText(idFile, Encoding.UTF8), out number);
        string error = null;
        // 仅在旧作品明确不存在时重建一次；网络、权限和新作品提交失败均保留 ID 供重试。
        for (int attempt = 0; attempt < 2; attempt++)
        {
            bool isNew = number == 0;
            if (isNew)
            {
                CreateItemResult_t created = default;
                yield return Wait<CreateItemResult_t>(SteamUGC.CreateItem(SteamUtils.GetAppID(), EWorkshopFileType.k_EWorkshopFileTypeFirst), r => created = r, e => error = e);
                if (error != null || created.m_eResult != EResult.k_EResultOK)
                { complete(0, LocalizationSource.GetLocalizationText("UploadCreateError", error ?? created.m_eResult.ToString())); yield break; }
                number = created.m_nPublishedFileId.m_PublishedFileId;
                // 在提交前记录 ID，提交失败后重试更新已有草稿，避免重复创建。
                File.WriteAllText(idFile, number.ToString(), new UTF8Encoding(false));
            }
            var id = new PublishedFileId_t(number);
            var handle = SteamUGC.StartItemUpdate(SteamUtils.GetAppID(), id);
            var tags = new List<string> { WorkshopBuildCodec.Tag, guide.Hero };
            if (guide.RequiresIdentityChange) tags.Add(WorkshopBuildCodec.IdentityChangeTag);
            bool accepted = SteamUGC.SetItemTitle(handle, guide.Title) &&
                SteamUGC.SetItemDescription(handle, WorkshopBuildCodec.Describe(guide)) &&
                SteamUGC.SetItemContent(handle, Path.GetFullPath(content)) &&
                SteamUGC.SetItemTags(handle, tags);
            if (isNew) accepted &= SteamUGC.SetItemVisibility(handle, ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate);
            if (!accepted) { complete(number, "Steam rejected Build metadata."); yield break; }
            SubmitItemUpdateResult_t uploaded = default;
            yield return Wait<SubmitItemUpdateResult_t>(SteamUGC.SubmitItemUpdate(handle, "BD Assistant Build"), r => uploaded = r, e => error = e, 300);
            if (error == null && uploaded.m_eResult == EResult.k_EResultFileNotFound && !isNew && attempt == 0)
            {
                // 备份失效关联；新建成功前保留原记录，新 ID 仍在提交前持久化。
                File.Copy(idFile, idFile + ".missing-" + number, true);
                Debug.LogWarning("[BD 助手] 工坊作品 " + number + " 已不存在，已备份旧 ID，正在新建私密作品。");
                number = 0;
                continue;
            }
            if (error != null || uploaded.m_eResult != EResult.k_EResultOK)
            { complete(number, LocalizationSource.GetLocalizationText("UploadSubmitError", number, error ?? uploaded.m_eResult.ToString())); yield break; }
            complete(number, null);
            yield break;
        }
    }

    private static bool IsInstalled(PublishedFileId_t id)
    {
        var state = (EItemState)SteamUGC.GetItemState(id);
        return (state & EItemState.k_EItemStateInstalled) != 0 &&
            (state & (EItemState.k_EItemStateNeedsUpdate | EItemState.k_EItemStateDownloading | EItemState.k_EItemStateDownloadPending)) == 0;
    }

    public static PublishedFileId_t ParseId(string page) => new(ulong.Parse(page.Substring("workshop:".Length)));
    public static string BrowseUrl => "https://steamcommunity.com/workshop/browse/?appid=" + SteamUtils.GetAppID().m_AppId + "&requiredtags%5B0%5D=build";
    public static string ItemUrl(ulong id) => "https://steamcommunity.com/sharedfiles/filedetails/?id=" + id;

    // 沿用游戏自带 Mod 管理器的 Steam 页面入口，统一在 Steam 内打开。
    public static void OpenItem(ulong id)
    {
        RequireSteam();
        SteamFriends.ActivateGameOverlayToWebPage("steam://url/CommunityFilePage/" + id);
    }

    public static void OpenBrowse()
    {
        RequireSteam();
        SteamFriends.ActivateGameOverlayToWebPage(BrowseUrl);
    }

    private IEnumerator Wait<T>(SteamAPICall_t call, Action<T> success, Action<string> error, float timeout = 30) where T : struct
    {
        bool done = false;
        var callback = CallResult<T>.Create((value, failed) =>
        {
            done = true;
            if (failed) error("Steam API request failed."); else success(value);
        });
        _pending.Add(callback);
        callback.Set(call);
        float start = Time.realtimeSinceStartup;
        try
        {
            while (!done && Time.realtimeSinceStartup - start < timeout) yield return null;
            if (!done) error("Steam API request timed out; check your Workshop items before retrying.");
        }
        finally { callback.Dispose(); _pending.Remove(callback); }
    }

    private void Release(UGCQueryHandle_t query) { SteamUGC.ReleaseQueryUGCRequest(query); _queries.Remove(query); }

    public void Dispose()
    {
        foreach (var callback in _pending.ToArray()) callback.Dispose();
        _pending.Clear();
        foreach (var query in _queries.ToArray()) Release(query);
    }
}
