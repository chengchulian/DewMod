using System;
using System.Collections.Generic;
using System.Globalization;
using Mirror;

namespace DewGemSlotCount.controller;

// 完整槽数只由房主通过原生 customData 下发；客机不发送能力消息。
public static class GemSlotStateSync
{
    public const int VanillaLimit = 4;
    public const string StatePrefix = "DewGemSlotCount::slots:v1:";
    public const string ResultKey = "DewGemSlotCount::slots:v1";
    private sealed class CachedState
    {
        public string payload;
        public int[] counts;
    }
    private static readonly Dictionary<uint, CachedState> Cache = new();

    public static void Publish(HeroSkill skill, int[] counts)
    {
        var settings = NetworkedManagerBase<GameSettingsManager>.softInstance;
        if (!NetworkServer.active || skill == null || skill.netId == 0 || settings?.customData == null) return;
        string key = GetKey(skill.netId);
        string payload = Serialize(counts);
        if (!settings.customData.TryGetValue(key, out string previous) || previous != payload)
            settings.customData[key] = payload;
    }

    // 从字典的最新值读取，事件尚未触发时也能生效；相同快照不重复解析。
    public static bool TryRead(HeroSkill skill, HeroSkillLocation location, out int count)
    {
        count = 0;
        var settings = NetworkedManagerBase<GameSettingsManager>.softInstance;
        if (skill == null || settings?.customData == null ||
            !settings.customData.TryGetValue(GetKey(skill.netId), out string payload)) return false;
        if (!Cache.TryGetValue(skill.netId, out CachedState cached) || cached.payload != payload)
        {
            if (!TryParse(payload, out int[] counts)) return false;
            cached = new CachedState { payload = payload, counts = counts };
            Cache[skill.netId] = cached;
        }
        int index = (int)location;
        if (index < 0 || index >= cached.counts.Length) return false;
        count = cached.counts[index];
        return true;
    }

    public static string Serialize(IList<int> counts)
    {
        var values = new string[6];
        for (int i = 0; i < values.Length; i++) values[i] = counts[i].ToString(CultureInfo.InvariantCulture);
        return string.Join(",", values);
    }

    public static bool TryParse(string payload, out int[] counts)
    {
        counts = null;
        if (string.IsNullOrEmpty(payload)) return false;
        string[] parts = payload.Split(',');
        if (parts.Length != 6) return false;
        var parsed = new int[6];
        for (int i = 0; i < parsed.Length; i++)
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out parsed[i]) ||
                parsed[i] < 0 || parsed[i] > Constant.MaxGemCount) return false;
        counts = parsed;
        return true;
    }

    private static string GetKey(uint id) => StatePrefix + id.ToString(CultureInfo.InvariantCulture);

    // 结算同样使用原有字典保存扩展值，原版容量列表保持四槽兼容并可正常存档。
    public static void CaptureResult(DewGameResult data)
    {
        if (data?.players == null) return;
        foreach (var player in data.players)
        {
            if (player.maxGemCounts == null || player.maxGemCounts.Count != 6) continue;
            player.capturedStarTooltipFields[ResultKey] = Serialize(player.maxGemCounts);
            for (int i = 0; i < player.maxGemCounts.Count; i++)
                player.maxGemCounts[i] = Math.Min(player.maxGemCounts[i], VanillaLimit);
        }
    }

    public static void ClearCache() => Cache.Clear();

    public static void Cleanup()
    {
        Cache.Clear();
        var settings = NetworkedManagerBase<GameSettingsManager>.softInstance;
        if (!NetworkServer.active || settings?.customData == null) return;
        foreach (string key in new List<string>(settings.customData.Keys))
            // 撤回基础槽后的快照保留至房间结束，供新程序集恢复超过四槽的奖励差值。
            if (key.StartsWith("DewGemSlotCount::confirmed:v1:", StringComparison.Ordinal) ||
                key == "DewGemSlotCount::capabilities:v1") settings.customData.Remove(key);
    }
}
