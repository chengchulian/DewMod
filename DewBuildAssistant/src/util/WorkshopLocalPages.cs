using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DewBuildAssistant.model;

namespace DewBuildAssistant.util;

internal static class WorkshopLocalPages
{
    // 只从当前账号的本地更新记录关联栏位，不下载配装或向工坊写入栏位值。
    public static void Annotate(IEnumerable<GuideSummary> guides, string root, ulong owner)
    {
        var records = new List<Tuple<string, string, int>>();
        string[] folders = Array.Empty<string>();
        try { if (Directory.Exists(root)) folders = Directory.GetDirectories(root); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        foreach (string folder in folders)
        {
            string name = Path.GetFileName(folder);
            int split = name.LastIndexOf('-');
            if (split <= 0 || !int.TryParse(name.Substring(split + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int slot)) continue;
            try
            {
                string path = Path.Combine(folder, "publishedfileid.txt");
                if (!File.Exists(path) || new FileInfo(path).Length > 64) continue;
                if (ulong.TryParse(File.ReadAllText(path, Encoding.UTF8).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out ulong id) && id > 0)
                    records.Add(Tuple.Create("workshop:" + id.ToString(CultureInfo.InvariantCulture), name.Substring(0, split), slot));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        foreach (var guide in guides)
        {
            guide.IsOwned = owner != 0 && guide.OwnerSteamId == owner;
            guide.LocalUploadSlot = -1;
            if (!guide.IsOwned || string.IsNullOrEmpty(guide.Hero)) continue;
            var slots = records.Where(r => r.Item1 == guide.Page &&
                (guide.RequiresIdentityChange
                    ? r.Item2 != guide.Hero && r.Item2.EndsWith("_" + guide.Hero, StringComparison.Ordinal)
                    : r.Item2 == guide.Hero)).Select(r => r.Item3).Distinct().ToArray();
            // 跨设备或关联冲突时保留未知，不把当前应用栏位当作上传栏位。
            if (slots.Length == 1) guide.LocalUploadSlot = slots[0];
        }
    }
}
