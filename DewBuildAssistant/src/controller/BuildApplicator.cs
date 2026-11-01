using System;
using System.Collections.Generic;
using System.Linq;
using DewBuildAssistant.config;
using DewBuildAssistant.model;
using DewBuildAssistant.util;

namespace DewBuildAssistant.controller;

internal sealed class BuildPlan
{
    public string Hero;
    public string StorageHero;
    public int Slot;
    public HeroLoadoutData Loadout;
    public bool SetDejavu;
    public string Dejavu;
    public int DejavuCost;
    public readonly List<string> Errors = new();
}

internal static class BuildApplicator
{
    private static readonly StarType[] StarTypes = { StarType.Destruction, StarType.Life, StarType.Imagination, StarType.Flexible };
    private static readonly string[] StarFields = { "破坏星座", "生命星座", "幻想星座", "灵活星座" };

    private static string T(string key, params object[] args) => LocalizationSource.GetLocalizationText(key, args);

    // 星座窗口的当前页优先于大厅快捷页；每次点击应用重新读取，避免写入旧栏位。
    public static bool TryGetTarget(out string hero, out int slot, out HeroLoadoutData current)
        => TryGetTarget(out hero, out slot, out current, out _);

    public static bool TryGetTarget(out string hero, out int slot, out HeroLoadoutData current, out string storageHero)
    {
        hero = DewPlayer.local != null ? DewPlayer.local.selectedHeroType : null;
        storageHero = hero == null ? null : IdentityChangeCompatibility.StorageHero(hero);
        slot = -1;
        current = null;
        var manager = NetworkedManagerBase<GameSettingsManager>.softInstance;
        // 中途加入时房间已经 InGame，本地玩家仍在大厅配装；入口与应用按本地状态判断。
        // Starting 仍处于配装锁定阶段，保持原有拦截。
        if (manager == null || (manager.state != GameState.InLobby && manager.state != GameState.InGame) || DewPlayer.local == null ||
            DewPlayer.local.state != PlayerState.InLobby || DewSave.profileMain == null || string.IsNullOrEmpty(hero)) return false;
        var editor = SingletonBehaviour<UI_Lobby_Constellations>.softInstance;
        if (editor != null && editor.isActiveAndEnabled && editor.loadout != null)
        {
            slot = editor.selectedLoadoutIndex;
            current = editor.loadout;
        }
        else
        {
            var settings = manager.GetLocalPreferredGameSettings();
            if (!settings.heroSelectedLoadoutIndex.TryGetValue(hero, out slot)) return false;
        }
        if (!DewSave.profileMain.heroLoadouts.TryGetValue(storageHero, out var pages) || slot < 0 || slot >= pages.Count) return false;
        current ??= pages[slot];
        return current != null;
    }

