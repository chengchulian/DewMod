using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DewLucidDreamExpansion.controller;

internal static class ShrinePresentation
{
    // 原版启蒙祭坛没有独立 model；完整复制子层级才能保留可见特效和网络路径。
    internal static void Copy(Shrine source, Shrine target)
    {
        target.gameObject.layer = source.gameObject.layer;
        target.transform.localScale = source.transform.localScale;
        var objects = new Dictionary<GameObject, GameObject> { [source.gameObject] = target.gameObject };
        for (int i = 0; i < source.transform.childCount; i++)
        {
            Transform child = source.transform.GetChild(i);
            GameObject copy = Object.Instantiate(child.gameObject, target.transform, false);
            MapHierarchy(child, copy.transform, objects);
        }
        target.model = Remap(source.model, objects);
        target.availableEffect = Remap(source.availableEffect, objects);
        target.unavailableEffect = Remap(source.unavailableEffect, objects);
        target.lockedEffect = Remap(source.lockedEffect, objects);
        target.unlockEffect = Remap(source.unlockEffect, objects);
        target.useLockedEffect = Remap(source.useLockedEffect, objects);
        target.useEffect = Remap(source.useEffect, objects);
        target.useEffectLocal = Remap(source.useEffectLocal, objects);
        target.mapVisibility = source.mapVisibility;
        target.interactableDelay = source.interactableDelay;
        target.useCooldown = source.useCooldown;
        target.unavailableByDefault = source.unavailableByDefault;
    }

    private static void MapHierarchy(Transform source, Transform target, Dictionary<GameObject, GameObject> objects)
    {
        objects.Add(source.gameObject, target.gameObject);
        for (int i = 0; i < source.childCount; i++)
            MapHierarchy(source.GetChild(i), target.GetChild(i), objects);
    }

    private static GameObject Remap(GameObject source, Dictionary<GameObject, GameObject> objects)
    {
        if (source == null) return null;
        return objects.TryGetValue(source, out GameObject copy) ? copy : source;
    }
}
