using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using DewMoreChaosChoices.util;
using HarmonyLib;

namespace DewMoreChaosChoices.patch;

[HarmonyPatch]
public static class ShrineChoices_Patch
{
    // 两种混沌均在服务器的新实例初始化中生成奖励，存档恢复分支保持原样。
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Shrine_Chaos), nameof(Shrine_Chaos.OnLateStartServer));
        yield return AccessTools.Method(typeof(Shrine_CorruptedChaos), nameof(Shrine_CorruptedChaos.OnLateStartServer));
    }

    // 只替换分配数组及循环上限，保留原版筛选、随机权重、回调和同步流程。
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        var code = instructions.Select(instruction => new CodeInstruction(instruction)).ToList();
        var choiceField = AccessTools.Field(original.DeclaringType, "numOfChoice");
        bool corrupted = original.DeclaringType == typeof(Shrine_CorruptedChaos);
        var rarityField = corrupted ? null : AccessTools.Field(original.DeclaringType, "rarity");
        if (!corrupted && (rarityField == null || rarityField.FieldType != typeof(Rarity)))
        {
            throw new InvalidOperationException($"混沌品质字段已变化：{original.DeclaringType?.Name}");
        }
        var matches = new List<int>();
        for (int i = 1; i + 2 < code.Count; i++)
        {
            if (code[i - 1].opcode == OpCodes.Ldarg_0 && code[i].LoadsField(choiceField)
                && code[i + 1].IsLdloc() && code[i + 2].opcode == OpCodes.Add)
            {
                matches.Add(i - 1);
            }
        }

        // 对当前程序集的布局做严格校验，版本变化时在加载阶段报告错误。
        if (matches.Count != 2 || code[matches[0] + 4].opcode != OpCodes.Newarr
            || !code[matches[0] + 5].IsStloc())
        {
            throw new InvalidOperationException($"混沌奖励生成结构已变化：{original.DeclaringType?.Name}");
        }

        int candidateLocal = original.GetMethodBody().LocalVariables
            .Single(local => local.LocalType == typeof(List<int>)).LocalIndex;
        var rewardsLocal = code[matches[0] + 5];

        // 堕落混沌每抽一次会缩小候选列表，循环必须始终读取已分配数组的长度。
        int loop = matches[1];
        var loadRewards = CodeInstruction.LoadLocal(GetStoredLocalIndex(rewardsLocal));
        code[loop].opcode = loadRewards.opcode;
        code[loop].operand = loadRewards.operand;
        code[loop + 1].opcode = OpCodes.Ldlen;
        code[loop + 1].operand = null;
        code[loop + 2].opcode = OpCodes.Conv_I4;
        code[loop + 2].operand = null;
        code[loop + 3].opcode = OpCodes.Nop;
        code[loop + 3].operand = null;

        int allocationAdd = matches[0] + 3;
        var loadCandidates = CodeInstruction.LoadLocal(candidateLocal);
        code[allocationAdd].opcode = loadCandidates.opcode;
        code[allocationAdd].operand = loadCandidates.operand;
        var countInstructions = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(List<int>), "Count")),
            new CodeInstruction(corrupted ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0)
        };
        // 堕落混沌没有品质字段；普通混沌从实例读取品质，不改变原版品质抽取。
        if (corrupted)
        {
            countInstructions.Add(new CodeInstruction(OpCodes.Ldc_I4_0));
        }
        else
        {
            countInstructions.Add(new CodeInstruction(OpCodes.Ldarg_0));
            countInstructions.Add(new CodeInstruction(OpCodes.Ldfld, rarityField));
        }
        countInstructions.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ShrineChoices_Patch), nameof(GetCount))));
        code.InsertRange(allocationAdd + 1, countInstructions);
        return code;
    }

    // 房主在原版基础数量和玩家天赋上叠加配置增量，结果由原版字典同步到客户端。
    public static int GetCount(int vanillaCount, int bonusCount, int availableCount, bool corrupted, Rarity rarity = Rarity.Common)
    {
        var config = DewMoreChaosChoices.Instance?.Config;
        int extraCount = config == null ? 0
            : corrupted ? config.ExtraCorruptedChaosChoices : config.GetExtraChaosChoices(rarity);
        return ChoiceCount.Resolve(vanillaCount, bonusCount, extraCount, availableCount);
    }

    // 支持短格式及通用格式的局部变量存储指令。
    private static int GetStoredLocalIndex(CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Stloc_0) return 0;
        if (instruction.opcode == OpCodes.Stloc_1) return 1;
        if (instruction.opcode == OpCodes.Stloc_2) return 2;
        if (instruction.opcode == OpCodes.Stloc_3) return 3;
        return instruction.operand is LocalBuilder local ? local.LocalIndex : Convert.ToInt32(instruction.operand);
    }
}
