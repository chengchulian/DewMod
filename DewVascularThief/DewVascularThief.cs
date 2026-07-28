using DewVascularThief.util;
using DewVascularThief.localization;
using UnityEngine;

namespace DewVascularThief;

public class DewVascularThief : ModBehaviour
{
    public static DewVascularThief Instance { get; private set; }

    internal VascularThiefController Controller { get; private set; }

    private void Awake()
    {
        Instance = this;
        Controller = new VascularThiefController();
    }

    private void Start()
    {
        instance.isAlteringGameplay = true;
        VascularThiefI18n.Initialize(this);
        VascularThiefSkillIcons.Initialize(this);
        harmony.PatchAll();
        Debug.Log($"[{mod.metadata.id}] Loaded {mod.metadata.name} by {mod.metadata.author}");
        VascularThiefSkillFactory.Register();
    }

    private void OnDestroy()
    {
        VascularThiefSkillFactory.Unregister();
        Controller?.CleanupAllStolenAbilities();
        harmony.UnpatchAll(harmony.Id);

        if (Instance == this)
        {
            Instance = null;
        }
    }
}
