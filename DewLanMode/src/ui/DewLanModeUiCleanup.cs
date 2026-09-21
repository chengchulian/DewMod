using System;
using UnityEngine;

namespace DewLanMode;

internal static class DewLanModeUiCleanup
{
    public const string CreateModeGroupName = "Field Group - Network Mode";
    public const string CreatePortGroupName = "Field Group - LAN Port";
    public const string FindModeSelectorName = "DewLanMode - Room Source Selector";
    public const string LegacyFindDropdownName = "Lobby Connection Mode Dropdown";
    public const string JoinByIpButtonName = "Join LAN With IP";
    public const string LanHintName = "LAN Lobby Hint";
    public const string JoinDialogName = "DewLanMode - Join By IP Dialog";

    private static readonly string[] OwnedObjectNames =
    {
        CreateModeGroupName,
        CreatePortGroupName,
        FindModeSelectorName,
        LegacyFindDropdownName,
        JoinByIpButtonName,
        LanHintName
    };

    public static void CleanupAll()
    {
        LanJoinDialog.Close();
        LanDiscoveryService.Instance?.CancelSearch();

        foreach (UI_Title_FindLobbyView view in Resources.FindObjectsOfTypeAll<UI_Title_FindLobbyView>())
        {
            if (!IsRuntimeSceneObject(view))
            {
                continue;
            }

            CleanupLegacyController(view, typeof(FindLobbyController));
            DestroyOwnedChildren(view.transform);
        }

        foreach (UI_Title_CreateLobbyView view in Resources.FindObjectsOfTypeAll<UI_Title_CreateLobbyView>())
        {
            if (!IsRuntimeSceneObject(view))
            {
                continue;
            }

            CleanupLegacyController(view, typeof(LanLobbyController));
            DestroyOwnedChildren(view.transform);
        }
    }

    public static void CleanupLegacyController(Component view, Type currentControllerType)
    {
        if (view == null)
        {
            return;
        }

        foreach (MonoBehaviour behaviour in view.GetComponents<MonoBehaviour>())
        {
            if (behaviour == null)
            {
                continue;
            }

            Type type = behaviour.GetType();
            if (type == currentControllerType ||
                string.Equals(type.FullName, currentControllerType.FullName, StringComparison.Ordinal))
            {
                UnityEngine.Object.DestroyImmediate(behaviour);
            }
        }
    }

    public static void DestroyOwnedChildren(Transform root)
    {
        if (root == null)
        {
            return;
        }

        for (int index = root.childCount - 1; index >= 0; index--)
        {
            Transform child = root.GetChild(index);
            DestroyOwnedChildren(child);
            if (IsOwnedObjectName(child.name))
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    public static bool IsRuntimeSceneObject(Component component)
    {
        return component != null && component.gameObject.scene.IsValid() && component.gameObject.scene.isLoaded;
    }

    private static bool IsOwnedObjectName(string value)
    {
        foreach (string name in OwnedObjectNames)
        {
            if (string.Equals(value, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
