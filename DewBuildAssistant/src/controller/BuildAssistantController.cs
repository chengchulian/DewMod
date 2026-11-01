using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DewBuildAssistant.config;
using DewBuildAssistant.model;
using DewBuildAssistant.ui;
using DewBuildAssistant.util;
using UnityEngine;

namespace DewBuildAssistant.controller;

internal sealed class BuildAssistantController : MonoBehaviour, ILangaugeChangedCallback
{
    public PluginConfig Config;
    private readonly WorkshopClient _workshop = new();
    private readonly List<GuideSummary> _allGuides = new();
    private readonly Dictionary<string, BuildRecommendations> _recommendationDrafts = new();
    private BuildAssistantView _view;
    private bool _inLobby;
    private bool _loadingList;
    private bool _loadingDetail;
    private bool _uploading;
    private int _generation;
    private int _detailVersion;
    private WorkshopFilter _filter = WorkshopFilter.All;
    private float _nextCheck;
    private string _hero;
    private string _storageHero;
    private int _slot = -1;
    private GuideSummary _selected;
    private GuideData _guide;
    private Func<string> _message = () => "";
    private string _language;
    private bool _languageChanged;

    private static string T(string key, params object[] args) => LocalizationSource.GetLocalizationText(key, args);

    public void EnterLobby()
    {
        if (_inLobby) return;
        _inLobby = true;
        _hero = null;
        _storageHero = null;
        _slot = -1;
        _language = DewSave.profileMain?.language;
        _view = BuildAssistantView.Create(transform, Toggle, Refresh, Apply, OpenWorkshop, SelectGuide, Upload, _filter, ChangeFilter, GetRecommendations, ApplyBuildNeeds);
        _view.SetOpen(false);
        _view.gameObject.SetActive(false);
        // 每次进入房间读取 build 标签，已有列表在请求失败时继续可用。
        Refresh();
    }

    // 版本号、停止协程与回调清理共同防止离开大厅后旧请求修改新窗口。
    public void LeaveLobby()
    {
        _inLobby = false;
        _generation++;
        _detailVersion++;
        StopAllCoroutines();
        _workshop.Dispose();
        _uploading = false;
        _loadingList = _loadingDetail = false;
        _guide = null;
        _selected = null;
        if (_view != null) Destroy(_view.gameObject);
        _view = null;
    }

    private void OnDestroy() => LeaveLobby();

