namespace DewVascularThief.util;

internal static class VascularThiefSkillFactory
{
    public static VascularThiefResourceRegistry Resources { get; } = new VascularThiefResourceRegistry();

    public static void Register()
    {
        Resources.Register();
        VascularThiefProfileRegistry.Register();

        if (NetworkedManagerBase<LootManager>.softInstance != null)
        {
            VascularThiefLootRegistry.Register(NetworkedManagerBase<LootManager>.instance);
        }
    }

    public static void Unregister()
    {
        Resources.Unregister();
    }
}
