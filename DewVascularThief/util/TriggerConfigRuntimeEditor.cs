using System.Reflection;

namespace DewVascularThief.util;

internal static class TriggerConfigRuntimeEditor
{
    private static readonly FieldInfo ParentField = typeof(TriggerConfig).GetField("_parent", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo ManaCostField = typeof(TriggerConfig).GetField("_manaCost", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo MaxChargesField = typeof(TriggerConfig).GetField("_maxCharges", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo AddedChargesField = typeof(TriggerConfig).GetField("_addedCharges", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo CooldownTimeField = typeof(TriggerConfig).GetField("_cooldownTime", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo MinimumDelayField = typeof(TriggerConfig).GetField("_minimumDelay", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo IsConfigDirtyField = typeof(AbilityTrigger).GetField("_isConfigDirty", BindingFlags.NonPublic | BindingFlags.Instance);

    public static void Attach(AbilityTrigger parent, TriggerConfig config)
    {
        if (config != null)
        {
            ParentField?.SetValue(config, parent);
        }
    }

    public static void AttachAll(AbilityTrigger parent)
    {
        if (parent?.configs == null)
        {
            return;
        }

        foreach (TriggerConfig config in parent.configs)
        {
            Attach(parent, config);
        }
    }

    public static void SetManaCost(TriggerConfig config, float value)
    {
        if (config == null)
        {
            return;
        }

        ManaCostField?.SetValue(config, value);
        MarkDirty(config);
    }

    public static void SetMaxCharges(TriggerConfig config, int value)
    {
        if (config == null)
        {
            return;
        }

        MaxChargesField?.SetValue(config, value);
        MarkDirty(config);
    }

    public static void SetAddedCharges(TriggerConfig config, int value)
    {
        if (config == null)
        {
            return;
        }

        AddedChargesField?.SetValue(config, value);
        MarkDirty(config);
    }

    public static void SetCooldownTime(TriggerConfig config, float value)
    {
        if (config == null)
        {
            return;
        }

        CooldownTimeField?.SetValue(config, value);
        MarkDirty(config);
    }

    public static void SetMinimumDelay(TriggerConfig config, float value)
    {
        if (config == null)
        {
            return;
        }

        MinimumDelayField?.SetValue(config, value);
        MarkDirty(config);
    }

    private static void MarkDirty(TriggerConfig config)
    {
        if (ParentField?.GetValue(config) is AbilityTrigger parent)
        {
            IsConfigDirtyField?.SetValue(parent, true);
        }
    }
}
