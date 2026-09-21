using DewHeroSkillJonas.config;
using DewHeroSkillJonas.util;
using UnityEngine;

namespace DewHeroSkillJonas
{
    public class DewHeroSkillJonas : ModBehaviour
    {
        public static DewHeroSkillJonas Instance;
        public readonly PluginConfig Config = new PluginConfig();

        public void Awake()
        {
            Instance = this;
        }

        public void Start()
        {
            LocalizationSource.Init(this);

            harmony.PatchAll();
            Debug.Log($"[{mod.metadata.id}] 已加载: {mod.metadata.name} by {mod.metadata.author}");

            CallOnNetworkedManager<ZoneManager>(PropEnt_Merchant_HeroSkill.ZoneManagerOnStart);
        }



        public void OnDestroy()
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }
}
