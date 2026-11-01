using System;
using System.Collections.Generic;

// 仅替代 Steam 调用边界；上传协程、文件持久化与重建判定直接执行生产代码。
public static class DewSteam { public static bool isInitialized = true; }
namespace UnityEngine
{
    public static class Application { public static string persistentDataPath; }
    public static class Time { public static float realtimeSinceStartup; }
    public static class Debug { public static void LogWarning(object message) { } }
}
namespace Steamworks
{
    public enum EResult { k_EResultOK = 1, k_EResultFail = 2, k_EResultFileNotFound = 9, k_EResultAccessDenied = 15 }
    public enum EWorkshopFileType { k_EWorkshopFileTypeFirst }
    public enum ERemoteStoragePublishedFileVisibility { k_ERemoteStoragePublishedFileVisibilityPublic, k_ERemoteStoragePublishedFileVisibilityPrivate }
    public enum EItemState { k_EItemStateInstalled = 4, k_EItemStateNeedsUpdate = 8, k_EItemStateDownloading = 16, k_EItemStateDownloadPending = 32 }
    public enum EUserUGCList { k_EUserUGCList_Published }
    public enum EUGCMatchingUGCType { k_EUGCMatchingUGCType_Items }
    public enum EUserUGCListSortOrder { k_EUserUGCListSortOrder_CreationOrderDesc }
    public enum EUGCQuery { k_EUGCQuery_RankedByPublicationDate }
    public struct UGCQueryHandle_t { }
    public struct UGCUpdateHandle_t { public ulong Id; }
    public struct AppId_t { public uint m_AppId; }
    public struct PublishedFileId_t
    {
        public ulong m_PublishedFileId;
        public PublishedFileId_t(ulong value) { m_PublishedFileId = value; }
    }
    public struct CSteamID
    {
        public ulong m_SteamID;
        public uint GetAccountID() => (uint)m_SteamID;
    }
    public struct SteamAPICall_t { public object Result; public bool Failed; }
    public struct SteamUGCQueryCompleted_t
    {
        public EResult m_eResult;
        public uint m_unNumResultsReturned, m_unTotalMatchingResults;
    }
    public struct SteamUGCDetails_t
    {
        public EResult m_eResult;
        public string m_rgchTags, m_rgchTitle;
        public PublishedFileId_t m_nPublishedFileId;
        public ulong m_ulSteamIDOwner;
        public ERemoteStoragePublishedFileVisibility m_eVisibility;
    }
    public struct RemoteStorageSubscribePublishedFileResult_t { public EResult m_eResult; }
    public struct CreateItemResult_t { public EResult m_eResult; public PublishedFileId_t m_nPublishedFileId; }
    public struct SubmitItemUpdateResult_t { public EResult m_eResult; }
    public sealed class CallResult<T> : IDisposable where T : struct
    {
        private readonly Action<T, bool> _callback;
        private CallResult(Action<T, bool> callback) { _callback = callback; }
        public static CallResult<T> Create(Action<T, bool> callback) => new(callback);
        public void Set(SteamAPICall_t call) => _callback((T)call.Result, call.Failed);
        public void Dispose() { }
    }
    public static class SteamUser { public static CSteamID GetSteamID() => new() { m_SteamID = 10 }; }
    public static class SteamUtils { public static AppId_t GetAppID() => new() { m_AppId = 2444750 }; }
    public static class SteamFriends { public static void ActivateGameOverlayToWebPage(string url) { } }

    public static class SteamUGC
    {
        public static readonly Queue<SteamAPICall_t> Creates = new();
        public static readonly Queue<SteamAPICall_t> Submits = new();
        public static readonly List<ulong> UpdatedIds = new();
        public static readonly List<ulong> PrivateIds = new();
        public static readonly List<ulong> SavedIdsAtSubmit = new();
        public static int CreateCount;
        public static bool AcceptMetadata;
        public static string IdFile, ContentPath;
        public static void Reset()
        {
            Creates.Clear(); Submits.Clear(); UpdatedIds.Clear(); PrivateIds.Clear(); SavedIdsAtSubmit.Clear();
            CreateCount = 0; AcceptMetadata = true; IdFile = ContentPath = null;
        }
        public static SteamAPICall_t CreateItem(AppId_t app, EWorkshopFileType type)
        {
            CreateCount++;
            return Creates.Dequeue();
        }
        public static UGCUpdateHandle_t StartItemUpdate(AppId_t app, PublishedFileId_t id)
        {
            UpdatedIds.Add(id.m_PublishedFileId);
            return new UGCUpdateHandle_t { Id = id.m_PublishedFileId };
        }
        public static bool SetItemTitle(UGCUpdateHandle_t handle, string title) => AcceptMetadata;
        public static bool SetItemDescription(UGCUpdateHandle_t handle, string description) => AcceptMetadata;
        public static bool SetItemContent(UGCUpdateHandle_t handle, string path) { ContentPath = path; return AcceptMetadata; }
        public static bool SetItemTags(UGCUpdateHandle_t handle, IList<string> tags) => AcceptMetadata;
        public static bool SetItemVisibility(UGCUpdateHandle_t handle, ERemoteStoragePublishedFileVisibility visibility)
        {
            if (visibility != ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate)
                throw new Exception("New upload must be private");
            PrivateIds.Add(handle.Id);
            return true;
        }
        public static SteamAPICall_t SubmitItemUpdate(UGCUpdateHandle_t handle, string note)
        {
            SavedIdsAtSubmit.Add(ulong.Parse(System.IO.File.ReadAllText(IdFile, System.Text.Encoding.UTF8)));
            return Submits.Dequeue();
        }
        // 未被上传场景使用的查询、下载边界保持显式失败。
        public static UGCQueryHandle_t CreateQueryUserUGCRequest(uint account, EUserUGCList list, EUGCMatchingUGCType type,
            EUserUGCListSortOrder sort, AppId_t creator, AppId_t consumer, uint page) => throw new NotSupportedException();
        public static UGCQueryHandle_t CreateQueryAllUGCRequest(EUGCQuery query, EUGCMatchingUGCType type, AppId_t creator,
            AppId_t consumer, uint page) => throw new NotSupportedException();
        public static bool AddRequiredTag(UGCQueryHandle_t query, string tag) => throw new NotSupportedException();
        public static bool SetAllowCachedResponse(UGCQueryHandle_t query, uint seconds) => throw new NotSupportedException();
        public static SteamAPICall_t SendQueryUGCRequest(UGCQueryHandle_t query) => throw new NotSupportedException();
        public static bool GetQueryUGCResult(UGCQueryHandle_t query, uint index, out SteamUGCDetails_t item) => throw new NotSupportedException();
        public static bool ReleaseQueryUGCRequest(UGCQueryHandle_t query) => throw new NotSupportedException();
        public static SteamAPICall_t SubscribeItem(PublishedFileId_t id) => throw new NotSupportedException();
        public static bool DownloadItem(PublishedFileId_t id, bool highPriority) => throw new NotSupportedException();
        public static uint GetItemState(PublishedFileId_t id) => throw new NotSupportedException();
        public static bool GetItemInstallInfo(PublishedFileId_t id, out ulong size, out string folder, uint length, out uint timestamp) => throw new NotSupportedException();
    }
}
