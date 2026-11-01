using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DewBuildAssistant.controller;
using DewBuildAssistant.model;
using DewBuildAssistant.util;
using DewBuildAssistant.ui;
using DewInternal;
using Newtonsoft.Json.Linq;

internal static class Program
{
    private static int _assertions;
    private static void Check(bool pass, string description)
    {
        _assertions++;
        if (!pass) throw new Exception("FAIL: " + description);
    }

    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        try
        {
            string fixtures = Path.GetFullPath(args.Length > 0 ? args[0] : "DewBuildAssistant/tests/fixtures");
            SchemaTests(fixtures);
            ApplicationTests();
            MidJoinTests();
            WorkshopTests();
            WorkshopUploadTests.Run(fixtures, Check);
            FilterTests();
            LocalPageTests();
            CatalogueTests();
            RecommendationTests();
            RecommendationDragTests();
            GemDropOnSkillTests();
            EmptySkillGemTests();
            MemoryAutofillTests();
            NativeSkillSpriteTests();
            NativeIconFitTests();
            BuildNeedTests();
            IdentityChangeTests();
            LanguageTests();
            AnnouncementTests();
            Console.WriteLine("PASS: " + _assertions + " assertions");
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception.GetType().FullName + ": " + exception.Message);
            return 1;
        }
    }

    // 消息使用发送时的英雄翻译，长标题仍保留完整署名和合法 Unicode 文本。
    private static void AnnouncementTests()
    {
        Reset();
        DewLocalization.data.ui["Hero_Test_Name"] = "塞图斯";
        Check(BuildAppliedAnnouncement.Format("Hero_Test", "冰川", 100) == "[塞图斯]一键星座 冰川 已应用。 --Bd助手mod", "announcement includes localized hero and exact requested wording");
        var english = new PerLanguageLocalizationData();
        english.ui["Hero_Test_Name"] = "Cetos";
        DewLocalization.data = english;
        Check(BuildAppliedAnnouncement.Format("Hero_Test", "冰川", 100).StartsWith("[Cetos]"), "announcement resolves hero after language switch without cached name");
        string longMessage = BuildAppliedAnnouncement.Format("Hero_Test", new string('冰', 120), 100);
        Check(longMessage.Length == 100 && longMessage.EndsWith(" 已应用。 --Bd助手mod"), "long title fits native chat limit while retaining signature");
        string emoji = BuildAppliedAnnouncement.Format("Hero_Test", string.Concat(Enumerable.Repeat("😀", 120)), 100);
        string emojiTitle = emoji.Substring("[Cetos]一键星座 ".Length).Split(' ')[0];
        Check(emoji.Length <= 100 && !char.IsHighSurrogate(emojiTitle.Last()), "chat truncation preserves complete surrogate pairs");
        string combined = BuildAppliedAnnouncement.Format("Hero_Test", string.Concat(Enumerable.Repeat("e\u0301", 120)), 100);
        Check(combined.Length <= 100 && combined.Contains("e\u0301 已应用"), "chat truncation preserves combining character clusters");
        Check(BuildAppliedAnnouncement.Format("Hero_Test", " A\r\n\t<b>B</b> ", 100) == "[Cetos]一键星座 A ＜b＞B＜/b＞ 已应用。 --Bd助手mod", "workshop title stays single line and cannot introduce rich text tags");
        Check(BuildAppliedAnnouncement.Format("Hero_Missing", null, 100) == "[Hero_Missing]一键星座  已应用。 --Bd助手mod", "missing localization and empty title remain printable");
        english.ui["Hero_Test_Name"] = new string('英', 120);
        Check(BuildAppliedAnnouncement.Format("Hero_Test", new string('名', 120), 100).Length == 100, "long localized hero and title share chat length budget");
    }

    private static void NativeIconFitTests()
    {
        Check(NativeIconFit.Calculate(44, 44, -64, -64, 64, 64, out float scale, out float x, out float y) &&
            Math.Abs(scale * 128 - 40) < 0.001f && x == 0 && y == 0, "128px native star fits inside 44px tile with padding");
        Check(NativeIconFit.Calculate(44, 68, 20, -10, 180, 70, out scale, out x, out y) &&
            Math.Abs(scale - 0.25f) < 0.001f && x == -25 && y == -7.5f, "offset and non-square native bounds are centered without distortion");
        Check(!NativeIconFit.Calculate(0, 0, -64, -64, 64, 64, out _, out _, out _), "unresolved parent layout defers fit");
        Check(!NativeIconFit.Calculate(44, 44, 0, 0, 0, 100, out _, out _, out _), "empty graphic bounds defer fit");
        Check(!NativeIconFit.Calculate(44, 44, float.NaN, 0, 64, 64, out _, out _, out _), "non-finite bounds do not produce invalid transform");
        Check(NativeIconFit.Calculate(88, 88, -64, -64, 64, 64, out scale, out _, out _) &&
            Math.Abs(scale * 128 - 84) < 0.001f, "resized parent recalculates whole-icon scale");
    }

    // 覆盖无技能精粹的编辑、序列化、语言刷新、描述和需求导出全链路。
    private static void EmptySkillGemTests()
    {
        Reset();
        DewResources.Assets["Gem_Extra"] = new Gem();
        var guide = new GuideData { Hero = "Hero_Test", Title = "独立精粹" };
        var build = guide.Recommendations;
        var w = guide.Recommendations.Get("W");
        var e = guide.Recommendations.Get("E");
        Check(RecommendationEditing.PlaceGem(build, w, 3, new GuideItem { ResourceHint = "Gem_Test", Name = "旧名字" }) && w.Skill == null,
            "fourth gem position accepts candidate without skill");
        w.GemSlots[3].IsCore = true;
        Check(RecommendationEditing.PlaceGemOnSkill(build, w, new GuideItem { ResourceHint = "Gem_Extra" }) && w.GemSlots[0] != null && w.Skill == null,
            "dropping on empty skill fills first gap while keeping skill empty");
        Check(!RecommendationEditing.PlaceGemOnSkill(build, w, new GuideItem { ResourceHint = "Gem_Test" }) && w.GemSlots[1] == null,
            "duplicate gems are still rejected on empty skill");
        var decoded = WorkshopBuildCodec.Decode(WorkshopBuildCodec.Encode(guide));
        Check(decoded.Recommendations.Get("W").Skill == null && decoded.Recommendations.Get("W").GemSlots[3].IsCore &&
            decoded.Recommendations.Get("W").GemSlots[1] == null, "gems-only JSON preserves position, gaps and independent core marker");
        BuildExporter.LocalizeForDisplay(decoded);
        Check(decoded.Recommendations.Get("W").GemSlots[3].Gem.Name == "测试精粹" && decoded.Details.Contains("测试精粹"),
            "gems-only resources are localized and included in workshop description");
        var exported = BuildNeedCompatibility.Entries(decoded.Recommendations);
        Check(exported.Count == 2 && exported.All(item => !item.Item3) && exported.Any(item => item.Item1 == "Gem_Test"),
            "need export includes gems from empty skill positions");
        Check(RecommendationEditing.PlaceGemOnSkill(build, e, w.GemSlots[3].Gem, w, 3) && w.GemSlots[3] == null && e.Skill == null && e.GemSlots[0].IsCore,
            "moving between empty skills preserves gem core marker");
        var gems = e.GemSlots;
        Check(RecommendationEditing.PlaceSkill(e, new GuideItem { ResourceHint = "St_Q_First" }) &&
            ReferenceEquals(gems, e.GemSlots) && e.GemSlots[0].IsCore,
            "choosing skill later preserves preselected gems and core markers");
        var doc = JObject.Parse(Encoding.UTF8.GetString(WorkshopBuildCodec.Encode(decoded)));
        doc["Build"]["Skills"][1]["GemSlots"][1] = doc["Build"]["Skills"][1]["GemSlots"][0].DeepClone();
        bool rejected = false;
        try { WorkshopBuildCodec.Decode(Encoding.UTF8.GetBytes(doc.ToString())); } catch (FormatException) { rejected = true; }
        Check(rejected, "gems-only JSON still rejects duplicate gem keys");
    }

    private static void BuildNeedTests()
    {
        Reset();
        DewMod.loadedInstances.Clear();
        Check(!BuildNeedCompatibility.Available, "need integration stays hidden when mod is not loaded");
        var build = new BuildRecommendations();
        build.Get("Q").Skill = new GuideItem { ResourceHint = "St_Q_First", Name = "stale UI name" };
        build.Get("Q").IsCore = true;
        build.Get("Q").GemSlots[3] = new BuildGem { Gem = new GuideItem { ResourceHint = "Gem_Test" } };
        build.Get("W").Skill = new GuideItem { ResourceHint = "St_Q_First" };
        build.Get("W").GemSlots[0] = new BuildGem { Gem = new GuideItem { ResourceHint = "Gem_Test" }, IsCore = true };
        build.Get("R").Skill = new GuideItem { ResourceHint = "St_R_First" };
        var entries = BuildNeedCompatibility.Entries(build);
        Check(entries.Count == 3 && entries[0].Item1 == "St_Q_First" && entries[1].Item1 == "Gem_Test", "need entries use resource keys and deduplicate across QWERD");
        Check(entries.Count(e => e.Item3) == 2 && entries.Any(e => !e.Item3), "core and non-core skills and gems are included");
        Check(entries[0].Item2 != "stale UI name" && entries[0].Item4 == 0 && entries[1].Item4 == 100, "need display names are localized and default values match target mod");
        Check(BuildNeedCompatibility.Entries(new BuildRecommendations()).Count == 0, "empty recommendation slots stay empty");
        var old = Tuple.Create("old", "old", false, 100);
        NineTailsCat.Mods.CatBDNeedResPing.Utils.ItemNeedDataManager.Disk = new()
        {
            ["Hero_Other"] = new() { [0] = new() { old } },
            ["Hero_Test"] = new() { [0] = new() { old }, [4] = new() { old } }
        };
        var container = new UnityEngine.GameObject();
        var controller = container.AddComponent<NineTailsCat.Mods.CatBDNeedResPing.Controllers.ItemNeedController>();
        NineTailsCat.Mods.CatBDNeedResPing.Controllers.ItemNeedController.Instance = controller;
        Check(!BuildNeedCompatibility.Available, "stale static instance outside loaded mod registry is ignored");
        var loaded = new LoadedModInstance
        {
            mod = new ModItem { metadata = new ModMetadata { id = BuildNeedCompatibility.ModId } },
            container = container
        };
        DewMod.loadedInstances.Add(loaded);
        Check(BuildNeedCompatibility.Available, "loaded need mod exposes the optional integration");
        Check(BuildNeedCompatibility.Apply(build, "Hero_Test", 4) == 3, "need integration imports current build to explicit current page");
        Check(controller.Panel.Page == 4 && controller.Panel.Hero == "Hero_Test" && controller.Panel.Selected.SequenceEqual(entries), "live need panel switches to imported hero and page");
        Check(controller.Panel.Data["Hero_Other"][0].Single() == old && controller.Panel.Data["Hero_Test"][0].Single() == old, "need import preserves other heroes and pages");
        Check(ReferenceEquals(controller.Cache, controller.Panel.Data) && controller.Panel.DisplayUpdates == 2, "controller cache and visible panel labels are synchronized");
        Check(NineTailsCat.Mods.CatBDNeedResPing.Utils.ItemNeedDataManager.LoadHeroLoadout("Hero_Test", 4).SequenceEqual(entries), "need import persists through target save boundary");
        NineTailsCat.Mods.CatBDNeedResPing.Utils.ItemNeedDataManager.FailSave = true;
        bool failed = false;
        try { BuildNeedCompatibility.Apply(build, "Hero_Test", 0); } catch (InvalidOperationException) { failed = true; }
        Check(failed && controller.Panel.Data["Hero_Test"][0].Single() == old && controller.Panel.Page == 4 && controller.Panel.Selected.SequenceEqual(entries), "swallowed save error is detected and previous selection restored");
        failed = false;
        try { BuildNeedCompatibility.Apply(build, "Hero_Missing", 0); } catch (InvalidOperationException) { failed = true; }
        Check(failed && controller.Panel.Page == 4, "unrecognized hero leaves need selection intact");
        failed = false;
        try { BuildNeedCompatibility.Apply(new BuildRecommendations(), "Hero_Test", 4); } catch (InvalidOperationException) { failed = true; }
        Check(failed && controller.Panel.Selected.Count == 3, "empty build does not clear existing needs");
        build.Get("D").Skill = new GuideItem { ResourceHint = "Gem_Test" };
        failed = false;
        try { BuildNeedCompatibility.Apply(build, "Hero_Test", 4); } catch (InvalidOperationException) { failed = true; }
        Check(failed && controller.Panel.Selected.Count == 3, "invalid resource type fails before changing need data");
        NineTailsCat.Mods.CatBDNeedResPing.Utils.ItemNeedDataManager.FailSave = false;
        DewMod.loadedInstances.Clear();
        Check(!BuildNeedCompatibility.Available, "need integration hides after registry unload even if static instance remains");
        var reloadedContainer = new UnityEngine.GameObject();
        var reloaded = reloadedContainer.AddComponent<NineTailsCat.Mods.CatBDNeedResPing.Controllers.ItemNeedController>();
        loaded.container = reloadedContainer;
        DewMod.loadedInstances.Add(loaded);
        build.Get("D").Skill = null;
        Check(BuildNeedCompatibility.Available && BuildNeedCompatibility.Apply(build, "Hero_Test", 2) == 3 &&
            reloaded.Panel.Page == 2 && controller.Panel.Page == 4, "reloaded container is used while old static instance still points at old controller");
        loaded.container = new UnityEngine.GameObject();
        Check(BuildNeedCompatibility.Available, "loaded mod button remains visible while controller initializes");
        failed = false;
        try { BuildNeedCompatibility.Apply(build, "Hero_Test", 2); } catch (InvalidOperationException) { failed = true; }
        Check(failed, "missing live controller reports a concrete error without using stale static instance");
        loaded.container = null;
        Check(!BuildNeedCompatibility.Available, "destroyed mod container hides need button");
        DewMod.loadedInstances.Clear();
        NineTailsCat.Mods.CatBDNeedResPing.Controllers.ItemNeedController.Instance = null;
    }

    // 候选精粹与已选精粹落到技能图标时，验证空位选择和失败后的原子性。
    private static void GemDropOnSkillTests()
    {
        var build = new BuildRecommendations();
        var q = build.Get("Q");
        var r = build.Get("R");
        var empty = build.Get("W");
        RecommendationEditing.PlaceSkill(q, new GuideItem { ResourceHint = "St_Q_Test" });
        RecommendationEditing.PlaceSkill(r, new GuideItem { ResourceHint = "St_R_Test" });
        var a = new GuideItem { ResourceHint = "Gem_A" };
        var b = new GuideItem { ResourceHint = "Gem_B" };
        var c = new GuideItem { ResourceHint = "Gem_C" };
        var d = new GuideItem { ResourceHint = "Gem_D" };
        RecommendationEditing.PlaceGem(build, q, 2, a);
        q.GemSlots[2].IsCore = true;
        Check(RecommendationEditing.PlaceGemOnSkill(build, q, b) && q.GemSlots[0].Gem.ResourceHint == "Gem_B" &&
            q.GemSlots[1] == null && q.GemSlots[2].IsCore, "candidate dropped on skill fills first gap and preserves chosen slots");
        Check(!ReferenceEquals(q.GemSlots[0].Gem, b) && !q.GemSlots[0].IsCore, "automatic candidate slot is copied and starts non-core");
        Check(!RecommendationEditing.PlaceGemOnSkill(build, q, a) && q.GemSlots[1] == null, "automatic placement rejects duplicate candidate without filling gap");
        RecommendationEditing.PlaceGem(build, r, 3, c);
        r.GemSlots[3].IsCore = true;
        var moving = r.GemSlots[3].Gem;
        Check(RecommendationEditing.PlaceGemOnSkill(build, q, moving, r, 3) && q.GemSlots[1].Gem.ResourceHint == "Gem_C" &&
            q.GemSlots[1].IsCore && r.GemSlots[3] == null, "selected gem dropped on another skill moves into first gap with core marker");
        var own = q.GemSlots[1];
        Check(!RecommendationEditing.PlaceGemOnSkill(build, q, own.Gem, q, 1) && ReferenceEquals(own, q.GemSlots[1]) && q.GemSlots[3] == null,
            "dropping onto own skill preserves original gem slot");
        // 注入已损坏的重复数据，验证移动预检仍保留两端内容。
        r.GemSlots[0] = new BuildGem { Gem = new GuideItem { ResourceHint = "Gem_A" } };
        var duplicate = r.GemSlots[0];
        Check(!RecommendationEditing.PlaceGemOnSkill(build, q, duplicate.Gem, r, 0) && ReferenceEquals(duplicate, r.GemSlots[0]) && q.GemSlots[3] == null,
            "duplicate move rejection preserves source and empty destination");
        Check(!RecommendationEditing.PlaceGemOnSkill(build, q, d, r, 1) && q.GemSlots[3] == null,
            "stale drag does not fill the automatic slot");
        Check(RecommendationEditing.PlaceGemOnSkill(build, q, d) && q.GemSlots[3].Gem.ResourceHint == "Gem_D", "automatic placement reaches fourth slot");
        RecommendationEditing.PlaceGem(build, r, 1, new GuideItem { ResourceHint = "Gem_E" });
        var overflow = r.GemSlots[1];
        Check(!RecommendationEditing.PlaceGemOnSkill(build, q, overflow.Gem, r, 1) && ReferenceEquals(overflow, r.GemSlots[1]) && q.GemSlots.Count == 4,
            "full skill rejects drop without removing source or creating fifth slot");
        Check(RecommendationEditing.PlaceGemOnSkill(build, empty, overflow.Gem, r, 1) && empty.Skill == null &&
            empty.GemSlots[0].Gem.ResourceHint == "Gem_E" && r.GemSlots[1] == null,
            "empty skill accepts automatic gem placement and moves source");
        Check(!RecommendationEditing.PlaceGemOnSkill(build, null, b) && !RecommendationEditing.PlaceGemOnSkill(build, r, null) &&
            !RecommendationEditing.PlaceGemOnSkill(build, r, new GuideItem { ResourceHint = "St_Test" }), "automatic placement rejects missing target and invalid resource kinds");
    }

    // 验证首次大厅只有共用原生 Sprite、没有局内技能模板时的资源解析与缺失处理。
    private static void NativeSkillSpriteTests()
    {
        var mask = new UnityEngine.Sprite { name = "ActionButton_Background" };
        var border = new UnityEngine.Sprite { name = "ActionButton_Border" };
        var background = new UnityEngine.Sprite { name = "ActionBar_Level_Background" };
        var gemBorder = new UnityEngine.Sprite { name = "ActionBar_Level_Border" };
        Check(NativeSkillSprites.TryResolve(new[] { mask, border, background, gemBorder }, out var native),
            "lobby common native sprites work without in-game UI template");
        Check(ReferenceEquals(native.Mask, mask) && ReferenceEquals(native.SkillBorder, border) &&
            ReferenceEquals(native.Background, background) && ReferenceEquals(native.GemBorder, gemBorder),
            "native round frame, mask and gem frame are referenced directly");
        Check(NativeSkillSprites.TryResolve(new[] { mask, border, background }, out var shared) && ReferenceEquals(shared.GemBorder, border),
            "missing optional gem border uses the game skill border");
        foreach (var missing in new[] { mask, border, background })
            Check(!NativeSkillSprites.TryResolve(new[] { mask, border, background, gemBorder }.Where(s => s != missing), out var absent) && absent == null,
                "missing required native sprite rejects square-image construction: " + missing.name);
        Check(!NativeSkillSprites.TryResolve(null, out _) && !NativeSkillSprites.TryResolve(Array.Empty<UnityEngine.Sprite>(), out _),
            "unloaded native resources produce no invalid sprite set");
        Check(!NativeSkillSprites.TryResolve(new[] { new UnityEngine.Sprite { name = "Knob" }, border, background }, out _),
            "editor-only built-in sprite never substitutes for a game resource");
        Check(NativeSkillSprites.TryResolve(new[] { (UnityEngine.Sprite)null, mask, border, background }, out _),
            "destroyed or missing sprite entries do not break resource resolution");
    }

    // 自动填充跟随当前普通/转职配装页，只补齐空的 Q/R/D。
    private static void MemoryAutofillTests()
    {
        Reset();
        var page = DewSave.profileMain.heroLoadouts["Hero_Test"][1];
        page.SetSkill(HeroSkillLocation.Q, 1);
        var build = new BuildRecommendations();
        BuildExporter.FillCurrentMemorySkills(build);
        Check(build.Get("Q").Skill.ResourceHint == "St_Q_Second", "editor fills Q from selected loadout page");
        Check(build.Get("R").Skill.ResourceHint == "St_R_First", "editor fills R from current memory");
        Check(build.Get("D").Skill.ResourceHint == "St_I_First", "editor maps identity memory to D");
        Check(build.Get("W").Skill == null && build.Get("E").Skill == null, "autofill leaves W and E for editing");
        Check(build.Skills.All(s => !s.IsCore && s.GemSlots.All(g => g == null)), "autofill does not invent core markers or gems");
        build.Get("Q").IsCore = true;
        RecommendationEditing.PlaceGem(build, build.Get("Q"), 2, new GuideItem { ResourceHint = "Gem_Test", Name = "精粹" });
        build.Get("Q").GemSlots[2].IsCore = true;
        page.SetSkill(HeroSkillLocation.Q, 0);
        BuildExporter.FillCurrentMemorySkills(build);
        Check(build.Get("Q").Skill.ResourceHint == "St_Q_Second" && build.Get("Q").IsCore && build.Get("Q").GemSlots[2].IsCore,
            "opening editor again preserves existing skill, gems and core markers");
        var active = new HeroLoadoutData();
        active.SetSkill(HeroSkillLocation.Q, 1);
        SingletonBehaviour<UI_Lobby_Constellations>.softInstance = new UI_Lobby_Constellations
            { isActiveAndEnabled = true, selectedLoadoutIndex = 3, loadout = active };
        var selected = new BuildRecommendations();
        BuildExporter.FillCurrentMemorySkills(selected);
        Check(selected.Get("Q").Skill.ResourceHint == "St_Q_Second", "autofill follows page selected in constellation editor");
        SingletonBehaviour<UI_Lobby_Constellations>.softInstance = null;
        var hero = (Hero_Test)DewResources.Assets["Hero_Test"];
        hero.Skills.Options[HeroSkillLocation.Q] = new SkillTrigger[] { new St_Q_First(), new St_Q_Second(), new St_Q_Foreign() };
        var identity = Enumerable.Range(0, 5).Select(_ => new HeroLoadoutData()).ToList();
        identity[1].SetSkill(HeroSkillLocation.Q, 2);
        DewSave.profileMain.heroLoadouts["DewIdentityChange_Hero_Test"] = identity;
        DewIdentityChange.config.LoadoutPageMapper.Enabled = true;
        var changed = new BuildRecommendations();
        BuildExporter.FillCurrentMemorySkills(changed);
        Check(changed.Get("Q").Skill.ResourceHint == "St_Q_Foreign", "autofill uses Identity Change page and extended catalogue");
        DewSave.profileMain.heroLoadouts.Remove("DewIdentityChange_Hero_Test");
        var missing = new BuildRecommendations();
        BuildExporter.FillCurrentMemorySkills(missing);
        Check(missing.Skills.All(s => s.Skill == null), "missing identity page never fills from normal loadout");
        Reset();
        DewSave.profileMain.heroLoadouts["Hero_Test"][1].SetSkill(HeroSkillLocation.R, 99);
        var invalid = new BuildRecommendations();
        BuildExporter.FillCurrentMemorySkills(invalid);
        Check(invalid.Get("R").Skill == null && invalid.Get("Q").Skill != null && invalid.Get("D").Skill != null,
            "invalid memory index leaves only its position empty");
        DewPlayer.local = null;
        BuildExporter.FillCurrentMemorySkills(new BuildRecommendations());
        Check(true, "editor tolerates loadout becoming unavailable");
        Reset();
    }

    // 验证候选复制、跨槽移动以及拒绝拖放时的数据完整性。
    private static void RecommendationDragTests()
    {
        var build = new BuildRecommendations();
        var q = build.Get("Q");
        var w = build.Get("W");
        var e = build.Get("E");
        var memory = new GuideItem { Name = "记忆", ResourceHint = "St_Test" };
        var gem = new GuideItem { Name = "精粹", ResourceHint = "Gem_Test" };
        Check(build.Skills.Select(s => s.Slot).SequenceEqual(new[] { "Q", "W", "E", "R", "D" }), "editor starts in game QWERD order");
        Check(!RecommendationEditing.PlaceSkill(q, gem), "skill position rejects gem candidate");
        Check(RecommendationEditing.PlaceGem(build, q, 0, gem) && q.Skill == null, "gem can be placed before choosing a skill");
        Check(RecommendationEditing.Remove(q, 0, q.GemSlots[0].Gem), "gem on empty skill can be removed independently");
        Check(RecommendationEditing.PlaceSkill(q, memory) && !ReferenceEquals(q.Skill, memory), "skill candidate is copied into Q");
        Check(RecommendationEditing.PlaceGem(build, q, 0, gem) && !ReferenceEquals(q.GemSlots[0].Gem, gem), "gem candidate is copied into a slot");
        Check(!RecommendationEditing.PlaceGem(build, q, 1, gem) && q.GemSlots[1] == null, "duplicate gem within same skill is rejected");
        Check(!RecommendationEditing.PlaceGem(build, q, 4, gem) && !RecommendationEditing.PlaceGem(build, q, -1, gem), "gem positions stay within four slots");
        Check(!RecommendationEditing.PlaceGem(build, q, 1, memory), "gem slot rejects skill candidate");
        var placed = q.GemSlots[0].Gem;
        q.GemSlots[0].IsCore = true;
        Check(!RecommendationEditing.PlaceGem(build, q, 0, placed, q, 0) && q.GemSlots[0].Gem == placed, "same-slot drop preserves source");
        Check(!RecommendationEditing.PlaceGem(build, q, 1, gem, q, 0) && q.GemSlots[0].Gem == placed, "stale source preserves existing item");
        Check(RecommendationEditing.PlaceGem(build, q, 3, placed, q, 0) && q.GemSlots[0] == null && q.GemSlots[3].IsCore, "move to fourth slot preserves gem core marker");
        Check(RecommendationEditing.PlaceSkill(w, memory), "second skill can be set");
        Check(!RecommendationEditing.PlaceGem(build, w, 0, gem) && w.GemSlots[0] == null, "same gem candidate is rejected across different skills");
        Check(RecommendationEditing.PlaceGem(build, w, 0, new GuideItem { ResourceHint = "Gem_Other" }), "different gem can be placed on another skill");
        var originalQGem = q.GemSlots[3];
        var originalWGem = w.GemSlots[0];
        Check(RecommendationEditing.PlaceGem(build, w, 0, q.GemSlots[3].Gem, q, 3) &&
            ReferenceEquals(w.GemSlots[0], originalQGem) && ReferenceEquals(q.GemSlots[3], originalWGem) &&
            w.GemSlots[0].IsCore && !q.GemSlots[3].IsCore, "cross-skill gem exchange preserves both items and their core markers");
        Check(RecommendationEditing.PlaceGem(build, q, 3, w.GemSlots[0].Gem, w, 0) &&
            ReferenceEquals(q.GemSlots[3], originalQGem) && ReferenceEquals(w.GemSlots[0], originalWGem), "swapping gems back restores exact original slots");
        Check(!RecommendationEditing.PlaceGem(build, w, 0, gem) && w.GemSlots[0].Gem.ResourceHint == "Gem_Other", "duplicate candidate replacement preserves both original gems");
        Check(RecommendationEditing.Remove(w, 0, w.GemSlots[0].Gem), "selected gem can be removed");
        Check(RecommendationEditing.PlaceGem(build, w, 2, q.GemSlots[3].Gem, q, 3) && q.GemSlots[3] == null && w.GemSlots[2].IsCore, "cross-skill move preserves core flag");
        q.IsCore = true;
        RecommendationEditing.PlaceGem(build, q, 0, new GuideItem { ResourceHint = "Gem_Q" });
        q.GemSlots[0].IsCore = false;
        Check(RecommendationEditing.PlaceSkill(e, q.Skill, q) && q.Skill == null && !q.IsCore && q.GemSlots.All(g => g == null), "moving skill clears source and moves its gems");
        Check(e.IsCore && e.GemSlots[0] != null && !e.GemSlots[0].IsCore, "skill and gem core flags remain independent after move");
        var groups = e.GemSlots;
        Check(RecommendationEditing.PlaceSkill(e, new GuideItem { ResourceHint = "St_Other" }) && ReferenceEquals(groups, e.GemSlots) && e.IsCore, "candidate replacement retains chosen gem layout and core marker");
        var eSkill = e.Skill;
        var wSkill = w.Skill;
        var wGems = w.GemSlots;
        Check(RecommendationEditing.PlaceSkill(w, e.Skill, e) && w.Skill == eSkill && e.Skill == wSkill,
            "moving skill onto occupied skill exchanges both skills");
        Check(w.IsCore && !e.IsCore && ReferenceEquals(w.GemSlots, groups) && ReferenceEquals(e.GemSlots, wGems) &&
            w.GemSlots[0].Gem.ResourceHint == "Gem_Q" && e.GemSlots[2].Gem.ResourceHint == "Gem_Test" && e.GemSlots[2].IsCore,
            "occupied skill exchange carries all gem slots and independent core flags");
        Check(w.Slot == "W" && e.Slot == "E" && !ReferenceEquals(w.GemSlots, e.GemSlots), "skill exchange retains key positions without sharing gem lists");
        Check(!RecommendationEditing.PlaceSkill(w, w.Skill, w) && !RecommendationEditing.PlaceSkill(w, memory, e) &&
            w.Skill == eSkill && e.Skill == wSkill, "same-position and stale skill drags preserve both groups");
        Check(RecommendationEditing.PlaceSkill(e, w.Skill, w) && ReferenceEquals(e.GemSlots, groups) && ReferenceEquals(w.GemSlots, wGems) &&
            e.Skill == eSkill && w.Skill == wSkill, "swapping back restores both complete groups");
        var r = build.Get("R");
        RecommendationEditing.PlaceGem(build, r, 3, new GuideItem { ResourceHint = "Gem_R" });
        Check(RecommendationEditing.PlaceSkill(r, e.Skill, e) && e.Skill == null && e.GemSlots[3].Gem.ResourceHint == "Gem_R" &&
            r.Skill == eSkill && r.IsCore && r.GemSlots[0].Gem.ResourceHint == "Gem_Q", "moving into gem-only position exchanges groups without losing preselected gems");
        var d = build.Get("D");
        RecommendationEditing.PlaceSkill(d, new GuideItem { ResourceHint = "St_I_First" });
        RecommendationEditing.PlaceGem(build, d, 1, new GuideItem { ResourceHint = "Gem_Identity" });
        d.GemSlots[1].IsCore = true;
        var identity = d.Skill;
        Check(!RecommendationEditing.PlaceSkill(q, d.Skill, d) && d.Skill == identity && q.Skill == null,
            "identity memory cannot be dragged out of D");
        Check(!RecommendationEditing.PlaceSkill(d, r.Skill, r) && d.Skill == identity && r.Skill == eSkill,
            "other skills cannot exchange with fixed identity memory");
        Check(RecommendationEditing.PlaceGem(build, q, 1, d.GemSlots[1].Gem, d, 1) && d.Skill == identity &&
            d.GemSlots[1] == null && q.GemSlots[1].IsCore, "D gem remains movable independently of fixed identity skill");
        Check(RecommendationEditing.PlaceGemOnSkill(build, d, q.GemSlots[1].Gem, q, 1) && d.Skill == identity && d.GemSlots[0].IsCore,
            "identity memory still accepts automatic gem drops");
        RecommendationEditing.PlaceGem(build, d, 3, new GuideItem { ResourceHint = "Gem_IdentityOther" });
        var firstIdentityGem = d.GemSlots[0];
        var lastIdentityGem = d.GemSlots[3];
        Check(RecommendationEditing.PlaceGem(build, d, 3, d.GemSlots[0].Gem, d, 0) &&
            ReferenceEquals(d.GemSlots[3], firstIdentityGem) && ReferenceEquals(d.GemSlots[0], lastIdentityGem),
            "gems can exchange within identity memory without moving the skill");
        var beforeSwap = d.GemSlots[3];
        var beforeOtherSwap = w.GemSlots[2];
        Check(RecommendationEditing.PlaceGem(build, w, 2, d.GemSlots[3].Gem, d, 3) &&
            ReferenceEquals(w.GemSlots[2], beforeSwap) && ReferenceEquals(d.GemSlots[3], beforeOtherSwap) && d.Skill == identity,
            "gems can exchange between identity and other skills while D stays fixed");
        Check(!RecommendationEditing.PlaceGemOnSkill(build, q, new GuideItem { ResourceHint = "Gem_Identity" }) && q.GemSlots.All(g => g == null),
            "automatic gem drop checks uniqueness against D");
        var foreign = new BuildRecommendations().Get("Q");
        Check(!RecommendationEditing.PlaceGem(build, foreign, 0, gem) && !RecommendationEditing.PlaceGem(build, q, 0, gem, foreign, 0),
            "gem editing rejects targets and sources outside the current build");
        Check(RecommendationEditing.Remove(r, -1, r.Skill) && r.Skill == null && r.GemSlots.All(g => g == null) && !r.IsCore, "removing skill clears its four gem slots and marker");
    }

    // 图鉴分类保持游戏资源顺序，搜索或语言变化后仍保留正确分区和身份记忆限制。
    private static void CatalogueTests()
    {
        Reset();
        DewLocalization.data = DewLocalization.buildData.dataByLanguage["zh-CN"];
        DewLocalization.data.ui["InGame_Tooltip_Rarity_Common"] = "通用";
        DewLocalization.data.ui["InGame_Tooltip_Rarity_Rare"] = "稀有";
        DewLocalization.data.ui["InGame_Tooltip_Rarity_Character"] = "英雄";
        DewLocalization.data.ui["InGame_Tooltip_Rarity_Identity"] = "身份";
        var hero = (Hero_Test)DewResources.Assets["Hero_Test"];
        hero.Skills.Options[HeroSkillLocation.Q] = new[] { DewResources.Assets["St_Q_Second"] as SkillTrigger, DewResources.Assets["St_Q_First"] as SkillTrigger };
        foreach (var skill in hero.Skills.Options.Values.SelectMany(s => s)) skill.rarity = Rarity.Character;
        ((SkillTrigger)DewResources.Assets["St_I_First"]).rarity = Rarity.Identity;
        var other = new Hero_Other();
        other.Skills.Options[HeroSkillLocation.Q] = new SkillTrigger[] { new St_Q_Foreign { rarity = Rarity.Character } };
        DewResources.Assets["Hero_Other"] = other;
        DewResources.Assets["St_Q_Foreign"] = other.Skills.Options[HeroSkillLocation.Q][0];
        Dew.allHeroes.Add(typeof(Hero_Other));
        DewResources.Assets["St_Normal_Z"] = new SkillTrigger { rarity = Rarity.Common };
        DewResources.Assets["St_Normal_A"] = new SkillTrigger { rarity = Rarity.Rare };
        var options = new[] { "St_I_First", "St_Q_First", "St_R_First", "St_Normal_A", "St_Q_Foreign", "St_Q_Second", "St_Normal_Z" }
            .Select(key => new GuideItem { ResourceHint = key, Name = "任意名称" }).ToList();
        var groups = RecommendationCatalog.Groups(options, false);
        Check(groups.Select(g => g.Rarity).SequenceEqual(new Rarity?[] { Rarity.Common, Rarity.Rare, Rarity.Character, Rarity.Identity }),
            "memory catalogue separates common rare hero and identity rarity categories");
        Check(groups[0].Items.Single().ResourceHint == "St_Normal_Z" && groups[1].Items.Single().ResourceHint == "St_Normal_A" &&
            groups[0].Children.Count == 0 && groups[1].Children.Count == 0,
            "rare memories are not merged into common memories or hero sections");
        Check(groups[2].Children.Select(g => g.HeroKey).SequenceEqual(new[] { "Hero_Test", "Hero_Other" }) &&
            groups[2].Children[0].Items.Select(i => i.ResourceHint).SequenceEqual(new[] { "St_Q_Second", "St_Q_First", "St_R_First" }) &&
            groups[3].Children[0].Items.Single().ResourceHint == "St_I_First",
            "hero subcategories retain native hero and Q/R ordering with identity in its own rarity category");
        Check(!RecommendationEditing.CanDragSkill(groups[3].Children[0].Items.Single()) && RecommendationEditing.CanDragSkill(groups[2].Children[0].Items.First()),
            "identity rarity candidates are read-only while ordinary hero memories remain draggable");
        var filtered = RecommendationCatalog.Groups(options.Where(i => i.ResourceHint == "St_Q_Foreign"), false);
        Check(filtered.Count == 1 && filtered[0].Rarity == Rarity.Character && filtered[0].Children.Single().HeroKey == "Hero_Other" && filtered[0].Children[0].Items.Count == 1,
            "search results preserve original category and omit empty sections");
        Check(groups[0].Title == "通用" && groups[1].Title == "稀有" && groups[2].Title == "英雄" && groups[3].Title == "身份" &&
            groups[2].Children[0].Title == "测试英雄", "memory rarity headings and nested hero names use native translations");
        DewResources.Assets["St_Epic"] = new SkillTrigger { rarity = Rarity.Epic };
        DewResources.Assets["St_Legendary"] = new SkillTrigger { rarity = Rarity.Legendary };
        DewResources.Assets["St_Unique"] = new SkillTrigger { rarity = Rarity.Unique };
        DewResources.Assets["St_ModHero"] = new SkillTrigger { rarity = Rarity.Character };
        var extended = RecommendationCatalog.Groups(options.Concat(new[] { "St_Epic", "St_Legendary", "St_Unique", "St_ModHero" }
            .Select(key => new GuideItem { ResourceHint = key })), false);
        Check(extended.Select(g => g.Rarity).SequenceEqual(new Rarity?[] { Rarity.Common, Rarity.Rare, Rarity.Epic, Rarity.Legendary, Rarity.Character, Rarity.Identity, Rarity.Unique }),
            "all native memory rarity values have separate categories in game order");
        Check(extended.Single(g => g.Rarity == Rarity.Character).Items.Single().ResourceHint == "St_ModHero",
            "hero-rarity mod memory without hero mapping stays visible in hero category");
        ((SkillTrigger)DewResources.Assets["St_Q_First"]).rarity = Rarity.Rare;
        var reclassified = RecommendationCatalog.Groups(options, false);
        Check(reclassified.Single(g => g.Rarity == Rarity.Rare).Items.Any(i => i.ResourceHint == "St_Q_First") &&
            reclassified.Single(g => g.Rarity == Rarity.Character).Children[0].Items.All(i => i.ResourceHint != "St_Q_First"),
            "memory rarity takes precedence over loadout mapping, including extended hero catalogues");
        ((SkillTrigger)DewResources.Assets["St_Q_First"]).rarity = Rarity.Character;
        DewLocalization.data = new PerLanguageLocalizationData();
        DewLocalization.data.ui["Hero_Test_Name"] = "Test Hero";
        DewLocalization.data.ui["InGame_Tooltip_Rarity_Common"] = "Common";
        DewLocalization.data.ui["InGame_Tooltip_Rarity_Character"] = "Hero";
        Check(groups[2].Title == "Hero" && groups[2].Children[0].Title == "Test Hero", "rarity and nested hero headings both resolve current language without cached text");
        DewResources.Assets["Gem_Z"] = new Gem { rarity = Rarity.Common };
        DewResources.Assets["Gem_A"] = new Gem { rarity = Rarity.Legendary };
        DewResources.Assets["Gem_B"] = new Gem { rarity = Rarity.Common };
        var gems = RecommendationCatalog.Groups(new[] { "Gem_A", "Gem_Z", "Gem_B" }.Select(key => new GuideItem { ResourceHint = key }), true);
        Check(gems.Count == 2 && gems[0].Rarity == Rarity.Common && gems[1].Rarity == Rarity.Legendary &&
            gems[0].Items.Select(i => i.ResourceHint).SequenceEqual(new[] { "Gem_B", "Gem_Z" }),
            "gem catalogue groups by native rarity and sorts resource keys within rarity");
        Check(gems[0].Title == "Common", "gem category heading reuses native rarity localization");
        Check(RecommendationCatalog.Groups(Array.Empty<GuideItem>(), false).Count == 0 &&
            RecommendationCatalog.Groups(Array.Empty<GuideItem>(), true).Count == 0, "empty memory and gem searches contain no category headings");
        DewResources.Assets["St_M_Test"] = new St_M_Test();
        DewResources.Assets["St_ExcludedTest"] = new St_ExcludedTest { excludeFromPool = true };
        var keys = BuildExporter.RecommendationOptions(false).Select(i => i.ResourceHint).ToArray();
        Check(!keys.Contains("St_M_Test") && !keys.Contains("St_ExcludedTest") && keys.Contains("St_I_First"),
            "candidate visibility follows native movement and excluded-skill filters while keeping identity available to inspect");
        Reset();
    }

    // 栏位提示只读取当前账号的本地作品关联，覆盖模式隔离、缺失记录和歧义处理。
    private static void LocalPageTests()
    {
        string root = Path.Combine(Path.GetTempPath(), "BD-Assistant-pages-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            void Record(string folder, string id)
            {
                string path = Path.Combine(root, folder);
                Directory.CreateDirectory(path);
                File.WriteAllText(Path.Combine(path, "publishedfileid.txt"), id, new UTF8Encoding(false));
            }
            Record("Hero_Test-0", "101");
            Record("DewIdentityChange_Hero_Test-3", "102");
            Record("Hero_Other-1", "103");
            Record("Hero_Test-4", "invalid");
            Record("Hero_Test--1", "104");
            Record("Hero_Test-6", new string('1', 65));
            var own = new GuideSummary { Page = "workshop:101", Hero = "Hero_Test", OwnerSteamId = 10 };
            var identity = new GuideSummary { Page = "workshop:102", Hero = "Hero_Test", OwnerSteamId = 10, RequiresIdentityChange = true };
            var foreign = new GuideSummary { Page = "workshop:101", Hero = "Hero_Test", OwnerSteamId = 20 };
            var missing = new GuideSummary { Page = "workshop:105", Hero = "Hero_Test", OwnerSteamId = 10 };
            var wrongHero = new GuideSummary { Page = "workshop:103", Hero = "Hero_Test", OwnerSteamId = 10 };
            var negative = new GuideSummary { Page = "workshop:104", Hero = "Hero_Test", OwnerSteamId = 10 };
            var guides = new[] { own, identity, foreign, missing, wrongHero, negative };
            WorkshopLocalPages.Annotate(guides, root, 10);
            Check(own.IsOwned && own.LocalUploadSlot == 0, "own Build resolves zero-based local upload page for one-based display");
            Check(identity.IsOwned && identity.LocalUploadSlot == 3, "identity Build resolves its separate storage page");
            Check(!foreign.IsOwned && foreign.LocalUploadSlot == -1, "other authors never display local upload pages even for matching item ID");
            Check(missing.IsOwned && missing.LocalUploadSlot == -1, "own Build without local record remains explicitly unlinked");
            Check(wrongHero.LocalUploadSlot == -1 && negative.LocalUploadSlot == -1, "wrong hero and invalid page folders do not create links");
            identity.RequiresIdentityChange = false;
            WorkshopLocalPages.Annotate(guides, root, 10);
            Check(identity.LocalUploadSlot == -1, "normal Build does not read identity storage page");
            Record("Hero_Test-2", "101");
            WorkshopLocalPages.Annotate(guides, root, 10);
            Check(own.LocalUploadSlot == -1, "conflicting local page associations remain unlinked instead of choosing arbitrary page");
            WorkshopLocalPages.Annotate(guides, Path.Combine(root, "missing"), 10);
            Check(guides.All(g => g.LocalUploadSlot == -1) && own.IsOwned, "missing local directory preserves ownership and clears stale page labels");
            WorkshopLocalPages.Annotate(guides, root, 20);
            Check(!own.IsOwned && foreign.IsOwned, "account switch recomputes ownership without retaining stale page labels");
            WorkshopLocalPages.Annotate(guides, root, 0);
            Check(guides.All(g => !g.IsOwned && g.LocalUploadSlot == -1), "unset account never claims local upload ownership");
        }
        finally { Directory.Delete(root, true); }
    }
    private static void SchemaTests(string fixtures)
    {
        var sample = JObject.Parse(File.ReadAllText(Path.Combine(fixtures, "build-v5.json"), Encoding.UTF8));
        var guide = WorkshopBuildCodec.Decode(Encoding.UTF8.GetBytes(sample.ToString()));
        guide.Page = "workshop:1";
        Check(guide.Hero == "Hero_Test" && guide.PresentFields.Count == 8, "current fixture contains hero and eight lobby fields");
        Check(guide.Title == "当前结构测试" && guide.Summary == "中文说明", "fixture preserves UTF-8 metadata");
        Check(guide.Recommendations.Skills.Select(s => s.Slot).SequenceEqual(BuildRecommendations.SlotKeys), "fixture uses fixed QWERD positions");
        Check(guide.Recommendations.Skills.All(s => s.GemSlots.Count == 4), "each skill has four positional gem slots");
        Check(guide.Recommendations.Get("Q").IsCore && !guide.Recommendations.Get("Q").GemSlots[0].IsCore, "skill and gem priority can differ");
        Check(guide.Recommendations.Get("D").Skill == null && guide.Recommendations.Get("D").GemSlots.All(g => g == null), "empty skill and gem slots survive decode");
        Check(WorkshopBuildCodec.Decode(WorkshopBuildCodec.Encode(guide)).Hero == guide.Hero, "current fixture round trips");
        var reordered = (JObject)sample.DeepClone();
        reordered["Build"]["Skills"] = new JArray(((JArray)reordered["Build"]["Skills"]).Reverse());
        Check(WorkshopBuildCodec.Decode(Encoding.UTF8.GetBytes(reordered.ToString())).Recommendations.Skills.Select(s => s.Slot).SequenceEqual(BuildRecommendations.SlotKeys), "decoder normalizes skill order to QWERD");
        void Reject(string description, Action<JObject> change)
        {
            var candidate = (JObject)sample.DeepClone();
            change(candidate);
            bool rejected = false;
            try { WorkshopBuildCodec.Decode(Encoding.UTF8.GetBytes(candidate.ToString())); } catch (FormatException) { rejected = true; }
            Check(rejected, description);
        }
        foreach (int version in new[] { 0, 1, 2, 3, 4, 6, 999 }) Reject("non-current version rejected: " + version, d => d["Version"] = version);
        Reject("missing version rejected", d => d.Remove("Version"));
        Reject("string version rejected", d => d["Version"] = "5");
        Reject("decimal version rejected", d => d["Version"] = 5.0);
        Reject("wrong format rejected", d => d["Format"] = "other");
        Reject("missing build rejected", d => d.Remove("Build"));
        Reject("missing skills rejected", d => ((JObject)d["Build"]).Remove("Skills"));
        Reject("missing clear flag rejected", d => ((JObject)d["Build"]).Remove("ClearDejavu"));
        Reject("missing mode flag rejected", d => ((JObject)d["Build"]).Remove("RequiresIdentityChange"));
        Reject("string mode flag rejected", d => d["Build"]["RequiresIdentityChange"] = "true");
        Reject("null mode flag rejected", d => d["Build"]["RequiresIdentityChange"] = JValue.CreateNull());
        Reject("localized hero rejected", d => d["Build"]["HeroKey"] = "测试英雄");
        Reject("localized item rejected", d => d["Build"]["Items"]["memoryQ"][0] = "替换记忆");
        Reject("legacy item object rejected", d => d["Build"]["Items"]["memoryQ"][0] = new JObject { ["Name"] = "替换记忆", ["ResourceHint"] = "St_Q_Second" });
        Reject("numeric key rejected", d => d["Build"]["Items"]["memoryQ"][0] = 123);
        Reject("wrong item type rejected", d => d["Build"]["Items"]["memoryQ"][0] = "Gem_Test");
        Reject("localized item field rejected", d => ((JObject)d["Build"]["Items"]).Add("旅行者记忆1", new JArray("St_Q_Second")));
        Reject("legacy guide model rejected", d => d["Build"] = JObject.FromObject(new GuideData { Hero = "Hero_Test", Title = "test" }));
        Reject("sixth skill rejected", d => ((JArray)d["Build"]["Skills"]).Add(d["Build"]["Skills"][0].DeepClone()));
        Reject("duplicate skill key position rejected", d => d["Build"]["Skills"][1]["Slot"] = "Q");
        Reject("unknown skill position rejected", d => d["Build"]["Skills"][1]["Slot"] = "M");
        Reject("fifth gem slot rejected", d => ((JArray)d["Build"]["Skills"][0]["GemSlots"]).Add(JValue.CreateNull()));
        Reject("missing skill marker rejected", d => ((JObject)d["Build"]["Skills"][0]).Remove("IsCore"));
        Reject("missing gem marker rejected", d => ((JObject)d["Build"]["Skills"][0]["GemSlots"][0]).Remove("IsCore"));
        Reject("core empty skill rejected", d => d["Build"]["Skills"][4]["IsCore"] = true);
        var gemsOnly = (JObject)sample.DeepClone();
        gemsOnly["Build"]["Skills"][4]["GemSlots"][0] = new JObject { ["GemKey"] = "Gem_Identity", ["IsCore"] = false };
        var gemsOnlyGuide = WorkshopBuildCodec.Decode(Encoding.UTF8.GetBytes(gemsOnly.ToString()));
        Check(gemsOnlyGuide.Recommendations.Get("D").Skill == null && gemsOnlyGuide.Recommendations.Get("D").GemSlots[0] != null,
            "current JSON accepts gems without a selected skill");
        Reject("duplicate gem within skill rejected", d => d["Build"]["Skills"][0]["GemSlots"][1] = d["Build"]["Skills"][0]["GemSlots"][0].DeepClone());
        Reject("duplicate gem across skills rejected", d => d["Build"]["Skills"][3]["GemSlots"][0] = d["Build"]["Skills"][0]["GemSlots"][0].DeepClone());
        Reject("duplicate gem on empty skill rejected", d => d["Build"]["Skills"][4]["GemSlots"][2] = d["Build"]["Skills"][0]["GemSlots"][0].DeepClone());
    }
    private static void WorkshopTests()
    {
        Reset();
        var settings = NetworkedManagerBase<GameSettingsManager>.softInstance;
        var current = DewSave.profileMain.heroLoadouts["Hero_Test"][1];
        current.SetSkill(HeroSkillLocation.Q, 1);
        current.GetStarList(StarType.Destruction)[0] = new LoadoutStarItem { name = "Se_Star_Test", level = 5 };
        settings.Preferred.dejavuItem = "Gem_Test";
        var exported = BuildExporter.Capture("中文构筑", "测试思路", "作者");
        var bytes = WorkshopBuildCodec.Encode(exported);
        var decoded = WorkshopBuildCodec.Decode(bytes);
        var document = JObject.Parse(Encoding.UTF8.GetString(bytes));
        Check((int)document["Version"] == WorkshopBuildCodec.Version && (string)document["Build"]["HeroKey"] == "Hero_Test", "current version stores hero resource key");
        Check(!(bool)document["Build"]["RequiresIdentityChange"] && !decoded.RequiresIdentityChange, "normal upload explicitly stores false identity requirement");
        Check((string)document["Build"]["Items"]["memoryQ"][0] == "St_Q_Second", "wire memory entry is a resource key string");
        Check(document["Build"]["Items"]["旅行者记忆1"] == null && document["Build"]["Details"] == null && document["Build"]["DejavuKind"] == null,
            "wire schema excludes localized field names and generated UI text");
        Check(document["Build"]["Slot"] == null && document["Build"]["Page"] == null && document["Build"]["Loadout"] == null,
            "wire schema excludes target page and raw loadout data");
        Check(!Encoding.UTF8.GetString(bytes).Contains("测试星座") && !Encoding.UTF8.GetString(bytes).Contains("替换记忆"),
            "wire data does not contain localized resource names");
        exported.Items["旅行者记忆1"][0].Name = "another language";
        Check(bytes.SequenceEqual(WorkshopBuildCodec.Encode(exported)), "changing UI labels does not change Build JSON");
        var broken = WorkshopBuildCodec.Decode(bytes);
        broken.Items["旅行者记忆1"][0].ResourceHint = "Missing_Skill";
        broken.Items["旅行者记忆1"][0].Name = "替换记忆";
        Check(BuildApplicator.Prepare(broken).Errors.Count > 0, "missing skill key does not fall back to matching UI name");
        broken = WorkshopBuildCodec.Decode(bytes);
        broken.Items["破坏星座"][0].ResourceHint = "Missing_Star";
        broken.Items["破坏星座"][0].Name = "测试星座";
        Check(BuildApplicator.Prepare(broken).Errors.Count > 0, "missing star key does not fall back to matching UI name");
        Check(WorkshopBuildCodec.Tag == "build", "Workshop uses the exact build tag");
        Check(decoded.Title == "中文构筑" && decoded.Summary == "测试思路", "UTF-8 Build round trip");
        Check(decoded.GetItems("旅行者记忆1").Single().ResourceHint == "St_Q_Second", "exports actual selected memory resource");
        Check(decoded.GetItems("破坏星座").Single().ResourceHint == "Se_Star_Test", "exports star resource without purchased levels");
        Check(decoded.GetItems("既视感").Single().ResourceHint == "Gem_Test", "exports deja vu");
        Check(decoded.PresentFields.Count == 8 && decoded.Items["生命星座"].Count == 0, "explicit empty star groups survive JSON round trip");
        current.SetSkill(HeroSkillLocation.Q, 0);
        var plan = BuildApplicator.Prepare(decoded);
        Check(plan.Errors.Count == 0 && plan.Loadout.GetSkill(HeroSkillLocation.Q) == 1, "Workshop resource IDs prepare correctly");
        BuildApplicator.Commit(plan);
        Check(DewSave.profileMain.heroLoadouts["Hero_Test"][1].GetSkill(HeroSkillLocation.Q) == 1, "Workshop applies to selected page");
        Check(DewSave.profileMain.heroLoadouts["Hero_Test"][0].GetSkill(HeroSkillLocation.Q) == 0, "Workshop preserves other pages");
        // 同一配装来自哪个栏位都不会影响上传内容，下载后只使用接收者当前选中的栏位。
        settings.Preferred.heroSelectedLoadoutIndex["Hero_Test"] = 3;
        DewSave.profileMain.heroLoadouts["Hero_Test"][3] = new HeroLoadoutData(DewSave.profileMain.heroLoadouts["Hero_Test"][1]);
        Check(bytes.SequenceEqual(WorkshopBuildCodec.Encode(BuildExporter.Capture("中文构筑", "测试思路", "作者"))),
            "same build on another upload page produces identical JSON");
        var untouched = DewSave.profileMain.heroLoadouts["Hero_Test"][1];
        document["Build"]["Slot"] = 0;
        var receiverPlan = BuildApplicator.Prepare(WorkshopBuildCodec.Decode(Encoding.UTF8.GetBytes(document.ToString())));
        Check(receiverPlan.Errors.Count == 0 && receiverPlan.Slot == 3, "downloaded page value is ignored in favor of receiver selection");
        BuildApplicator.Commit(receiverPlan);
        Check(ReferenceEquals(untouched, DewSave.profileMain.heroLoadouts["Hero_Test"][1]), "applying downloaded build preserves uploader page");
        settings.Preferred.heroSelectedLoadoutIndex["Hero_Test"] = 1;
        settings.Preferred.dejavuItem = null;
        var empty = WorkshopBuildCodec.Decode(WorkshopBuildCodec.Encode(BuildExporter.Capture("空既视感", "", "作者")));
        settings.Preferred.dejavuItem = "Gem_Test";
        var clear = BuildApplicator.Prepare(empty);
        Check(clear.Errors.Count == 0 && clear.SetDejavu && clear.Dejavu == null && clear.DejavuCost == 0, "explicit empty Workshop deja vu clears selection");
        BuildApplicator.Commit(clear);
        Check(settings.Preferred.dejavuItem == null, "cleared deja vu saved and synced");
        bool rejected = false;
        try { WorkshopBuildCodec.Decode(new byte[WorkshopBuildCodec.MaxBytes + 1]); } catch (FormatException) { rejected = true; }
        Check(rejected, "oversized Workshop Build rejected");
        rejected = false;
        try { WorkshopBuildCodec.Decode(Encoding.UTF8.GetBytes("{\"Version\":999}")); } catch (FormatException) { rejected = true; }
        Check(rejected, "unsupported schema version rejected");
        rejected = false;
        try { WorkshopBuildCodec.Decode(new byte[] { 0xff, 0xfe }); } catch (DecoderFallbackException) { rejected = true; }
        Check(rejected, "invalid UTF-8 rejected");
        exported.Items["破坏星座"][0].ResourceHint = "Se_Missing_Resource";
        exported.Items["破坏星座"][0].Name = "Missing Resource";
        Check(BuildApplicator.Prepare(WorkshopBuildCodec.Decode(WorkshopBuildCodec.Encode(exported))).Errors.Count > 0,
            "missing downloaded resource preserves original page");
        exported.PresentFields.Add("unknown");
        rejected = false;
        try { WorkshopBuildCodec.Encode(exported); } catch (FormatException) { rejected = true; }
        Check(rejected, "unknown Build fields rejected");
    }

    private static void FilterTests()
    {
        var ownPrivate = new GuideSummary { Page = "workshop:1", OwnerSteamId = 10, IsPublic = false, Hero = "Hero_Test", Title = "test", Author = "author", RequiresIdentityChange = true };
        var ownPublic = new GuideSummary { Page = "workshop:2", OwnerSteamId = 10, IsPublic = true };
        var otherPublic = new GuideSummary { Page = "workshop:3", OwnerSteamId = 20, IsPublic = true };
        var otherPrivate = new GuideSummary { Page = "workshop:4", OwnerSteamId = 20, IsPublic = false };
        var records = new[] { ownPrivate, ownPublic, otherPublic, otherPrivate, ownPublic };
        var mine = WorkshopGuideFilter.Select(records, WorkshopFilter.Mine, 10);
        Check(mine.Count == 2 && mine.Contains(ownPrivate) && mine.Contains(ownPublic), "mine includes own private and public builds without duplicates");
        var publicBuilds = WorkshopGuideFilter.Select(records, WorkshopFilter.Public, 10);
        Check(publicBuilds.Count == 2 && publicBuilds.Contains(ownPublic) && publicBuilds.Contains(otherPublic), "public includes only publicly visible builds");
        var all = WorkshopGuideFilter.Select(records, WorkshopFilter.All, 10);
        Check(all.Count == 3 && all.Contains(ownPrivate) && !all.Contains(otherPrivate), "all merges public builds and own drafts without foreign private entries");
        Check(WorkshopGuideFilter.Select(Array.Empty<GuideSummary>(), WorkshopFilter.Mine, 10).Count == 0, "empty personal list supported");
        var oldPublic = new GuideSummary { Page = "workshop:1", OwnerSteamId = 10, IsPublic = true };
        var updated = new[] { oldPublic, ownPrivate };
        Check(WorkshopGuideFilter.Select(updated, WorkshopFilter.Public, 10).Count == 0, "fresh owner visibility supersedes stale public result");
        Check(ReferenceEquals(WorkshopGuideFilter.Select(updated, WorkshopFilter.All, 10).Single(), ownPrivate), "all prefers current owner result");
    }

    private static void RecommendationTests()
    {
        Reset();
        var guide = BuildExporter.Capture("推荐测试", "说明", "作者");
        var baseline = BuildApplicator.Prepare(guide);
        var q = guide.Recommendations.Get("Q");
        q.Skill = new GuideItem { Name = "任意显示名", ResourceHint = "St_Q_Second" };
        q.IsCore = false;
        for (int i = 0; i < 4; i++) q.GemSlots[i] = new BuildGem { Gem = new GuideItem { Name = "精粹显示名", ResourceHint = i == 0 ? "Gem_Test" : "Gem_Missing" + i }, IsCore = i % 2 == 0 };
        var d = guide.Recommendations.Get("D");
        d.Skill = new GuideItem { Name = "身份", ResourceHint = "St_I_First" };
        d.IsCore = true;
        d.GemSlots[0] = new BuildGem { Gem = new GuideItem { ResourceHint = "Gem_Identity" } };
        byte[] bytes = WorkshopBuildCodec.Encode(guide);
        var document = JObject.Parse(Encoding.UTF8.GetString(bytes));
        Check(document["Build"]["CoreBuild"] == null && document["Build"]["ExtraBuild"] == null, "JSON contains no core/extra sections");
        Check((string)document["Build"]["Skills"][0]["Slot"] == "Q" && (string)document["Build"]["Skills"][0]["SkillKey"] == "St_Q_Second", "skill JSON stores position and resource key");
        Check((string)document["Build"]["Skills"][0]["GemSlots"][3]["GemKey"] == "Gem_Missing3", "fourth gem uses its resource key");
        Check(!(bool)document["Build"]["Skills"][0]["IsCore"] && (bool)document["Build"]["Skills"][0]["GemSlots"][0]["IsCore"], "JSON stores independent skill and gem markers");
        Check(!Encoding.UTF8.GetString(bytes).Contains("精粹显示名") && !Encoding.UTF8.GetString(bytes).Contains("任意显示名"), "recommendation JSON excludes UI labels");
        var loaded = WorkshopBuildCodec.Decode(bytes);
        Check(loaded.Recommendations.Skills.Count == 5 && loaded.Recommendations.Get("W").Skill == null, "five positions including empty skills round trip");
        Check(loaded.Recommendations.Get("Q").GemSlots.Count == 4 && loaded.Recommendations.Get("D").GemSlots[3] == null, "four gem positions including empty ones round trip");
        BuildExporter.LocalizeForDisplay(loaded);
        Check(loaded.Recommendations.Get("Q").Skill.Name == "替换记忆" && loaded.Recommendations.Get("Q").GemSlots[0].Gem.Name == "测试精粹", "recommendation names resolve locally");
        Check(loaded.Recommendations.Get("Q").GemSlots[1].Gem.Name == "Gem_Missing1", "unknown recommendation resource displays key");
        var plan = BuildApplicator.Prepare(loaded);
        Check(plan.Errors.Count == baseline.Errors.Count && plan.Loadout.GetSkill(HeroSkillLocation.Q) == baseline.Loadout.GetSkill(HeroSkillLocation.Q), "QWERD advice does not equip run loot or alter lobby preflight");
        guide.Recommendations.Skills.Reverse();
        Check(bytes.SequenceEqual(WorkshopBuildCodec.Encode(guide)), "encoder always emits game QWERD order");
        q.GemSlots[3].Gem.ResourceHint = "Gem_Test";
        bool rejected = false;
        try { WorkshopBuildCodec.Encode(guide); } catch (FormatException) { rejected = true; }
        Check(rejected, "encoder rejects duplicate gems in one skill");
        q.GemSlots[3].Gem.ResourceHint = "Gem_Missing3";
        d.GemSlots[0].Gem.ResourceHint = "Gem_Test";
        rejected = false;
        try { WorkshopBuildCodec.Encode(guide); } catch (FormatException) { rejected = true; }
        Check(rejected, "encoder rejects gem repeated on another skill");
        d.GemSlots[0].Gem.ResourceHint = "Gem_Identity";
        q.Skill = null;
        var emptySkill = WorkshopBuildCodec.Decode(WorkshopBuildCodec.Encode(guide)).Recommendations.Get("Q");
        Check(emptySkill.Skill == null && emptySkill.GemSlots.Count(g => g != null) == 4 && emptySkill.GemSlots[0].IsCore,
            "encoder preserves four gems and gem core markers without a skill");
    }
    private static void Reset()
    {
        Dew.allHeroes.Clear();
        Dew.allHeroes.Add(typeof(Hero_Test));
        DewIdentityChange.config.LoadoutPageMapper.Enabled = false;
        DewSave.profileMain = new DewProfile();
        DewSave.profileStats = new DewProfileStats();
        DewSave.Saves = 0;
        DewPlayer.local = new DewPlayer { selectedHeroType = "Hero_Test", state = PlayerState.InLobby };
        NetworkedManagerBase<GameSettingsManager>.softInstance = new GameSettingsManager();
        NetworkedManagerBase<GameSettingsManager>.softInstance.Preferred.heroSelectedLoadoutIndex["Hero_Test"] = 1;
        DewSave.profileMain.heroLoadouts["Hero_Test"] = Enumerable.Range(0, 5).Select(_ => new HeroLoadoutData()).ToList();
        DewSave.profileMain.heroUnlockedStarSlots["Hero_Test"] = new DewProfile.HeroStarSlotUnlockData();
        SingletonBehaviour<UI_Lobby_Constellations>.softInstance = null;
        DewResources.Assets.Clear();
        var hero = new Hero_Test();
        hero.Skills.Options[HeroSkillLocation.Q] = new SkillTrigger[] { new St_Q_First(), new St_Q_Second() };
        hero.Skills.Options[HeroSkillLocation.R] = new SkillTrigger[] { new St_R_First() };
        hero.Skills.Options[HeroSkillLocation.Identity] = new SkillTrigger[] { new St_I_First() };
        DewResources.Assets["Hero_Test"] = hero;
        foreach (var skill in hero.Skills.Options.Values.SelectMany(options => options))
            DewResources.Assets[skill.GetType().Name] = skill;
        DewResources.Assets["Gem_Test"] = new Gem_Test();
        DewResources.Assets["Se_Star_Test"] = new Se_Star_Test();
        DewSave.profileMain.newStars["Se_Star_Test"] = new DewProfile.StarData();
        foreach (string skill in new[] { "St_Q_First", "St_Q_Second", "St_R_First", "St_I_First" })
            DewSave.profileMain.skills[skill] = new DewProfile.UnlockData();
        DewSave.profileMain.gems["Gem_Test"] = new DewProfile.UnlockData();
        var zh = new PerLanguageLocalizationData();
        zh.ui["Hero_Test_Name"] = "测试英雄";
        zh.stars["Se_Star_Test"] = new StarData { name = "测试星座" };
        zh.gems["Test"] = new GemData { name = "测试精粹" };
        zh.skills["Q_First"] = Skill("初始记忆");
        zh.skills["Q_Second"] = Skill("替换记忆");
        zh.skills["R_First"] = Skill("记忆二");
        zh.skills["I_First"] = Skill("身份记忆");
        DewLocalization.buildData.dataByLanguage["zh-CN"] = zh;
        DewLocalization.data = new PerLanguageLocalizationData(); // 当前语言无需中文。
    }

    private static SkillData Skill(string name) => new() { configs = new List<SkillConfigData> { new() { name = name } } };
    private static GuideData Guide(bool memoryOnly = false)
    {
        var guide = new GuideData { Hero = "Hero_Test", Title = "测试构筑" };
        guide.PresentFields.Add("旅行者记忆1");
        guide.Items["旅行者记忆1"] = new List<GuideItem> { new() { Name = "替换记忆", ResourceHint = "St_Q_Second" } };
        if (!memoryOnly)
        {
            guide.PresentFields.UnionWith(new[] { "破坏星座", "既视感" });
            guide.Items["破坏星座"] = new List<GuideItem> { new() { Name = "测试星座", ResourceHint = "Se_Star_Test" } };
            guide.Items["既视感"] = new List<GuideItem> { new() { Name = "测试精粹", ResourceHint = "Gem_Test" } };
        }
        return guide;
    }

    private static void ApplicationTests()
    {
        Reset();
        var pages = DewSave.profileMain.heroLoadouts["Hero_Test"];
        var original = pages.ToArray();
        var plan = BuildApplicator.Prepare(Guide());
        Check(plan.Errors.Count == 0, "valid guide passes");
        Check(plan.Slot == 1 && plan.Loadout.GetSkill(HeroSkillLocation.Q) == 1, "current quick page and Q mapping");
        Check(pages[1].GetSkill(HeroSkillLocation.Q) == 0 && DewSave.Saves == 0, "preflight is read-only");
        BuildApplicator.Commit(plan);
        Check(!ReferenceEquals(pages[1], original[1]), "selected page replaced");
        Check(Enumerable.Range(0, 5).Where(i => i != 1).All(i => ReferenceEquals(pages[i], original[i])), "other pages preserved");
        Check(pages[1].GetStarList(StarType.Destruction)[0].level == 3, "owned star level used");
        Check(DewPlayer.local.Commands == 2 && DewPlayer.local.SentDejavu == "Gem_Test", "original game commands used");
        Check(DewSave.profileMain.stardust == 1000 && NetworkedManagerBase<GameSettingsManager>.softInstance.localPlayerDejavuCost == 75, "deja vu cost recorded without immediate deduction");

        Reset();
        var editor = new UI_Lobby_Constellations { isActiveAndEnabled = true, selectedLoadoutIndex = 4, loadout = new HeroLoadoutData() };
        editor.loadout.SetSkill(HeroSkillLocation.Movement, 7);
        SingletonBehaviour<UI_Lobby_Constellations>.softInstance = editor;
        plan = BuildApplicator.Prepare(Guide());
        Check(plan.Slot == 4 && plan.Loadout.GetSkill(HeroSkillLocation.Movement) == 7, "editor current page and unsaved unrelated content respected");
        BuildApplicator.Commit(plan);
        Check(DewSave.profileMain.heroLoadouts["Hero_Test"][4].GetSkill(HeroSkillLocation.Q) == 1, "editor page actually saved");

        Reset();
        plan = BuildApplicator.Prepare(Guide());
        NetworkedManagerBase<GameSettingsManager>.softInstance.Preferred.heroSelectedLoadoutIndex["Hero_Test"] = 3;
        bool rejected = false;
        try { BuildApplicator.Commit(plan); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && DewSave.Saves == 0 && DewPlayer.local.Commands == 0, "page changes invalidate stale plans");

        void Reject(string key, Action change, Action<GuideData> edit = null)
        {
            Reset(); change();
            var guide = Guide();
            edit?.Invoke(guide);
            var candidate = BuildApplicator.Prepare(guide);
            Check(candidate.Errors.Any(e => e.StartsWith(key + ":", StringComparison.Ordinal)), key + " reported");
            bool blocked = false;
            try { BuildApplicator.Commit(candidate); } catch (InvalidOperationException) { blocked = true; }
            Check(blocked && DewSave.Saves == 0 && DewPlayer.local.Commands == 0, key + " preserves saves and network state");
        }
        Reject("Locked", () => DewSave.profileMain.skills["St_Q_Second"].isAvailableInGame = false);
        Reject("Locked", () => DewSave.profileMain.newStars["Se_Star_Test"].level = 0);
        Reject("UnreadyFirst", () => DewPlayer.local.isReady = true);
        Reject("HeroMismatch", () => { }, g => g.Hero = "测试英雄");
        Reject("DejavuDisabled", () => NetworkedManagerBase<GameSettingsManager>.softInstance.allowDejavu = false);
        Reject("DejavuLocked", () => DewSave.profileStats.HasWins = false);
        Reject("DejavuCost", () => DewSave.profileMain.stardust = 0);
        Reject("Banned", () => NetworkedManagerBase<GameSettingsManager>.softInstance.bannedGameItems.Add("Gem_Test"));
        Reject("Capacity", () => { }, g => g.Items["破坏星座"] = Enumerable.Repeat(g.Items["破坏星座"][0], 3).ToList());
        Reject("DuplicateStar", () => { }, g => g.Items["破坏星座"].Add(g.Items["破坏星座"][0]));
        Reject("IncompatibleStar", () => ((Se_Star_Test)DewResources.Assets["Se_Star_Test"]).Incompatible = true);
        Reject("NotInLobby", () => NetworkedManagerBase<GameSettingsManager>.softInstance.state = GameState.Starting);
        Reject("Unresolved", () => { }, g => g.Items["旅行者记忆1"][0].ResourceHint = "St_Missing");
        Reset();
        plan = BuildApplicator.Prepare(Guide(true));
        Check(plan.Errors.Count == 0 && !plan.SetDejavu, "omitted deja vu retained");
        DewLocalization.buildData.dataByLanguage["zh-CN"].ui["Hero_Test_Name"] = "任意显示名";
        Check(BuildApplicator.Prepare(Guide()).Errors.Count == 0, "hero key matching is independent of display names");
        Reset();
        DewSave.profileMain.heroLoadouts["Hero_Test"][1].GetStarList(StarType.Life)[0] = new LoadoutStarItem { name = "Se_Star_Test", level = 3 };
        ((Se_Star_Test)DewResources.Assets["Se_Star_Test"]).Incompatible = true;
        plan = BuildApplicator.Prepare(Guide(true));
        Check(plan.Errors.Any(e => e.StartsWith("IncompatibleStar:", StringComparison.Ordinal)), "retained stars checked against changed memories");
        Reset();
        DewSave.profileMain.heroLoadouts["Hero_Test"][1].GetStarList(StarType.Life)[0] = new LoadoutStarItem { name = "Se_Star_Test", level = 0 };
        plan = BuildApplicator.Prepare(Guide(true));
        Check(plan.Loadout.GetStarList(StarType.Life)[0].level == 3, "name-only saved stars receive owned levels for validation");
        Check(DewSave.profileMain.heroLoadouts["Hero_Test"][1].GetStarList(StarType.Life)[0].level == 0, "level population preserves original saved page during preflight");
    }
    // 中途加入的全局状态为 InGame，本地仍在大厅时应显示入口并允许使用当前配装页。
    private static void MidJoinTests()
    {
        Reset();
        NetworkedManagerBase<GameSettingsManager>.softInstance.state = GameState.InGame;
        Check(BuildApplicator.TryGetTarget(out string hero, out int slot, out var current) &&
            hero == "Hero_Test" && slot == 1 && ReferenceEquals(current, DewSave.profileMain.heroLoadouts[hero][1]),
            "mid-join lobby exposes launcher target while room is already in game");
        Check(DewSave.Saves == 0 && DewPlayer.local.Commands == 0, "mid-join target lookup is read-only");
        var pages = DewSave.profileMain.heroLoadouts[hero];
        var original = pages.ToArray();
        var plan = BuildApplicator.Prepare(Guide());
        Check(plan.Errors.Count == 0 && plan.Slot == 1, "mid-join lobby permits normal build preflight");
        BuildApplicator.Commit(plan);
        Check(pages[1].GetSkill(HeroSkillLocation.Q) == 1 && DewPlayer.local.Commands == 2 && DewSave.Saves == 1,
            "mid-join build saves and uses native loadout and deja vu commands");
        Check(Enumerable.Range(0, 5).Where(i => i != 1).All(i => ReferenceEquals(pages[i], original[i])),
            "mid-join application preserves other loadout pages");

        Reset();
        NetworkedManagerBase<GameSettingsManager>.softInstance.state = GameState.InGame;
        SingletonBehaviour<UI_Lobby_Constellations>.softInstance = new UI_Lobby_Constellations
            { isActiveAndEnabled = true, selectedLoadoutIndex = 4, loadout = new HeroLoadoutData() };
        Check(BuildApplicator.Prepare(Guide()).Slot == 4, "mid-join lobby respects open constellation editor page");

        foreach (var state in new[] { PlayerState.None, PlayerState.Playing, PlayerState.Spectating })
        {
            Reset();
            NetworkedManagerBase<GameSettingsManager>.softInstance.state = GameState.InGame;
            plan = BuildApplicator.Prepare(Guide());
            DewPlayer.local.state = state;
            Check(!BuildApplicator.TryGetTarget(out _, out _, out _), state + " hides launcher target after leaving lobby");
            bool rejected = false;
            try { BuildApplicator.Commit(plan); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && DewSave.Saves == 0 && DewPlayer.local.Commands == 0,
                state + " invalidates pending mid-join application without writes");
        }
        Reset();
    }

    // 同一文本绑定接收多次语言回调，始终重新按 key 生成名称而不是翻译旧显示值。
    private static void LanguageTests()
    {
        Reset();
        string language = "zh-CN";
        int count = 1;
        var text = new UnityEngine.GameObject().AddComponent<TMPro.TMP_Text>();
        LocalizedTextBinding.Bind(text, () => (language == "zh-CN" ? "构筑 " : "Build ") + count);
        var binding = text.GetComponent<LocalizedTextBinding>();
        Check(text.text == "构筑 1", "text binding resolves initial language");
        language = "en-US";
        binding.OnLanguageChanged();
        Check(text.text == "Build 1", "existing text refreshes on native language callback");
        count = 3;
        language = "zh-CN";
        binding.OnLanguageChanged();
        Check(text.text == "构筑 3", "repeated language switches preserve resolver and current arguments");
        LocalizedTextBinding.Bind(text, () => language == "zh-CN" ? "精粹" : "Gems");
        language = "en-US";
        binding.OnLanguageChanged();
        Check(ReferenceEquals(binding, text.GetComponent<LocalizedTextBinding>()) && text.text == "Gems", "rebinding updates resolver without duplicate components");

        var guide = Guide();
        guide.Recommendations.Get("Q").Skill = new GuideItem { Name = "旧名称", ResourceHint = "St_Q_Second" };
        guide.Recommendations.Get("Q").GemSlots[0] = new BuildGem { Gem = new GuideItem { Name = "旧名称", ResourceHint = "Gem_Test" }, IsCore = true };
        byte[] before = WorkshopBuildCodec.Encode(guide);
        var english = new PerLanguageLocalizationData();
        english.skills["Q_Second"] = Skill("Second Memory");
        english.gems["Test"] = new GemData { name = "Test Gem" };
        DewLocalization.data = english;
        LocalizedTextBinding.Bind(text, () => BuildExporter.ResourceDisplayName("St_Q_Second"));
        BuildExporter.LocalizeForDisplay(guide);
        Check(text.text == "Second Memory" && guide.GetItems("旅行者记忆1")[0].Name == "Second Memory", "resource labels resolve active game language");
        Check(guide.Recommendations.Get("Q").Skill.Name == "Second Memory" && guide.Recommendations.Get("Q").GemSlots[0].Gem.Name == "Test Gem", "both recommendation resource kinds refresh language");
        DewLocalization.data = DewLocalization.buildData.dataByLanguage["zh-CN"];
        binding.OnLanguageChanged();
        BuildExporter.LocalizeForDisplay(guide);
        Check(text.text == "替换记忆" && guide.Recommendations.Get("Q").GemSlots[0].Gem.Name == "测试精粹", "resource labels switch back without stale cached display names");
        Check(before.SequenceEqual(WorkshopBuildCodec.Encode(guide)), "language refresh leaves upload resource keys and user metadata unchanged");
        var candidateKeys = BuildExporter.RecommendationOptions(false).Select(i => i.ResourceHint).OrderBy(k => k).ToArray();
        DewLocalization.data = english;
        Check(candidateKeys.Length > 0 && candidateKeys.SequenceEqual(BuildExporter.RecommendationOptions(false).Select(i => i.ResourceHint).OrderBy(k => k)), "candidate catalogue uses same resource keys across languages");
        Check(BuildExporter.RecommendationOptions(false).Any(i => i.ResourceHint == "St_Q_Second" && i.Name == "Second Memory"), "refreshed candidate catalogue uses current localized name");
    }

    // 转职使用独立存档页，技能索引由当前已扩展的候选目录按资源 key 重新查找。
    private static void IdentityChangeTests()
    {
        Reset();
        Check(!IdentityChangeCompatibility.Enabled && IdentityChangeCompatibility.StorageHero("Hero_Test") == "Hero_Test", "disabled Identity Change uses vanilla storage");
        var vanilla = DewSave.profileMain.heroLoadouts["Hero_Test"];
        var originalVanilla = vanilla.ToArray();
        var identity = Enumerable.Range(0, 5).Select(_ => new HeroLoadoutData()).ToList();
        DewSave.profileMain.heroLoadouts["DewIdentityChange_Hero_Test"] = identity;
        identity[1].SetSkill(HeroSkillLocation.Q, 2);
        identity[1].SetSkill(HeroSkillLocation.Movement, 7);
        var hero = (Hero_Test)DewResources.Assets["Hero_Test"];
        hero.Skills.Options[HeroSkillLocation.Q] = new SkillTrigger[] { new St_Q_First(), new St_Q_Second(), new St_Q_Foreign() };
        DewSave.profileMain.skills["St_Q_Foreign"] = new DewProfile.UnlockData();
        var star = (Se_Star_Test)DewResources.Assets["Se_Star_Test"];
        star.heroType = typeof(Hero_Other);
        star.Incompatible = true;
        DewIdentityChange.config.LoadoutPageMapper.Enabled = true;
        Check(IdentityChangeCompatibility.Enabled && IdentityChangeCompatibility.StorageHero("Hero_Test") == "DewIdentityChange_Hero_Test", "bridge follows current Identity Change mapper");
        var captured = BuildExporter.Capture("转职构筑", "跨角色", "作者");
        Check(captured.RequiresIdentityChange, "identity mode upload marks the Build requirement");
        Check(captured.Hero == "Hero_Test" && captured.GetItems("旅行者记忆1")[0].ResourceHint == "St_Q_Foreign", "export reads Identity Change storage and expanded skill catalogue");
        var bytes = WorkshopBuildCodec.Encode(captured);
        Check(WorkshopBuildCodec.Decode(bytes).RequiresIdentityChange, "identity requirement survives JSON round trip");
        Check(!Encoding.UTF8.GetString(bytes).Contains("DewIdentityChange_Hero_Test"), "identity storage key stays local");
        var guide = Guide();
        guide.RequiresIdentityChange = true;
        guide.Items["旅行者记忆1"][0].ResourceHint = "St_Q_Foreign";
        var plan = BuildApplicator.Prepare(guide);
        Check(plan.Errors.Count == 0 && plan.StorageHero == "DewIdentityChange_Hero_Test" && plan.Loadout.GetSkill(HeroSkillLocation.Q) == 2, "cross-hero memories and stars prepare with enabled mod");
        BuildApplicator.Commit(plan);
        Check(identity[1].GetSkill(HeroSkillLocation.Q) == 2 && identity[1].GetSkill(HeroSkillLocation.Movement) == 7, "identity commit preserves movement and writes active page");
        Check(Enumerable.Range(0, 5).All(i => ReferenceEquals(vanilla[i], originalVanilla[i])), "identity commit preserves every vanilla page");
        Check(identity.Where((_, i) => i != 1).All(p => p.GetSkill(HeroSkillLocation.Q) == 0), "identity commit preserves other identity pages");
        DewSave.profileMain.skills["St_Q_Foreign"].isAvailableInGame = false;
        Check(BuildApplicator.Prepare(guide).Errors.Any(e => e.StartsWith("Locked:", StringComparison.Ordinal)), "identity mode still checks memory unlocks");
        DewSave.profileMain.skills["St_Q_Foreign"].isAvailableInGame = true;
        plan = BuildApplicator.Prepare(guide);
        DewIdentityChange.config.LoadoutPageMapper.Enabled = false;
        int saves = DewSave.Saves;
        bool rejected = false;
        try { BuildApplicator.Commit(plan); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && saves == DewSave.Saves, "mode changes invalidate pending plans");
        var marked = Guide();
        marked.RequiresIdentityChange = true;
        var blockedPlan = BuildApplicator.Prepare(marked);
        Check(blockedPlan.Errors.Any(e => e.StartsWith("IdentityChangeRequired:", StringComparison.Ordinal)), "marked build is blocked outside identity mode even with ordinary resources");
        int commands = DewPlayer.local.Commands;
        rejected = false;
        try { BuildApplicator.Commit(blockedPlan); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && saves == DewSave.Saves && commands == DewPlayer.local.Commands, "blocked identity build preserves saves and network state");
        Check(BuildApplicator.Prepare(Guide()).Errors.Any(e => e.StartsWith("WrongHeroStar:", StringComparison.Ordinal)), "disabled mod restores hero-specific star restrictions");
        hero.Skills.Options[HeroSkillLocation.Q] = new SkillTrigger[] { new St_Q_First(), new St_Q_Second() };
        guide.RequiresIdentityChange = false;
        Check(BuildApplicator.Prepare(guide).Errors.Any(e => e.StartsWith("Unresolved:", StringComparison.Ordinal)), "foreign skill is rejected when absent from active catalogue");
        DewIdentityChange.config.LoadoutPageMapper.Enabled = true;
        DewSave.profileMain.heroLoadouts.Remove("DewIdentityChange_Hero_Test");
        Check(!BuildApplicator.TryGetTarget(out _, out _, out _), "missing identity storage never falls back to vanilla page");
        Reset();
    }
}
