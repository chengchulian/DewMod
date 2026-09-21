using DewSuperSmart.config;
using HarmonyLib;
using UnityEngine;

namespace DewSuperSmart;

public class DewSuperSmart : ModBehaviour
{
    public static DewSuperSmart Instance;
    public readonly PluginConfig Config = new PluginConfig();

    private SkillRangeDisplay _skillRangeDisplay;
    private MonsterThreatRangeDisplay _monsterThreatRangeDisplay;
    private AutoDodgeController _autoDodgeController;
    private ThreatSnapshotProvider _threatSnapshotProvider;
    private Harmony _harmony;

    internal ThreatSnapshotProvider Threats => _threatSnapshotProvider;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        LocalizationSource.Init(this);
        _harmony = new Harmony("com.chengchulian.DewMod.DewSuperSmart.ThreatTracking");
        SafeZoneRpcPatch.Install(_harmony);

        _threatSnapshotProvider = gameObject.GetComponent<ThreatSnapshotProvider>() ?? gameObject.AddComponent<ThreatSnapshotProvider>();
        _skillRangeDisplay = gameObject.GetComponent<SkillRangeDisplay>() ?? gameObject.AddComponent<SkillRangeDisplay>();
        _monsterThreatRangeDisplay = gameObject.GetComponent<MonsterThreatRangeDisplay>() ?? gameObject.AddComponent<MonsterThreatRangeDisplay>();
        _autoDodgeController = gameObject.GetComponent<AutoDodgeController>() ?? gameObject.AddComponent<AutoDodgeController>();

        Debug.Log($"[{mod.metadata.id}] {LocalizationSource.GetLocalizationText("Log.Loaded", mod.metadata.name, mod.metadata.author)}");
    }

    private void OnDestroy()
    {
        if (_harmony != null)
        {
            _harmony.UnpatchAll(_harmony.Id);
            _harmony = null;
        }

        SafeZoneTracker.Clear();

        if (_skillRangeDisplay != null)
        {
            Destroy(_skillRangeDisplay);
            _skillRangeDisplay = null;
        }

        if (_monsterThreatRangeDisplay != null)
        {
            Destroy(_monsterThreatRangeDisplay);
            _monsterThreatRangeDisplay = null;
        }

        if (_autoDodgeController != null)
        {
            Destroy(_autoDodgeController);
            _autoDodgeController = null;
        }

        if (_threatSnapshotProvider != null)
        {
            Destroy(_threatSnapshotProvider);
            _threatSnapshotProvider = null;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
}
