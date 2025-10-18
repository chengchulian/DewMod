using UnityEngine;

namespace DewDifficultyCustomizeMod.ui
{
    public static class ConfigFieldDrawer
    {
        private static int Width = 350;
        public static void DrawIntField(string label, ref int value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(Width));
            string input = GUILayout.TextField(value.ToString(), GUILayout.Width(80));
            if (int.TryParse(input, out int result))
                value = result;
            GUILayout.EndHorizontal();
        }

        public static void DrawFloatField(string label, ref float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(Width));
            string input = GUILayout.TextField(value.ToString("F2"), GUILayout.Width(80));
            if (float.TryParse(input, out float result))
                value = result;
            GUILayout.EndHorizontal();
        }

        public static void DrawPercentageField(string label, ref float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(Width));
            string input = GUILayout.TextField((value * 100f).ToString("F2"), GUILayout.Width(80));
            if (float.TryParse(input, out float result))
                value = result / 100f;
            GUILayout.Label("%", GUILayout.Width(20));
            GUILayout.EndHorizontal();
        }
    }
}
