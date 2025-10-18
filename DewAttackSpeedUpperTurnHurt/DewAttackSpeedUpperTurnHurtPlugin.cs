
using UnityEngine;

namespace DewAttackSpeedUpperTurnHurt;


public class DewAttackSpeedUpperTurnHurtPlugin : ModBehaviour
{    private void Awake()
    {
        harmony.PatchAll();
    }

    private void Start()
    {
        Debug.Log($"[{mod.metadata.id}] 已加载: {mod.metadata.name} by {mod.metadata.author}");
    }

    private void OnDestroy()
    {
        harmony.UnpatchAll(harmony.Id);
    }

}