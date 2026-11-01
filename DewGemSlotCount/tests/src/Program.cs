using System;
using System.IO;
using System.Linq;
using System.Text;
using DewGemSlotCount.controller;
using DewGemSlotCount.patch;
using Mirror;
using Mono.CecilX;
using Mono.CecilX.Cil;
using HarmonyLib;

internal static class Program
{
    private static int _checks;

    // 验证生产控制器的权威状态更新及实际游戏程序集中的补丁入口。
    private static void Main()
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        try { Run(); }
        catch (Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
                Console.WriteLine(current.GetType().FullName + ": " + current.Message);
            Environment.ExitCode = 1;
        }
    }

    private static void Run()
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        DewGemSlotCount.DewGemSlotCount.Instance = new DewGemSlotCount.DewGemSlotCount();
        NetworkedManagerBase<GameSettingsManager>.softInstance = new GameSettingsManager();
        NetworkServer.active = true;
        DewGemSlotCount.DewGemSlotCount.Instance.Config.SkillQGemCount = 4;
        HeroSkill skill = new();
        GemSlotController.ApplyConfig(skill);
        Equal(4, skill.GetMaxGemCount((HeroSkillLocation)0), "四槽写入真实字段，供原版网络同步");
        GemSlotController.ApplyConfig(skill);
        Equal(4, skill.GetMaxGemCount((HeroSkillLocation)0), "重复初始化不叠加");
        skill.SetMaxGemCount(HeroSkillLocation.Q, 5);
        DewGemSlotCount.DewGemSlotCount.Instance.Config.SkillQGemCount = 5;
        GemSlotController.RefreshAll();
        Equal(6, skill.GetMaxGemCount((HeroSkillLocation)0), "更改基础槽保留额外加成");

        Hero hero = new() { Skill = skill };
        var chaos = new Se_Shrine_Chaos_StatBonus { victim = hero, currentAddedGemSlotQ = 2 };
        skill.SetMaxGemCount(HeroSkillLocation.Q, 5); // 原版 NotifyUpdate_Corrupted 写入 3 + 2。
        GemSlotController.ApplyChaosBonus(chaos);
        Equal(7, skill.GetMaxGemCount((HeroSkillLocation)0), "混沌按配置基础槽与保存加成重算");
        GemSlotController.ApplyChaosBonus(chaos);
        Equal(7, skill.GetMaxGemCount((HeroSkillLocation)0), "重复混沌重算不叠加");
        DewGemSlotCount.DewGemSlotCount.Instance.Config.SkillQGemCount = 8;
        GemSlotController.RefreshAll();
        Equal(8, skill.GetMaxGemCount((HeroSkillLocation)0), "总数限制为八槽");

        DewGemSlotCount.DewGemSlotCount.Instance.Config.SkillMovementGemCount = 2;
        GemSlotController.ApplyConfig(skill);
        skill.SetMaxGemCount(HeroSkillLocation.Movement, 3);
        GemSlotController.ApplyChaosBonus(chaos);
        Equal(3, skill.GetMaxGemCount((HeroSkillLocation)5), "其他混沌奖励保留位移额外槽");

        NetworkServer.active = false;
        HeroSkill client = new() { isServer = false };
        client.Counts[0] = 4;
        GemSlotController.ApplyConfig(client);
        GemSlotController.ApplyChaosBonus(new Se_Shrine_Chaos_StatBonus { victim = new Hero { Skill = client } });
        Equal(4, client.GetMaxGemCount((HeroSkillLocation)0), "客机原版同步值保持四槽");
        NetworkServer.active = true;
        GemSlotController.ApplyConfig(client);
        GemSlotController.ApplyChaosBonus(new Se_Shrine_Chaos_StatBonus { victim = new Hero { Skill = client } });
        Equal(4, client.GetMaxGemCount((HeroSkillLocation)0), "服务端也跳过非服务端对象");

        GemSlotController.Clear();
        DewGemSlotCount.DewGemSlotCount.Instance.Config.SkillQGemCount = 4;
        HeroSkill lateJoin = new();
        GemSlotController.ApplyConfig(lateJoin);
        Equal(4, lateJoin.GetMaxGemCount((HeroSkillLocation)0), "新房间及后生成英雄重新初始化");
        Hero loaded = new();
        GemSlotController.ApplyChaosBonus(new Se_Shrine_Chaos_StatBonus { victim = loaded, currentAddedGemSlotQ = 1 });
        GemSlotController.ApplyConfig(loaded.Skill);
        Equal(5, loaded.Skill.GetMaxGemCount((HeroSkillLocation)0), "混沌读档先于技能初始化时保持正确总数");
        VerifyMixedClients();
        VerifyLiveConfigSync();
        VerifyReload();
        VerifyMovementAndGeometry();
        VerifySkillInteraction();
        VerifyPatchTargets();
        VerifyHarmonyRegistration();
        VerifyOrphanPatches();
        Console.WriteLine("通过 " + _checks + " 项回归检查。");
    }

    // 回归截图中的三项问题：历史位置上移、原版四槽不对称、展开缩放后的镜像中心漂移。
    private static void VerifyMovementAndGeometry()
    {
        var previous = DewGemSlotCount.DewGemSlotCount.Instance;
        var mod = new DewGemSlotCount.DewGemSlotCount();
        DewGemSlotCount.DewGemSlotCount.Instance = mod;
        DewPlayer.local = new DewPlayer { hero = new Hero() };
        DewPlayer.local.hero.Skill.Counts[5] = 4;
        var edit = new EditSkillManager { mode = EditSkillManager.ModeType.Regular };
        ManagerBase<EditSkillManager>.softInstance = edit;
        var container = new UnityEngine.RectTransform { anchoredPosition3D = new UnityEngine.Vector3(-427, 16, 3) };
        var canvas = new UnityEngine.CanvasGroup { transform = container };
        var button = new UI_InGame_SkillButton { skillType = HeroSkillLocation.Movement,
            transform = new UnityEngine.Transform { parent = new UnityEngine.Transform { parent = container } } };
        var ui = new UI_InGame_SkillButtons { skillButtons = new[] { button }, hiddenWhenExpanded = new[] { canvas } };
        DewGemSlotCount.ui.MovementEditLayout.Apply(ui, edit.mode);
        Near(-427, container.anchoredPosition3D.x, "编辑位移保留历史横坐标");
        Near(366, container.anchoredPosition3D.y, "原容器按历史偏移上移三百五十");
        Near(3, container.anchoredPosition3D.z, "保留深度");
        Equal(1, canvas.blocksRaycasts && canvas.interactable && canvas.alpha == 1 ? 1 : 0, "原版隐藏容器恢复可见与交互");
        for (int i = 0; i < 20; i++) DewGemSlotCount.ui.MovementEditLayout.Apply(ui, edit.mode);
        Near(366, container.anchoredPosition3D.y, "逐帧调用不累计偏移");
        Equal(1, DG.Tweening.ShortcutExtensions.moves, "展开期间不重启动画");
        edit.mode = EditSkillManager.ModeType.EditSkillShrine;
        DewGemSlotCount.ui.MovementEditLayout.Apply(ui, edit.mode);
        Equal(1, DG.Tweening.ShortcutExtensions.moves, "编辑模式切换不重复移动");
        edit.mode = EditSkillManager.ModeType.None;
        DewGemSlotCount.ui.MovementEditLayout.Apply(ui, edit.mode);
        Near(16, container.anchoredPosition3D.y, "关闭编辑精确恢复原位");
        edit.mode = EditSkillManager.ModeType.Regular;
        DewGemSlotCount.ui.MovementEditLayout.Apply(ui, edit.mode);
        Near(366, container.anchoredPosition3D.y, "重新打开沿用初始原位");
        mod.Config.OptimizeUI = false;
        DewGemSlotCount.ui.MovementEditLayout.Apply(ui, edit.mode);
        Near(16, container.anchoredPosition3D.y, "关闭优化恢复原位");
        Equal(0, canvas.blocksRaycasts ? 1 : 0, "原版编辑模式恢复隐藏状态");
        mod.Config.OptimizeUI = true;
        DewPlayer.local.hero.Skill.Counts[5] = 0;
        Equal(0, DewGemSlotCount.ui.MovementEditLayout.ShouldShowMovement(edit.mode) ? 1 : 0, "无可编辑内容时隐藏位移");
        mod.Config.AllowMovementCorruptedChaos = true;
        edit.mode = EditSkillManager.ModeType.EditSkillShrine;
        edit.currentProvider = new Shrine_CorruptedChaos();
        Equal(1, DewGemSlotCount.ui.MovementEditLayout.ShouldShowMovement(edit.mode) ? 1 : 0, "允许混沌取得首个位移槽时显示");

        // PlayGame.unity 位移四槽端点：左 y=-10.8，右 y=-25.099945，底部锚点额外减 50。
        var left = new UnityEngine.Vector3(-56.8f, -60.8f);
        var right = new UnityEngine.Vector3(56.8f, -75.099945f);
        DewGemSlotCount.ui.GemSlotGeometry.MakeSymmetric(ref left, ref right, 0);
        Near(-67.94997f, left.y, "四槽端点使用成对平均高度");
        Near(left.y, right.y, "四槽左右高度一致");
        Near(0, left.x + right.x, "四槽左右围绕技能中心对称");

        // 官方 Gem Group 轴心为 (0.5,0.1)，展开由 0.9 缩放至 1.3，技能中心在组内随之变化。
        foreach (float scale in new[] { 0.9f, 1f, 1.15f, 1.3f, 0.9f })
        {
            float rowOrigin = -57.5f + scale * 53.6f;
            var center = new UnityEngine.Vector3(0, -rowOrigin / scale);
            var lower = new UnityEngine.Vector3(-56.8f, -60.8f, 2);
            var upper = DewGemSlotCount.ui.GemSlotGeometry.MirrorAbove(lower, center);
            Near(0, (rowOrigin + lower.y * scale) + (rowOrigin + upper.y * scale), "不同展开缩放下上下槽贴合距离相等");
            Near(lower.x, upper.x, "镜像保留槽位横坐标");
            Near(lower.z, upper.z, "镜像保留深度");
        }
        DewPlayer.local = null;
        DewGemSlotCount.DewGemSlotCount.Instance = previous;
    }

    // 真实生产补丁与控制器在卸载空窗及连续重载时保持基础槽和奖励一致。
    private static void VerifyReload()
    {
        var mod = DewGemSlotCount.DewGemSlotCount.Instance;
        var skill = new HeroSkill();
        DewGemSlotCount.DewGemSlotCount.Instance = null;
        HeroSkill_Patch.OnLateStartServer_Prefix(skill);
        Equal(3, skill.GetMaxGemCount((HeroSkillLocation)0), "卸载空窗中的技能初始化保留原值");
        Equal(3, HeroSkill_Patch.GetConfiguredBaseGemCount(HeroSkillLocation.Q, skill), "旧补丁读取空实例时返回当前槽数");
        Equal(0, HeroSkill_Patch.GetConfiguredBaseGemCount(HeroSkillLocation.Movement), "缺少实例和技能时返回原版基础槽");
        Equal(4, HeroSkill_Patch.GetCorruptedChaosMaxGemCount(HeroSkillLocation.Q), "空实例混沌上限回退原版");
        bool result = false;
        Equal(1, HeroSkill_Patch.TryGetEquippedGemOfSameType_Prefix(skill, ref result, typeof(Gem), out _, out _) ? 1 : 0,
            "空实例合并补丁继续原版方法");
        int requested = 12;
        HeroSkill_Patch.SetMaxGemCount_Prefix(skill, HeroSkillLocation.Q, ref requested);
        Equal(12, requested, "空实例写入补丁继续原版参数");
        DewGemSlotCount.DewGemSlotCount.Instance = mod;
        GemSlotController.Clear();
        mod.Config.SkillQGemCount = 4;
        mod.Config.SkillMovementGemCount = 2;
        skill.SetMaxGemCount(HeroSkillLocation.Q, 5);
        skill.SetMaxGemCount(HeroSkillLocation.Movement, 1);
        GemSlotController.ApplyConfig(skill);
        Equal(6, skill.GetMaxGemCount((HeroSkillLocation)0), "重载前基础四槽加两槽奖励");
        Equal(3, skill.GetMaxGemCount((HeroSkillLocation)5), "重载前位移基础与奖励合计");
        for (int i = 0; i < 5; i++)
        {
            GemSlotController.RestoreAll();
            Equal(5, skill.GetMaxGemCount((HeroSkillLocation)0), "卸载撤回配置差值并保留奖励");
            Equal(1, skill.GetMaxGemCount((HeroSkillLocation)5), "卸载保留位移奖励");
            GemSlotController.RestoreAll();
            GemSlotController.Clear();
            GemSlotStateSync.ClearCache();
            GemSlotController.Resume(skill);
            GemSlotController.ApplyConfig(skill);
            Equal(6, skill.GetMaxGemCount((HeroSkillLocation)0), "连续重载不叠加基础槽");
            Equal(3, skill.GetMaxGemCount((HeroSkillLocation)5), "连续重载不叠加位移槽");
        }
        GemSlotController.RestoreAll();
        mod.Config.SkillQGemCount = 1;
        GemSlotController.ApplyConfig(skill);
        Equal(3, skill.GetMaxGemCount((HeroSkillLocation)0), "重载后调低基础槽仍保留奖励");
        GemSlotController.RestoreAll();
    }

    // 用实际 Harmony 验证旧 GUID 与稳定身份补丁均被清理，其他 Mod 的补丁保留。
    private static void VerifyOrphanPatches()
    {
        const string prefix = "com.chengchulian.DewMod.DewGemSlotCount";
        var old = new Harmony(prefix + "0123456789abcdef");
        var current = new Harmony(prefix + ".runtime");
        var other = new Harmony("DewGemSlotCount.RegressionTests.OtherMod");
        var target = AccessTools.Method(typeof(Program), nameof(ReloadFixture));
        try
        {
            old.Patch(target, prefix: new HarmonyMethod(AccessTools.Method(typeof(Program), nameof(ReloadFixtureOldPrefix))));
            current.Patch(target, prefix: new HarmonyMethod(AccessTools.Method(typeof(Program), nameof(ReloadFixturePrefix))));
            other.Patch(target, prefix: new HarmonyMethod(AccessTools.Method(typeof(Program), nameof(ReloadFixtureOtherPrefix))));
            GemPatchLifecycle.Remove();
            var owners = Harmony.GetPatchInfo(target).Owners;
            Equal(0, owners.Contains(old.Id) ? 1 : 0, "清理旧程序集的随机身份补丁");
            Equal(0, owners.Contains(current.Id) ? 1 : 0, "清理当前稳定身份补丁");
            Equal(1, owners.Contains(other.Id) ? 1 : 0, "保留其他 Mod 在同一入口的补丁");
            GemPatchLifecycle.Remove();
            Equal(1, Harmony.GetPatchInfo(target).Owners.Count, "重复清理安全且不影响其他补丁");
        }
        finally
        {
            old.UnpatchAll(old.Id);
            current.UnpatchAll(current.Id);
            other.UnpatchAll(other.Id);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int ReloadFixture() => 1;
    private static void ReloadFixtureOldPrefix() { }
    private static void ReloadFixturePrefix() { }
    private static void ReloadFixtureOtherPrefix() { }

    // 房主单向发布完整槽数，Mod 客机直接读取，原版网络字段始终最多四槽。
    private static void VerifyMixedClients()
    {
        GemSlotController.Clear();
        NetworkServer.active = true;
        NetworkClient.sent = 0;
        var settings = new GameSettingsManager();
        NetworkedManagerBase<GameSettingsManager>.softInstance = settings;
        var modded = Player(false, 1);
        var vanilla = Player(false, 2);
        DewGemSlotCount.DewGemSlotCount.Instance.Config.SkillQGemCount = 4;
        GemSlotController.RefreshAll();
        Equal(4, modded.hero.Skill.GetMaxGemCount(HeroSkillLocation.Q), "四槽配置的服务端初始容量");
        var client = new HeroSkill { isServer = false, netId = modded.hero.Skill.netId };
        client.Counts[0] = modded.hero.Skill.Counts[0];
        NetworkServer.active = false;
        Equal(4, client.GetMaxGemCount(HeroSkillLocation.Q), "客机直接读取四槽快照");
        NetworkServer.active = true;
        DewGemSlotCount.DewGemSlotCount.Instance.Config.SkillQGemCount = 8;
        GemSlotController.RefreshAll();
        Equal(8, modded.hero.Skill.GetMaxGemCount(HeroSkillLocation.Q), "房主四改八后服务端完整容量");
        Equal(4, vanilla.hero.Skill.Counts[0], "未装 Mod 通过原生字段仍读取四槽");
        Equal(4, modded.hero.Skill.Counts[0], "Mod 客机也使用相同的原生四槽字段");
        NetworkServer.active = false;
        Equal(8, client.GetMaxGemCount(HeroSkillLocation.Q), "现有客机无需握手从四槽更新到八槽");
        Equal(0, NetworkClient.sent, "读取扩展容量不发送任何客户端消息");
        NetworkServer.active = true;
        DewGemSlotCount.DewGemSlotCount.Instance.Config.SkillQGemCount = 3;
        GemSlotController.RefreshAll();
        GemSlotController.ApplyChaosBonus(new Se_Shrine_Chaos_StatBonus { victim = modded.hero, currentAddedGemSlotQ = 3 });
        Equal(6, modded.hero.Skill.GetMaxGemCount(HeroSkillLocation.Q), "混沌完整容量为六槽");
        Equal(4, modded.hero.Skill.Counts[0], "混沌原生同步仍最多四槽");
        NetworkServer.active = false;
        Equal(6, client.GetMaxGemCount(HeroSkillLocation.Q), "Mod 客机直接读取混沌六槽");
        var lateClient = new HeroSkill { isServer = false, netId = modded.hero.Skill.netId };
        lateClient.Counts[0] = 4;
        Equal(6, lateClient.GetMaxGemCount(HeroSkillLocation.Q), "晚加入直接读取现有快照");
        GemSlotStateSync.ClearCache();
        Equal(6, lateClient.GetMaxGemCount(HeroSkillLocation.Q), "客机重载后重新读取现有快照");
        NetworkServer.active = true;
        GemSlotController.Clear();
        DewPlayer.allHumanPlayers.Clear();
    }

    // 快照校验、房间切换与结算兼容均不依赖能力上报。
    private static void VerifyLiveConfigSync()
    {
        NetworkServer.active = false;
        var settings = NetworkedManagerBase<GameSettingsManager>.softInstance;
        var skill = new HeroSkill { isServer = false, netId = 500 };
        skill.Counts[0] = 4;
        string key = GemSlotStateSync.StatePrefix + "500";
        Equal(4, skill.GetMaxGemCount(HeroSkillLocation.Q), "没有 Mod 快照的房间继续读取原版字段");
        settings.customData[key] = "8,7,6,5,2,1";
        Equal(8, skill.GetMaxGemCount(HeroSkillLocation.Q), "首次快照立即生效");
        Equal(1, skill.GetMaxGemCount(HeroSkillLocation.Movement), "快照分别保存六种技能容量");
        settings.customData[key] = "4,3,2,1,0,0";
        Equal(4, skill.GetMaxGemCount(HeroSkillLocation.Q), "同一英雄修改快照使缓存失效");
        foreach (string invalid in new[] { "9,3,3,3,0,0", "-1,3,3,3,0,0", "8,3", "x,3,3,3,0,0", "", null })
        {
            settings.customData[key] = invalid;
            Equal(4, skill.GetMaxGemCount(HeroSkillLocation.Q), "非法快照回退原生字段");
        }
        NetworkedManagerBase<GameSettingsManager>.softInstance = new GameSettingsManager();
        Equal(4, skill.GetMaxGemCount(HeroSkillLocation.Q), "切房间后不使用前一房间的缓存");
        var result = new DewGameResult();
        GemSlotStateSync.CaptureResult(result);
        Equal(4, result.players[0].maxGemCounts[0], "结算原生容量列表保持四槽");
        Equal(1, GemSlotStateSync.TryParse(result.players[0].capturedStarTooltipFields[GemSlotStateSync.ResultKey], out int[] counts) ? 1 : 0,
            "完整结算容量保存在已有可序列化字典");
        Equal(8, counts[0], "结算字典保留完整八槽");
        Equal(0, NetworkClient.sent, "房间与结算读取均不发送能力消息");
        NetworkServer.active = true;
    }
    private static DewPlayer Player(bool local, int connectionId)
    {
        var player = new DewPlayer { isLocalPlayer = local,
            netId = (uint)(connectionId + 1),
            connectionToClient = new NetworkConnectionToClient { connectionId = connectionId,
                identity = new NetworkIdentity { netId = (uint)(connectionId + 1) } }, hero = new Hero() };
        player.hero.owner = player;
        DewPlayer.allHumanPlayers.Add(player);
        return player;
    }

    // 实际执行生产结算补丁的注册，回归辅助函数误命名为 Harmony 保留钩子的启动崩溃。
    private static void VerifyHarmonyRegistration()
    {
        var harmony = new Harmony("DewGemSlotCount.RegressionTests.ResultUi");
        try
        {
            harmony.CreateClassProcessor(typeof(UI_InGame_ResultView_Patch)).Patch();
            harmony.CreateClassProcessor(typeof(UI_InGame_Result_HeroSkillItem_Patch)).Patch();
            harmony.CreateClassProcessor(typeof(HeroSkill_Patch)).Patch();
            harmony.CreateClassProcessor(typeof(GameResultManager_Patch)).Patch();
            Equal(0, DewGemSlotCount.ui.GemSlotLayout.extendCalls, "注册补丁时不提前访问结算 UI");
            var view = new UI_InGame_ResultView();
            AccessTools.Method(typeof(UI_InGame_ResultView), "Refresh").Invoke(view, null);
            Equal(7, view.groupsAtRefresh, "结算原方法收集组件前已经准备二至八槽");
            var result = new DewGameResult();
            GemSlotStateSync.CaptureResult(result);
            view.skills[0].UpdateAndGetScore(result, 0, 1f);
            Equal(1, view.skills[0].originalCalls, "保留原版结算单项刷新");
            Equal(8, view.skills[0].renderedCount, "结算原版绘制过程读取扩展八槽");
            Equal(4, result.players[0].maxGemCounts[0], "结算绘制后恢复四槽兼容值");
            Equal(2, DewGemSlotCount.ui.GemSlotLayout.extendCalls, "结算两个生产补丁均成功安装并执行");
            var manager = new GameResultManager();
            AccessTools.Method(typeof(GameResultManager), "UpdateGameResult").Invoke(manager, null);
            Equal(4, manager.tracked.players[0].maxGemCounts[0], "真实结算收集补丁输出原版四槽");
            Equal(1, manager.tracked.players[0].capturedStarTooltipFields.ContainsKey(GemSlotStateSync.ResultKey) ? 1 : 0,
                "真实结算收集补丁保存完整容量");
            view.skills[0].throwOnUpdate = true;
            bool threw = false;
            try { view.skills[0].UpdateAndGetScore(result, 0, 1f); }
            catch (InvalidOperationException) { threw = true; }
            Equal(1, threw ? 1 : 0, "结算异常仍保留原始异常");
            Equal(4, result.players[0].maxGemCounts[0], "结算异常时也恢复原版兼容值");
        }
        catch (Exception exception)
        {
            // Harmony 的动态堆栈在独立运行时可能依赖 Unity；逐层输出类型和消息。
            for (Exception current = exception; current != null; current = current.InnerException)
            {
                Console.WriteLine(current.GetType().FullName + ": " + current.Message);
                foreach (var frame in new System.Diagnostics.StackTrace(current, false).GetFrames() ?? new System.Diagnostics.StackFrame[0])
                {
                    var method = frame.GetMethod();
                    Console.WriteLine("  " + method?.DeclaringType?.FullName + "." + method?.Name);
                }
            }
            Environment.ExitCode = 1;
            throw new InvalidOperationException("Harmony 注册回归失败，详见前面的异常链。", null);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    // 用真实 Harmony 前缀验证空身份、已有身份及编辑开关的交互路由。
    private static void VerifySkillInteraction()
    {
        var harmony = new Harmony("DewGemSlotCount.RegressionTests.SkillInteraction");
        var config = DewGemSlotCount.DewGemSlotCount.Instance.Config;
        bool previousEditIdentity = config.EditIdentitySkill;
        try
        {
            harmony.CreateClassProcessor(typeof(SkillTrigger_Patch)).Patch();
            harmony.Patch(AccessTools.Method(typeof(HeroSkill), nameof(HeroSkill.CanReplaceSkill)),
                prefix: new HarmonyMethod(typeof(HeroSkill_Patch), nameof(HeroSkill_Patch.CanReplaceSkill_Prefix)));
            var hero = new Hero { owner = new DewPlayer(), isOwned = true };
            config.EditIdentitySkill = false;
            var identity = new St_D_RegressionIdentity { rarity = Rarity.Identity };
            ((IInteractable)identity).OnInteract(hero, false);
            Equal(1, identity.originalCalls, "旧身份被吞掉后交回原版空槽确认流程");
            Equal(0, hero.Skill.equipCalls, "关闭编辑时不绕过原版身份装备流程");
            hero.Skill.Identity = new SkillTrigger();
            ((IInteractable)identity).OnInteract(hero, false);
            Equal(2, identity.originalCalls, "已有身份仍由原版确认替换");
            ((IInteractable)identity).OnInteract(hero, true);
            Equal(3, identity.originalCalls, "身份分解交互交回原版");
            hero.isOwned = false;
            ((IInteractable)identity).OnInteract(hero, false);
            Equal(4, identity.originalCalls, "远端英雄交互交回原版处理");
            config.EditIdentitySkill = true;
            hero.Skill.Identity = null;
            ((IInteractable)identity).OnInteract(hero, false);
            Equal(4, identity.originalCalls, "开启身份编辑时使用扩展拾取流程");
            Equal(1, ReferenceEquals(identity, hero.Skill.Identity) ? 1 : 0, "身份优先装入空身份槽");
            Equal(1, hero.Skill.pickupCalls, "服务端发送一次拾取通知");
            var regular = new SkillTrigger();
            config.EditIdentitySkill = false;
            ((IInteractable)regular).OnInteract(hero, false);
            Equal(0, regular.originalCalls, "普通技能保留扩展拾取流程");
            Equal(1, ReferenceEquals(regular, hero.Skill.Q) ? 1 : 0, "普通技能装入空Q槽");
            VerifyEmptySkillPickup();
        }
        finally
        {
            config.EditIdentitySkill = previousEditIdentity;
            harmony.UnpatchAll(harmony.Id);
        }
    }

    // 覆盖技能已满时的空特殊槽，防止误选禁用槽后丢失手持替换流程。
    private static void VerifyEmptySkillPickup()
    {
        var config = DewGemSlotCount.DewGemSlotCount.Instance.Config;
        bool previousEditIdentity = config.EditIdentitySkill;
        bool previousEditMovement = config.EditMovementSkill;
        try
        {
            foreach (bool editIdentity in new[] { false, true })
            foreach (bool editMovement in new[] { false, true })
            foreach (bool emptyMovement in new[] { false, true })
            {
                config.EditIdentitySkill = editIdentity;
                config.EditMovementSkill = editMovement;
                var hero = new Hero { owner = new DewPlayer() };
                hero.Skill.Q = new SkillTrigger();
                hero.Skill.W = new SkillTrigger();
                hero.Skill.E = new SkillTrigger();
                hero.Skill.R = new SkillTrigger();
                hero.Skill.Movement = emptyMovement ? null : new SkillTrigger();
                var memory = new SkillTrigger();
                ((IInteractable)memory).OnInteract(hero, false);
                bool useMovement = emptyMovement && editMovement;
                bool useIdentity = !useMovement && editIdentity;
                Equal(useMovement ? 1 : 0, ReferenceEquals(memory, hero.Skill.Movement) ? 1 : 0,
                    "位移空槽仅在允许编辑时自动装备");
                Equal(useIdentity ? 1 : 0, ReferenceEquals(memory, hero.Skill.Identity) ? 1 : 0,
                    "身份空槽仅在允许编辑时自动装备");
                Equal(useMovement || useIdentity ? 0 : 1, ReferenceEquals(memory, hero.Skill.holdingObject) ? 1 : 0,
                    "无可编辑空槽时拾取到手中，允许替换普通记忆");
                Equal(useMovement || useIdentity ? 1 : 0, hero.Skill.equipCalls,
                    "不尝试向禁用槽装备记忆");
                Equal(1, hero.Skill.pickupCalls, "装备或手持只通知拾取一次");

                // 同一空身份状态下，普通技能有空位时仍应直接装入普通槽。
                hero.Skill.Q = null;
                hero.Skill.Identity = null;
                var next = new SkillTrigger();
                ((IInteractable)next).OnInteract(hero, false);
                Equal(1, ReferenceEquals(next, hero.Skill.Q) ? 1 : 0, "无身份时仍可填充普通技能空槽");
            }
        }
        finally
        {
            config.EditIdentitySkill = previousEditIdentity;
            config.EditMovementSkill = previousEditMovement;
        }
    }

    // 用元数据读取已安装游戏的 IL，不加载 Unity 原生运行时。
    private static void VerifyPatchTargets()
    {
        string managed = Path.Combine(Environment.GetEnvironmentVariable("ShapeOfDreamsHome"), "Shape of Dreams_Data", "Managed");
        using (var core = ModuleDefinition.ReadModule(Path.Combine(managed, "Dew.Core.dll")))
        {
            var interact = core.Types.Single(t => t.Name == "SkillTrigger").Methods.Single(m => m.Name == "IInteractable.OnInteract");
            Equal(1, interact.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldstr &&
                Equals(i.Operand, "InGame_Message_ConfirmEmbraceNewIdentity_NoDestroy")) ? 1 : 0,
                "已安装游戏原版交互支持无旧身份的确认提示");
            var edit = core.Types.Single(t => t.Name == "EditSkillManager");
            foreach (string name in new[] { "TryGetNextRelevantSkillLocation", "TryGetNextRelevantGemSlot" })
            {
                var method = edit.Methods.Single(m => m.Name == name);
                Equal(2, method.Body.Instructions.Count(i => i.OpCode == OpCodes.Ldc_I4_4), name + " 的身份末项边界常量");
            }
            Equal(1, core.Types.Single(t => t.Name == "HeroSkill").Methods.Count(m => m.Name == "OnLateStartServer"), "技能初始化补丁入口");
            var heroSkill = core.Types.Single(t => t.Name == "HeroSkill");
            Equal(1, heroSkill.Methods.Single(m => m.Name == "GetMaxGemCount").Parameters.Count(p => p.Name == "type"), "读取槽数的实际补丁参数名");
            Equal(1, heroSkill.Methods.Single(m => m.Name == "SetMaxGemCount").Parameters.Count(p => p.Name == "type"), "写入槽数的实际补丁参数名");
            Equal(1, core.Types.Single(t => t.Name == "GameResultManager").Methods.Count(m => m.Name == "UpdateGameResult"), "结算兼容容量捕获入口");
            Equal(1, core.Types.Single(t => t.Name == "Se_Shrine_Chaos_StatBonus").Methods.Count(m => m.Name == "NotifyUpdate_Corrupted"), "混沌重算补丁入口");
        }
        using (var ui = ModuleDefinition.ReadModule(Path.Combine(managed, "Dew.UI.dll")))
        {
            Equal(1, ui.Types.Single(t => t.Name == "UI_InGame_ResultView").Methods.Count(m => m.Name == "Refresh"), "结算预建槽补丁入口");
            Equal(1, ui.Types.Single(t => t.Name == "UI_InGame_Result_HeroSkillItem").Methods.Count(m => m.Name == "UpdateAndGetScore"), "结算单项刷新补丁入口");
        }
    }

    private static void Equal(int expected, int actual, string message)
    {
        _checks++;
        if (expected != actual) throw new InvalidOperationException(message + ": " + actual + " != " + expected);
    }

    private static void Near(float expected, float actual, string message)
    {
        _checks++;
        if (Math.Abs(expected - actual) > 0.001f)
            throw new InvalidOperationException(message + ": " + actual + " != " + expected);
    }
}
