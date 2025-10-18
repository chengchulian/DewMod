using DewDifficultyCustomizeMod.i18n;
using UnityEngine;

namespace DewDifficultyCustomizeMod.ui
{
    public static class AttrCustomizeConfigWindow
    {
        private static Vector2 _scrollPos;

        public static void DrawWindowContents(int windowId)
        {
            GUILayout.BeginVertical();

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Width(530), GUILayout.Height(530));

            ConfigEditorUI.DrawConfigFields();

            GUILayout.EndScrollView();

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal(GUILayout.Height(40));
            if (GUILayout.Button(LocalizationConfig.Get("save_config"), GUILayout.Height(30)))
            {
                ConfigEditorManager.SaveConfig();
            }

            if (GUILayout.Button(LocalizationConfig.Get("reset_config"), GUILayout.Height(30)))
            {
                ConfigEditorManager.ResetConfig();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, 550, 20));
        }
    }
}