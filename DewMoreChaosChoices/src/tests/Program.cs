using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using DewMoreChaosChoices.config;
using DewMoreChaosChoices.patch;
using DewMoreChaosChoices.util;
using HarmonyLib;

namespace DewMoreChaosChoices.tests;

internal static class Program
{
    private static int _checks;

    // 回归检查失败时终止进程，并输出具体原因。
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
    }

    // 使用本机游戏真实 IL 验证匹配位置、服务器回调和奖励生成循环。
    private static void CheckInstalledMethod(Type type, bool corrupted)
    {
        MethodInfo method = type.GetMethod("OnLateStartServer");
        var original = PatchProcessor.GetOriginalInstructions(method);
        var patched = ShrineChoices_Patch.Transpiler(original, method).ToList();
        var resolver = AccessTools.Method(typeof(ShrineChoices_Patch), nameof(ShrineChoices_Patch.GetCount));
        int allocation = patched.FindIndex(instruction => instruction.Calls(resolver));
        Check(allocation >= 0, type.Name + ": missing count resolver");
        Check(patched.Count(instruction => instruction.Calls(resolver)) == 1, "Resolve once per allocation");
        Check(patched[allocation - (corrupted ? 2 : 3)].opcode == (corrupted ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0), "Independent config selection");
        Check(corrupted ? patched[allocation - 1].opcode == OpCodes.Ldc_I4_0
            : patched[allocation - 1].LoadsField(AccessTools.Field(type, "rarity")), "Read actual shrine rarity for normal chaos only");
        Check(patched[allocation + 1].opcode == OpCodes.Newarr, "Cap before array allocation");
        Check(patched.Count(instruction => instruction.opcode == OpCodes.Ldlen)
              == original.Count(instruction => instruction.opcode == OpCodes.Ldlen) + 1, "Loop uses fixed array length");
        Check(patched.Count(instruction => instruction.opcode == OpCodes.Ret)
              == original.Count(instruction => instruction.opcode == OpCodes.Ret), "Preserve saved-instance early return");
        Check(original.SelectMany(instruction => instruction.labels).SequenceEqual(patched.SelectMany(instruction => instruction.labels)), "Preserve branch labels");
        Check(original.Where(instruction => instruction.operand is MethodInfo)
            .All(call => patched.Any(instruction => Equals(instruction.operand, call.operand))), "Preserve vanilla calls and networking");
        Console.WriteLine(type.Name + ": installed IL verified");
    }

    // 构造与游戏相同的候选抽取循环，实际执行重写后的 IL 以覆盖数组和循环边界。
    private sealed class Fixture
    {
        public int numOfChoice = 3;
        public Rarity rarity = Rarity.Common;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public int[] Generate(int bonus, int available)
        {
            var candidates = Enumerable.Range(0, available).ToList();
            int offset = GetBonus(bonus);
            var result = new int[numOfChoice + offset];
            for (int i = 0; i < numOfChoice + offset; i++)
            {
                if (candidates.Count == 0) break;
                result[i] = candidates[0];
                candidates.RemoveAt(0);
            }
            return result;
        }

        // 与游戏从玩家字典取得的额外选项一样，保持独立局部变量。
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int GetBonus(int bonus) => bonus;
    }

    // 对真实执行路径安装补丁，检查候选列表缩小时不会提前截断或补零。
    private static void CheckExecutedPatch()
    {
        var harmony = new Harmony("DewMoreChaosChoices.RegressionTests");
        var method = AccessTools.Method(typeof(Fixture), nameof(Fixture.Generate));
        harmony.Patch(method, transpiler: new HarmonyMethod(typeof(ShrineChoices_Patch), nameof(ShrineChoices_Patch.Transpiler)));
        try
        {
            var fixture = new Fixture { numOfChoice = 20 };
            Check(fixture.Generate(5, 12).SequenceEqual(Enumerable.Range(0, 12)), "Exhausted pool produces 12 unique filled rewards");
            Check(fixture.Generate(int.MaxValue, 8).Length == 8, "Extreme bonus does not overflow allocation");
            Check(fixture.Generate(0, 0).Length == 0, "Empty pool does not fabricate rewards");
            fixture.numOfChoice = 1;
            Check(fixture.Generate(2, 12).SequenceEqual(Enumerable.Range(0, 3)), "Talent bonus is added");
            Check(fixture.Generate(int.MinValue, 12).Length == 1, "Extreme negative bonus keeps one valid reward");

            var entry = (DewMoreChaosChoices)FormatterServices.GetUninitializedObject(typeof(DewMoreChaosChoices));
            entry.Config = new PluginConfig
            {
                ExtraChaosChoices = 1,
                ExtraRareChaosChoices = 2,
                ExtraEpicChaosChoices = 3,
                ExtraLegendaryChaosChoices = 4
            };
            DewMoreChaosChoices.Instance = entry;
            fixture.numOfChoice = 3;
            foreach (Rarity rarity in new[] { Rarity.Common, Rarity.Rare, Rarity.Epic, Rarity.Legendary })
            {
                fixture.rarity = rarity;
                int expected = 4 + (int)rarity;
                Check(fixture.Generate(0, 12).SequenceEqual(Enumerable.Range(0, expected)), "Executed patch selects " + rarity + " config");
            }
        }
        finally
        {
            DewMoreChaosChoices.Instance = null;
            harmony.UnpatchAll(harmony.Id);
        }
    }

    // 绕过 Unity 构造过程，验证各品质和堕落混沌的设置独立且会被新抽取读取。
    private static void CheckIndependentConfigs()
    {
        var entry = (DewMoreChaosChoices)FormatterServices.GetUninitializedObject(typeof(DewMoreChaosChoices));
        entry.Config = new PluginConfig
        {
            ExtraChaosChoices = 2,
            ExtraRareChaosChoices = 1,
            ExtraEpicChaosChoices = 3,
            ExtraLegendaryChaosChoices = 4,
            ExtraCorruptedChaosChoices = 5
        };
        DewMoreChaosChoices.Instance = entry;
        try
        {
            Check(ShrineChoices_Patch.GetCount(3, 1, 8, false) == 6, "Normal extra config with talent");
            Check(ShrineChoices_Patch.GetCount(3, 1, 12, true) == 9, "Independent corrupted extra config with talent");
            entry.Config.ExtraChaosChoices = 0;
            Check(ShrineChoices_Patch.GetCount(3, 0, 8, false) == 3, "Zero extra preserves vanilla base");
            Check(ShrineChoices_Patch.GetCount(3, 2, 8, false) == 5, "Zero extra preserves talents");
            Check(ShrineChoices_Patch.GetCount(5, 1, 12, false) == 6, "Base count from other mods is preserved");
            Check(ShrineChoices_Patch.GetCount(3, 0, 12, true) == 8, "Normal changes leave corrupted extras intact");
            Check(ShrineChoices_Patch.GetCount(3, 1, 12, false, Rarity.Rare) == 5, "Rare uses independent extra count");
            Check(ShrineChoices_Patch.GetCount(3, 1, 12, false, Rarity.Epic) == 7, "Epic uses independent extra count");
            Check(ShrineChoices_Patch.GetCount(3, 1, 12, false, Rarity.Legendary) == 8, "Legendary uses independent extra count");
            Check(ShrineChoices_Patch.GetCount(3, 1, 12, true, Rarity.Legendary) == 9, "Corrupted ignores normal rarity config");
            entry.Config.ExtraEpicChaosChoices = 0;
            Check(ShrineChoices_Patch.GetCount(3, 1, 8, false, Rarity.Epic) == 4, "Zero epic extra keeps base and talent");
            Check(ShrineChoices_Patch.GetCount(3, 1, 8, false, Rarity.Legendary) == 8, "Changing epic leaves legendary intact");
            Check(ShrineChoices_Patch.GetCount(3, 0, 8, false, Rarity.Unique) == 3, "Special rarity keeps vanilla count");
        }
        finally
        {
            DewMoreChaosChoices.Instance = null;
        }
    }

    // 仅挂接服务端奖励生成方法，不要求客户端界面补丁或自定义网络类型。
    private static void CheckHostOnlyPatches()
    {
        var assembly = typeof(DewMoreChaosChoices).Assembly;
        Check(!assembly.GetReferencedAssemblies().Any(reference => reference.Name == "Dew.UI"), "No client gameplay UI dependency");
        var patchTypes = assembly.GetTypes().Where(type => type.IsDefined(typeof(HarmonyPatch), false)).ToArray();
        Check(patchTypes.Length == 1 && patchTypes[0] == typeof(ShrineChoices_Patch), "Only server generation patch is registered");
        var targets = (IEnumerable<MethodBase>)AccessTools.Method(typeof(ShrineChoices_Patch), "TargetMethods").Invoke(null, null);
        Check(targets.All(method => method.Name == "OnLateStartServer") && targets.Count() == 2, "Both targets are server lifecycle methods");
    }

    // 测试数量边界及本机版本兼容性，不启动 Unity 或加载存档。
    public static int Main()
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        try
        {
            return Run();
        }
        catch (Exception exception)
        {
            for (Exception current = exception; current != null; current = current.InnerException)
            {
                Console.WriteLine(current.GetType().FullName + ": " + current.Message);
            }
            return 1;
        }
    }

    // 独立方法使加载或 JIT 异常也进入测试入口的错误报告。
    private static int Run()
    {
        Console.WriteLine("Checking count boundaries...");
        Check(ChoiceCount.Resolve(3, 0, 3, 8) == 6, "Default extra count");
        Check(ChoiceCount.Resolve(3, 1, 2, 8) == 6, "Vanilla plus talent plus extras");
        Check(ChoiceCount.Resolve(3, 2, 100, 6) == 6, "Normal pool cap");
        Check(ChoiceCount.Resolve(3, 2, 100, 11) == 11, "Eligible melee pool cap");
        Check(ChoiceCount.Resolve(3, 0, 0, 6) == 3, "Zero extra leaves vanilla unchanged");
        Check(ChoiceCount.Resolve(3, 0, -8, 6) == 3, "Negative extra clamps to zero");
        Check(ChoiceCount.Resolve(int.MaxValue, int.MaxValue, int.MaxValue, 12) == 12, "Integer overflow");
        Check(ChoiceCount.Resolve(3, 0, 3, 0) == 0, "Empty pool");
        CheckInstalledMethod(typeof(Shrine_Chaos), false);
        CheckInstalledMethod(typeof(Shrine_CorruptedChaos), true);
        CheckExecutedPatch();
        CheckIndependentConfigs();
        CheckHostOnlyPatches();
        Console.WriteLine("PASS: " + _checks + " checks");
        return 0;
    }
}