    // 完整预检在副本上进行。任何错误都保留原栏位，避免部分配装被静默覆盖。
    public static BuildPlan Prepare(GuideData guide)
    {
        var plan = new BuildPlan();
        if (!TryGetTarget(out plan.Hero, out plan.Slot, out var current, out plan.StorageHero))
        {
            plan.Errors.Add(T("NotInLobby"));
            return plan;
        }
        if (guide == null || guide.Hero != plan.Hero)
        {
            plan.Errors.Add(T("HeroMismatch"));
            return plan;
        }
        if (guide.RequiresIdentityChange && !IdentityChangeCompatibility.Enabled)
        {
            plan.Errors.Add(T("IdentityChangeRequired"));
            return plan;
        }
        if (DewPlayer.local.isReady) plan.Errors.Add(T("UnreadyFirst"));
        plan.Loadout = new HeroLoadoutData(current);
        Hero hero = DewResources.GetByShortTypeName<Hero>(plan.Hero);
        if (hero == null) { plan.Errors.Add(T("MissingHero")); return plan; }

        SetSkill("旅行者记忆1", HeroSkillLocation.Q);
        SetSkill("旅行者记忆2", HeroSkillLocation.R);
        SetSkill("旅行者身份记忆", HeroSkillLocation.Identity);
        for (int group = 0; group < StarTypes.Length; group++) SetStars(StarFields[group], StarTypes[group]);
        SetDejavu();

        // 游戏保存的配装可能只记星座名称，等级在发送时才补全；预检也使用当前已购等级。
        plan.Loadout.PopulateLevelsByLocalSaveData();
        if (!guide.PresentFields.Any()) plan.Errors.Add(T("EmptyGuide"));
        // 攻略可能只替换记忆；保留的星座也必须与新记忆相容。
        foreach (StarType type in StarTypes)
        foreach (LoadoutStarItem item in plan.Loadout.GetStarList(type))
        {
            if (string.IsNullOrEmpty(item.name)) continue;
            var star = DewResources.GetByShortTypeName<StarEffect>(item.name);
            if (star != null && star.IsIncompatibleWith(hero, plan.Loadout))
                plan.Errors.Add(T("IncompatibleStar", BuildExporter.ResourceDisplayName(item.name)));
        }
        if (!plan.Loadout.IsValidFor(plan.Hero)) plan.Errors.Add(T("InvalidLoadout"));
        return plan;

        void SetSkill(string field, HeroSkillLocation location)
        {
            var items = guide.GetItems(field);
            if (items.Count == 0) return;
            if (items.Count != 1) { plan.Errors.Add(T("SingleItem", WorkshopBuildCodec.FieldLabel(field))); return; }
            var item = items[0];
            var skills = hero.GetComponent<HeroSkill>().GetLoadoutSkills(location);
            var matches = Enumerable.Range(0, skills.Length).Where(i => skills[i].GetType().Name == item.ResourceHint).ToList();
            if (matches.Count != 1) { plan.Errors.Add(T("Unresolved", WorkshopBuildCodec.FieldLabel(field), item.Name)); return; }
            var skill = skills[matches[0]];
            if (!DewSave.profileMain.skills.TryGetValue(skill.GetType().Name, out var unlocked) || !unlocked.isAvailableInGame)
            { plan.Errors.Add(T("Locked", item.Name)); return; }
            plan.Loadout.SetSkill(location, matches[0]);
        }

        void SetStars(string field, StarType type)
        {
            if (!guide.PresentFields.Contains(field)) return;
            var requested = guide.GetItems(field);
            var settings = hero.GetConstellationSettings(type);
            var output = plan.Loadout.GetStarList(type);
            output.Clear();
            for (int i = 0; i < settings.maxCount; i++) output.Add(default);
            var slots = DewSave.profileMain.heroUnlockedStarSlots.TryGetValue(plan.Hero, out var unlocks) ? unlocks.Get(type) : new List<int>();
            var available = Enumerable.Range(0, settings.maxCount).Where(i => i < settings.defaultCount || slots.Contains(i)).ToList();
            if (requested.Count > available.Count) plan.Errors.Add(T("Capacity", WorkshopBuildCodec.FieldLabel(field), requested.Count, available.Count));
            var used = new HashSet<string>();
            for (int i = 0; i < requested.Count; i++)
            {
                var item = requested[i];
                string key = item.ResourceHint;
                var star = key == null ? null : DewResources.GetByShortTypeName<StarEffect>(key);
                if (star == null || !Dew.IsStarIncludedInGame(key)) { plan.Errors.Add(T("Unresolved", WorkshopBuildCodec.FieldLabel(field), item.Name)); continue; }
                if (!IdentityChangeCompatibility.Enabled && star.heroType != null && star.heroType.Name != plan.Hero)
                { plan.Errors.Add(T("WrongHeroStar", item.Name)); continue; }
                if (!used.Add(key)) { plan.Errors.Add(T("DuplicateStar", WorkshopBuildCodec.FieldLabel(field), item.Name)); continue; }
                if (!DewSave.profileMain.newStars.TryGetValue(key, out var data) || data.level <= 0)
                { plan.Errors.Add(T("Locked", item.Name)); continue; }
                if (star.IsIncompatibleWith(hero, plan.Loadout)) { plan.Errors.Add(T("IncompatibleStar", item.Name)); continue; }
                if (i < available.Count) output[available[i]] = new LoadoutStarItem { name = key, level = Math.Min(data.level, star.maxStarLevel) };
            }
        }

        void SetDejavu()
        {
            var items = guide.GetItems("既视感");
            if (guide.ClearDejavu && items.Count == 0)
            {
                plan.SetDejavu = true;
                plan.Dejavu = null;
                plan.DejavuCost = 0;
                return;
            }
            if (items.Count == 0) return;
            if (items.Count != 1) { plan.Errors.Add(T("SingleItem", T("Dejavu"))); return; }
            var item = items[0];
            string key = item.ResourceHint;
            var target = key == null ? null : DewResources.GetByShortTypeName<Actor>(key, ResourceLoadSettings.Light);
            if (!(target is Gem || target is SkillTrigger) || (target is Gem gem && (gem.excludeFromPool || !Dew.IsGemIncludedInGame(key))) ||
                (target is SkillTrigger skill && (skill.excludeFromPool || !Dew.IsSkillIncludedInGame(key) || skill.rarity == Rarity.Character || skill.rarity == Rarity.Identity)))
            { plan.Errors.Add(T("Unresolved", T("Dejavu"), item.Name)); return; }
            var manager = NetworkedManagerBase<GameSettingsManager>.softInstance;
            if (!manager.allowDejavu) plan.Errors.Add(T("DejavuDisabled"));
            if (manager.bannedGameItems.Contains(key)) plan.Errors.Add(T("Banned", item.Name));
            var unlocks = target is Gem ? DewSave.profileMain.gems : DewSave.profileMain.skills;
            if (DewSave.profileStats.total.masteryLevel < 50 || !unlocks.TryGetValue(key, out var unlocked) || unlocked.status != UnlockStatus.Complete ||
                !DewSave.profileStats.TryGetItemData(target.GetType(), out var stats) || stats.wins <= 0)
                plan.Errors.Add(T("DejavuLocked", item.Name));
            plan.SetDejavu = true;
            plan.Dejavu = key;
            plan.DejavuCost = Dew.IsDejavuFree(key) ? 0 : Dew.GetDejavuCost(target);
            if (DewSave.profileMain.stardust < plan.DejavuCost) plan.Errors.Add(T("DejavuCost", plan.DejavuCost));
        }
    }

