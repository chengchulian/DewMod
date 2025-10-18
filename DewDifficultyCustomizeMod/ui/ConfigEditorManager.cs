using DewDifficultyCustomizeMod.config;

namespace DewDifficultyCustomizeMod.ui
{
    public static class ConfigEditorManager
    {


        public static void SaveConfig()
        {
            AttrCustomizeResources.SaveConfig();
        }

        public static void ResetConfig()
        {
            AttrCustomizeResources.ResetConfig();
        }
    }
}
