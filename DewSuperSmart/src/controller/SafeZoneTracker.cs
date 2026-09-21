using System.Collections.Generic;
using UnityEngine;

namespace DewSuperSmart;

internal static class SafeZoneTracker
{
    private static readonly Dictionary<int, Entry> Entries = new Dictionary<int, Entry>();

    private sealed class Entry
    {
        public Actor Actor;
        public readonly List<Vector3> Centers = new List<Vector3>();
    }

    // 读取有效角色的安全区中心，并清除过期记录。
    public static bool TryGetCenters(Actor actor, out IReadOnlyList<Vector3> centers)
    {
        centers = null;
        if (actor == null)
        {
            return false;
        }

        int id = actor.GetInstanceID();
        if (!Entries.TryGetValue(id, out Entry entry))
        {
            return false;
        }

        if (entry.Actor == null || entry.Actor != actor || entry.Actor.IsNullOrInactive())
        {
            Entries.Remove(id);
            return false;
        }

        centers = entry.Centers;
        return entry.Centers.Count > 0;
    }

    // 卸载 Mod 时清空所有安全区记录。
    public static void Clear()
    {
        Entries.Clear();
    }

    // 接收补丁转发的中心点，同一角色的相近坐标仅记录一次。
    internal static void RecordCenter(object instance, Vector3 pos)
    {
        if (instance is not Actor actor || !Dew.IsOkay(pos))
        {
            return;
        }

        int id = actor.GetInstanceID();
        if (!Entries.TryGetValue(id, out Entry entry) || entry.Actor != actor)
        {
            entry = new Entry { Actor = actor };
            Entries[id] = entry;
        }

        for (int i = 0; i < entry.Centers.Count; i++)
        {
            if ((entry.Centers[i].ToXY() - pos.ToXY()).sqrMagnitude < 0.01f)
            {
                return;
            }
        }

        entry.Centers.Add(pos);
    }
}