    private void Update()
    {
        if (!_inLobby || _view == null) return;
        if (Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + 0.25f;
        bool valid = BuildApplicator.TryGetTarget(out string hero, out int slot, out _, out string storageHero);
        _view.gameObject.SetActive(valid);
        if (!valid) return;
        _view.SetBuildNeedsEnabled(BuildNeedCompatibility.Available, CanApplyBuildNeeds);
        string language = DewSave.profileMain?.language;
        if (_languageChanged || _language != language)
        {
            _languageChanged = false;
            _language = language;
            if (_guide != null) BuildExporter.LocalizeForDisplay(_guide);
            _view.RefreshLanguage();
            Draw();
        }
        if (_hero != hero)
        {
            _hero = hero;
            _slot = -1;
            _guide = null;
            _selected = null;
            _detailVersion++;
            _loadingDetail = false;
            _message = () => "";
            Draw();
        }
        if (_slot != slot || _storageHero != storageHero)
        {
            _slot = slot;
            _storageHero = storageHero;
            _view.SetTarget(hero, slot);
            Draw();
        }
    }

    private void Toggle() { if (_view != null) _view.SetOpen(!_view.IsOpen); }

    public void OnLanguageChanged() => _languageChanged = true;

    private void ChangeFilter(WorkshopFilter filter)
    {
        if (_filter == filter || _loadingList) return;
        _filter = filter;
        // 清除上一个筛选的条目，防止失败时把其他范围的旧列表误认为当前结果。
        _allGuides.Clear();
        Refresh();
    }

    private void Refresh()
    {
        if (!_inLobby || _loadingList) return;
        _loadingList = true;
        _guide = null;
        _selected = null;
        _detailVersion++;
        _loadingDetail = false;
        _message = () => T("LoadingList");
        int generation = _generation;
        Draw();
        Run(_workshop.LoadList(_filter, (guides, error) =>
        {
            if (!_inLobby || generation != _generation) return;
            _loadingList = false;
            if (error == null)
            {
                _allGuides.Clear();
                _allGuides.AddRange(guides.GroupBy(g => g.Page).Select(g => g.First()));
                _message = () => "";
            }
            else { _message = () => T("LoadError", error); Debug.LogWarning("[BD 助手] 工坊列表获取失败：" + error); }
            Draw();
        }));
    }

    private void SelectGuide(GuideSummary summary)
    {
        if (summary == _selected)
        {
            _selected = null;
            _guide = null;
            _detailVersion++;
            _loadingDetail = false;
            Draw();
            return;
        }
        _selected = summary;
        _guide = null;
        _loadingDetail = true;
        _message = () => T("LoadingDetail");
        int generation = _generation, detailVersion = ++_detailVersion;
        Draw();
        Run(_workshop.LoadDetail(summary, (guide, error) =>
        {
            if (!_inLobby || generation != _generation || detailVersion != _detailVersion) return;
            _loadingDetail = false;
            _guide = guide;
            _message = () => error == null ? "" : T("LoadError", error);
            Draw();
        }));
    }

    private void Apply()
    {
        if (_guide == null) return;
        try
        {
            BuildPlan plan = BuildApplicator.Prepare(_guide);
            if (plan.Errors.Count > 0)
            {
                var guide = _guide;
                _message = () => T("PreflightErrors") + "\n" + string.Join("\n", BuildApplicator.Prepare(guide).Errors.Distinct().Select(e => "• " + e));
            }
            else
            {
                BuildApplicator.Commit(plan);
                _message = () => T("Applied", plan.Slot + 1) + (plan.SetDejavu ? "\n" + T("CostNotice", plan.DejavuCost) : "");
                Debug.Log("[BD 助手] 已应用攻略到 " + plan.Hero + " 的第 " + (plan.Slot + 1) + " 栏：" + _guide.Page);
                AnnounceApplied(plan.Hero, _guide.Title);
            }
        }
        catch (Exception exception) { _message = () => T("ApplyError", exception.Message); Debug.LogException(exception); }
        Draw();
    }

    // 配装提交成功后只调用一次正常聊天命令，沿用游戏限速；聊天失败不改变应用结果。
    private static void AnnounceApplied(string hero, string title)
    {
        try
        {
            var chat = NetworkedManagerBase<ChatManager>.softInstance;
            if (chat == null || !Mirror.NetworkClient.active) return;
            chat.CmdSendChatMessage(BuildAppliedAnnouncement.Format(hero, title, ChatManager.ChatMessage_MaxLength));
        }
        catch (Exception ex) { Debug.LogWarning("[BD 助手] 配装已应用，喊话发送失败：" + ex.Message); }
    }

    private void OpenWorkshop()
    {
        try
        {
            WorkshopClient.RequireSteam();
            if (_selected == null) WorkshopClient.OpenBrowse();
            else WorkshopClient.OpenItem(WorkshopClient.ParseId(_selected.Page).m_PublishedFileId);
        }
        catch (Exception ex) { _message = () => T("LoadError", ex.Message); Draw(); }
    }

    private bool CanApplyBuildNeeds => _guide != null && !_loadingDetail && !_loadingList &&
        (!_guide.RequiresIdentityChange || IdentityChangeCompatibility.Enabled) &&
        _guide.Recommendations.Skills.Any(s => s.Skill != null || s.GemSlots.Any(g => g?.Gem != null));

    private void ApplyBuildNeeds()
    {
        if (!CanApplyBuildNeeds) return;
        try
        {
            if (!BuildApplicator.TryGetTarget(out string hero, out int page, out _, out _) || hero != _guide.Hero)
                throw new InvalidOperationException(T("TargetChanged"));
            int count = BuildNeedCompatibility.Apply(_guide.Recommendations, hero, page);
            _message = () => T("BuildNeedsApplied", count, page + 1);
        }
        catch (Exception ex)
        {
            string error = (ex.InnerException ?? ex).Message;
            _message = () => T("BuildNeedsError", error);
            Debug.LogWarning("[BD 助手] 构筑需求联动失败：" + error);
        }
        Draw();
    }

    // 确认上传时捕获英雄和栏位，等待 Steam 回调期间切换英雄也不会导出到其他作品。
    private void Upload(string title, string description, BuildRecommendations recommendations)
    {
        if (_uploading) return;
        try
        {
            WorkshopClient.RequireSteam();
            if (!BuildApplicator.TryGetTarget(out _, out int slot, out _, out string storageHero)) throw new InvalidOperationException(T("NotInLobby"));
            var guide = BuildExporter.Capture(title, description, Steamworks.SteamFriends.GetPersonaName());
            guide.Recommendations = recommendations;
            WorkshopBuildCodec.Encode(guide);
            BuildExporter.LocalizeForDisplay(guide);
            _uploading = true;
            _message = () => T("Uploading");
            Draw();
            Run(_workshop.Upload(guide, slot, storageHero, (id, error) =>
            {
                _uploading = false;
                _message = () => error == null ? T("Uploaded", id) : T("UploadError", error);
                if (error != null) Debug.LogWarning("[BD 助手] 工坊上传失败：" + error);
                if (error == null) _workshop.UpdateLocalPages(_allGuides);
                Draw();
                if (error == null) WorkshopClient.OpenItem(id);
            }));
        }
        catch (Exception ex) { _uploading = false; _message = () => T("UploadError", ex.Message); Draw(); }
    }

    private BuildRecommendations GetRecommendations()
    {
        if (!BuildApplicator.TryGetTarget(out _, out int slot, out _, out string storageHero)) return null;
        string key = storageHero + ":" + slot;
        _view.UploadDraftNotice = () => "";
        if (_recommendationDrafts.TryGetValue(key, out var draft)) return draft;
        try { draft = _workshop.LoadRecommendations(storageHero, slot); }
        catch (Exception ex)
        {
            _message = () => T("DraftReset", ex.Message);
            _view.UploadDraftNotice = _message;
            Debug.LogWarning("[BD 助手] 推荐草稿未载入，将使用空推荐：" + ex.Message);
            Draw();
            draft = new BuildRecommendations();
        }
        _recommendationDrafts[key] = draft;
        return draft;
    }

    // 展开子协程以统一捕获文件和 Steam 初始化异常，避免窗口永远停在加载状态。
    private void Run(IEnumerator routine)
    {
        try { WorkshopClient.RequireSteam(); StartCoroutine(Guard(routine)); }
        catch (Exception ex) { Failed(ex); }
    }

    private IEnumerator Guard(IEnumerator routine)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        try
        {
            while (stack.Count > 0)
            {
                object current = null;
                bool moved = false;
                Exception error = null;
                try { moved = stack.Peek().MoveNext(); if (moved) current = stack.Peek().Current; }
                catch (Exception ex) { error = ex; }
                if (error != null) { Failed(error); yield break; }
                if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (current is IEnumerator child) stack.Push(child);
                else yield return current;
            }
        }
        finally { while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); }
    }

    private void Failed(Exception ex)
    {
        string errorKey = _uploading ? "UploadError" : "LoadError";
        _loadingList = _loadingDetail = _uploading = false;
        _message = () => T(errorKey, ex.Message);
        Debug.LogWarning("[BD 助手] 创意工坊操作失败：" + ex.Message);
        Draw();
    }

    private void Draw()
    {
        if (_view == null) return;
        var guides = _hero == null ? new List<GuideSummary>() : _allGuides.Where(g => g.Hero == _hero).ToList();
        _view.SetGuides(guides, _selected, _loadingList);
        _view.SetGuideCards(_guide, _message(),
            _guide != null && !_loadingDetail && !_loadingList &&
                (!_guide.RequiresIdentityChange || IdentityChangeCompatibility.Enabled), !_loadingList);
        _view.SetUploadEnabled(!_uploading);
        _view.SetBuildNeedsEnabled(BuildNeedCompatibility.Available, CanApplyBuildNeeds);
    }
}