    // 仅保存所选页，通过游戏原有命令同步本地玩家；既视感费用仍在游戏开始时扣除。
    public static void Commit(BuildPlan plan)
    {
        if (plan.Errors.Count > 0 || !TryGetTarget(out string hero, out int slot, out _, out string storageHero) ||
            hero != plan.Hero || slot != plan.Slot || storageHero != plan.StorageHero)
            throw new InvalidOperationException(T("TargetChanged"));
        var manager = NetworkedManagerBase<GameSettingsManager>.softInstance;
        DewSave.profileMain.heroLoadouts[storageHero][slot] = new HeroLoadoutData(plan.Loadout);
        DewSave.SaveProfileMain();
        manager.GetLocalPreferredGameSettings().heroSelectedLoadoutIndex[hero] = slot;
        DewPlayer.local.CmdSetHeroLoadoutData(plan.Loadout);
        if (plan.SetDejavu)
        {
            manager.localPlayerDejavuCost = plan.DejavuCost;
            manager.GetLocalPreferredGameSettings().dejavuItem = plan.Dejavu;
            DewPlayer.local.CmdSetDejavuItem(plan.Dejavu);
        }
        var editor = SingletonBehaviour<UI_Lobby_Constellations>.softInstance;
        if (editor != null && editor.isActiveAndEnabled) editor.LoadLoadout(slot);
    }
}
