using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DewBuildAssistant.controller;
using DewBuildAssistant.model;
using UnityEngine;
using NeedEntry = System.Tuple<string, string, bool, int>;
using NeedData = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<System.Tuple<string, string, bool, int>>>>;

namespace DewBuildAssistant.util;

// 可选联动 CatBDNeedResPing 1.5.1，只绑定已加载程序集，不加载工坊 DLL 或发送聊天。
internal static class BuildNeedCompatibility
{
    private const string Prefix = "NineTailsCat.Mods.CatBDNeedResPing.";
    internal const string ModId = "com.NineTailsCat.CatBDNeedResPing";
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type _controller;
    private static FieldInfo _panelField, _cache, _data, _heroes, _heroIndex, _pageIndex;
    private static MethodInfo _ensure, _apply, _save, _load, _updateHero, _updatePage;

    // DewMod 每次重载都会改名并保留旧程序集；以当前容器为准，避免命中已卸载的静态 Instance。
    private static LoadedModInstance FindMod() => DewMod.loadedInstances.LastOrDefault(mod =>
        mod.mod?.metadata?.id == ModId && mod.container != null);

    private static Component FindController() => FindMod()?.container.GetComponents<Component>().FirstOrDefault(component =>
        component != null && component.GetType().FullName == Prefix + "Controllers.ItemNeedController");

    private static bool Bind(Type controller)
    {
        if (_controller == controller) return true;
        var assembly = controller.Assembly;
        var panel = assembly.GetType(Prefix + "UI.ItemNeedUIPanel", false);
        var manager = assembly.GetType(Prefix + "Utils.ItemNeedDataManager", false);
        if (panel == null || manager == null) return false;
        _panelField = controller.GetField("_panel", Members);
        _cache = controller.GetField("_cachedData", Members);
        _ensure = controller.GetMethod("EnsurePanel", Members);
        _data = panel.GetField("_allData", Members);
        _heroes = panel.GetField("_heroTypeNames", Members);
        _heroIndex = panel.GetField("_selectedHeroIndex", Members);
        _pageIndex = panel.GetField("_selectedLoadoutIndex", Members);
        _apply = panel.GetMethod("ApplyCurrentLoadout", Members);
        _save = panel.GetMethod("SaveSelection", Members);
        _updateHero = panel.GetMethod("UpdateHeroDisplay", Members);
        _updatePage = panel.GetMethod("UpdateLoadoutDisplay", Members);
        _load = manager.GetMethod("LoadHeroLoadout", BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(string), typeof(int) }, null);
        if (_panelField?.FieldType != panel || _cache?.FieldType != typeof(NeedData) ||
            _data?.FieldType != typeof(NeedData) || _heroes?.FieldType != typeof(string[]) ||
            _heroIndex?.FieldType != typeof(int) || _pageIndex?.FieldType != typeof(int) ||
            new[] { _ensure, _apply, _save, _load, _updateHero, _updatePage }.Any(m => m == null)) return false;
        _controller = controller;
        return true;
    }

    // 已加载就展示按钮；接口检查留在点击时执行，接口异常会显示具体错误而非悄悄隐藏。
    public static bool Available => FindMod() != null;

    // key 去重，包含核心和非核心，沿用目标 Mod 的技能 0、精粹 100 默认值。
    public static List<NeedEntry> Entries(BuildRecommendations build)
    {
        var entries = new List<NeedEntry>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        void Add(GuideItem item, bool skill)
        {
            if (string.IsNullOrEmpty(item?.ResourceHint)) return;
            string key = item.ResourceHint;
            bool valid = skill ? DewResources.GetByShortTypeName<SkillTrigger>(key, ResourceLoadSettings.Light) != null :
                DewResources.GetByShortTypeName<Gem>(key, ResourceLoadSettings.Light) != null;
            if (!valid)
                throw new InvalidOperationException("构筑资源缺失或类型错误：" + key);
            if (keys.Add(key)) entries.Add(Tuple.Create(key, BuildExporter.ResourceDisplayName(key), skill, skill ? 0 : 100));
        }
        if (build == null) return entries;
        foreach (string key in BuildRecommendations.SlotKeys)
        {
            var slot = build.Get(key);
            Add(slot.Skill, true);
            foreach (var gem in slot.GemSlots) Add(gem?.Gem, false);
        }
        return entries;
    }

    // 只替换当前英雄当前页，使用对方保存入口，同步面板与控制器缓存。
    public static int Apply(BuildRecommendations build, string hero, int page)
    {
        var controller = FindController();
        if (controller == null) throw new InvalidOperationException("构筑需求消息控制器尚未就绪，请重新加载该 Mod。");
        if (!Bind(controller.GetType())) throw new InvalidOperationException("构筑需求消息接口与当前版本不匹配。");
        if (string.IsNullOrEmpty(hero) || page < 0) throw new ArgumentException("英雄或配装页无效。");
        var entries = Entries(build);
        if (entries.Count == 0) throw new InvalidOperationException("当前构筑没有技能或精粹。");
        _ensure.Invoke(controller, null);
        object panel = _panelField.GetValue(controller);
        if (panel == null) throw new InvalidOperationException("需求配置面板尚未就绪。");
        var heroes = (string[])_heroes.GetValue(panel);
        int index = heroes == null ? -1 : Array.IndexOf(heroes, hero);
        if (index < 0) throw new InvalidOperationException("需求配置中未找到英雄：" + hero);
        var data = (NeedData)_data.GetValue(panel);
        if (data == null) throw new InvalidOperationException("需求配置尚未就绪。");
        bool hadHero = data.TryGetValue(hero, out var pages);
        if (!hadHero) data[hero] = pages = new Dictionary<int, List<NeedEntry>>();
        bool hadPage = pages.TryGetValue(page, out var previous);
        int oldHero = (int)_heroIndex.GetValue(panel), oldPage = (int)_pageIndex.GetValue(panel);
        pages[page] = entries;
        try
        {
            _heroIndex.SetValue(panel, index);
            _pageIndex.SetValue(panel, page);
            _apply.Invoke(panel, null);
            _save.Invoke(panel, null);
            // 对方会记录并吞掉 I/O 异常，读回确认后才报告成功。
            var saved = _load.Invoke(null, new object[] { hero, page }) as List<NeedEntry>;
            if (saved == null || !entries.SequenceEqual(saved)) throw new InvalidOperationException("需求配置保存校验失败。");
        }
        catch
        {
            if (hadPage) pages[page] = previous;
            else pages.Remove(page);
            if (!hadHero) data.Remove(hero);
            _heroIndex.SetValue(panel, oldHero);
            _pageIndex.SetValue(panel, oldPage);
            _apply.Invoke(panel, null);
            throw;
        }
        _cache.SetValue(controller, data);
        _updateHero.Invoke(panel, null);
        _updatePage.Invoke(panel, null);
        return entries.Count;
    }
}
