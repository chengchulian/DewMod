using System;
using UnityEngine;

namespace DewMorePlayers;

internal static class DewMorePlayersUiCleanup
{
    public const string MaxPlayersGroupName = "Field Group - Max Players";
    public const string ContinueDialogName = "Continue Player Count Dialog";

    public static void CleanupAll()
    {
        ContinuePlayerCountDialog.Close();

        foreach (UI_Title_CreateLobbyView view in Resources.FindObjectsOfTypeAll<UI_Title_CreateLobbyView>())
        {
            if (!IsRuntimeSceneObject(view))
            {
                continue;
            }

            CleanupLegacyController(view, typeof(MorePlayersLobbyController));
            DestroyOwnedChildren(view.transform);
        }

        foreach (UI_Window window in Resources.FindObjectsOfTypeAll<UI_Window>())
        {
            if (IsRuntimeSceneObject(window)
                && string.Equals(window.name, ContinueDialogName, StringComparison.Ordinal))
            {
                UnityEngine.Object.DestroyImmediate(window.gameObject);
            }
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
            if (type == currentControllerType
                || string.Equals(type.FullName, currentControllerType.FullName, StringComparison.Ordinal))
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
            if (string.Equals(child.name, MaxPlayersGroupName, StringComparison.Ordinal))
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static bool IsRuntimeSceneObject(Component component)
    {
        return component != null && component.gameObject.scene.IsValid() && component.gameObject.scene.isLoaded;
    }
}
