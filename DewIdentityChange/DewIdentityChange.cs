using DewIdentityChange.config;
using UnityEngine;

namespace DewIdentityChange;

public class DewIdentityChange : ModBehaviour
{
    public static DewIdentityChange Instance;

    public readonly PluginConfig config = new PluginConfig();

    private IdentityConfigSync _sync;

    public bool IsIdentityEnabled => _sync?.IsIdentityEnabled ?? config.enable;

    private void Awake()
    {
        Instance = this;
        _sync = new IdentityConfigSync(this, config, ApplyConfigEffects);
    }

    private void Start()
    {
        instance.isAlteringGameplay = true;
        LocalizationSource.Init(this);
        harmony.PatchAll();
        Debug.Log($"[{mod.metadata.id}] Loaded {mod.metadata.name} by {mod.metadata.author}");

        LoadoutSnapshot.CaptureLoadedHeroSkills();
        ApplyConfigEffects();
        CallOnManager<LobbyManager>(_sync.LobbyManagerOnStart, _sync.LobbyManagerOnStop);
        CallOnNetworkedManager<GameSettingsManager>(_sync.GameSettingsManagerOnStartClient, _sync.GameSettingsManagerOnStopClient);

        DewPlayer.onHumanPlayerAdded -= _sync.OnHumanPlayerAdded;
        DewPlayer.onHumanPlayerAdded += _sync.OnHumanPlayerAdded;
    }

    public override void OnConfigChanged()
    {
        _sync.SyncOrLockConfig("config changed");
        ApplyConfigEffects();
    }

    public void ApplyConfigEffects()
    {
        LoadoutSnapshot.Switch(IsIdentityEnabled);
        LoadoutPageMapper.EnsureProfileLoadoutPages(DewSave.profileMain);
        LoadoutPageMapper.RefreshLobbyAfterConfigChange();
    }

    private void OnDestroy()
    {
        if (_sync != null)
        {
            DewPlayer.onHumanPlayerAdded -= _sync.OnHumanPlayerAdded;
            _sync.Dispose();
        }

        LoadoutSnapshot.RestoreCaptured();
        harmony.UnpatchAll(harmony.Id);
    }
}
