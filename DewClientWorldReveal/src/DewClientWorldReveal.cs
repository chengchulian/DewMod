using UnityEngine;

namespace DewClientWorldReveal;

public class DewClientWorldReveal : ModBehaviour
{
    private void Start()
    {
        harmony.PatchAll();
        Debug.Log($"[{mod.metadata.id}] 客机千里眼已加载");
    }

    private void OnDestroy()
    {
        harmony.UnpatchAll(harmony.Id);
    }
}
