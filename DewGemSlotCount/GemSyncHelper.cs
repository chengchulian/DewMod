using System;
using DewGemSlotCount.config;
using UnityEngine;

namespace DewGemSlotCount
{
    [Serializable]
    public sealed class GemConfigSyncSnapshot
    {
        public const int CurrentProtocolVersion = 2;

        public int ProtocolVersion = CurrentProtocolVersion;
        public uint Revision;
        public int SkillQGemCount;
        public int SkillWGemCount;
        public int SkillEGemCount;
        public int SkillRGemCount;
        public int SkillIdentityGemCount;
        public int SkillMovementGemCount;
        public int SkillQCorruptedChaosMaxGemCount;
        public int SkillWCorruptedChaosMaxGemCount;
        public int SkillECorruptedChaosMaxGemCount;
        public int SkillRCorruptedChaosMaxGemCount;
        public int SkillIdentityCorruptedChaosMaxGemCount;
        public int SkillMovementCorruptedChaosMaxGemCount;
        public bool EditIdentitySkill;
        public bool EditMovementSkill;
        public bool AllowMovementCorruptedChaos;
        public bool GemNoMerge;

        public static GemConfigSyncSnapshot FromConfig(PluginConfig config, uint revision)
        {
            return new GemConfigSyncSnapshot
            {
                Revision = revision,
                SkillQGemCount = config.SkillQGemCount,
                SkillWGemCount = config.SkillWGemCount,
                SkillEGemCount = config.SkillEGemCount,
                SkillRGemCount = config.SkillRGemCount,
                SkillIdentityGemCount = config.SkillIdentityGemCount,
                SkillMovementGemCount = config.SkillMovementGemCount,
                SkillQCorruptedChaosMaxGemCount = config.SkillQCorruptedChaosMaxGemCount,
                SkillWCorruptedChaosMaxGemCount = config.SkillWCorruptedChaosMaxGemCount,
                SkillECorruptedChaosMaxGemCount = config.SkillECorruptedChaosMaxGemCount,
                SkillRCorruptedChaosMaxGemCount = config.SkillRCorruptedChaosMaxGemCount,
                SkillIdentityCorruptedChaosMaxGemCount = config.SkillIdentityCorruptedChaosMaxGemCount,
                SkillMovementCorruptedChaosMaxGemCount = config.SkillMovementCorruptedChaosMaxGemCount,
                EditIdentitySkill = config.EditIdentitySkill,
                EditMovementSkill = config.EditMovementSkill,
                AllowMovementCorruptedChaos = config.AllowMovementCorruptedChaos,
                GemNoMerge = config.GemNoMerge
            };
        }

        public void ApplyTo(PluginConfig config)
        {
            config.SkillQGemCount = SkillQGemCount;
            config.SkillWGemCount = SkillWGemCount;
            config.SkillEGemCount = SkillEGemCount;
            config.SkillRGemCount = SkillRGemCount;
            config.SkillIdentityGemCount = SkillIdentityGemCount;
            config.SkillMovementGemCount = SkillMovementGemCount;
            config.SkillQCorruptedChaosMaxGemCount = SkillQCorruptedChaosMaxGemCount;
            config.SkillWCorruptedChaosMaxGemCount = SkillWCorruptedChaosMaxGemCount;
            config.SkillECorruptedChaosMaxGemCount = SkillECorruptedChaosMaxGemCount;
            config.SkillRCorruptedChaosMaxGemCount = SkillRCorruptedChaosMaxGemCount;
            config.SkillIdentityCorruptedChaosMaxGemCount = SkillIdentityCorruptedChaosMaxGemCount;
            config.SkillMovementCorruptedChaosMaxGemCount = SkillMovementCorruptedChaosMaxGemCount;
            config.EditIdentitySkill = EditIdentitySkill;
            config.EditMovementSkill = EditMovementSkill;
            config.AllowMovementCorruptedChaos = AllowMovementCorruptedChaos;
            config.GemNoMerge = GemNoMerge;
        }
    }

    public static class GemSyncHelper
    {
        public const string SyncKey = "DewGemSlotCount::config:v2";

        public static string Serialize(GemConfigSyncSnapshot snapshot)
        {
            return JsonUtility.ToJson(snapshot);
        }

        public static bool TryDeserialize(
            string payload,
            out GemConfigSyncSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            error = null;

            if (string.IsNullOrWhiteSpace(payload))
            {
                error = "payload is empty";
                return false;
            }

            try
            {
                snapshot = JsonUtility.FromJson<GemConfigSyncSnapshot>(payload);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }

            if (snapshot == null)
            {
                error = "payload did not contain a snapshot";
                return false;
            }

            if (snapshot.ProtocolVersion != GemConfigSyncSnapshot.CurrentProtocolVersion)
            {
                error =
                    "unsupported protocol version " + snapshot.ProtocolVersion +
                    " (expected " + GemConfigSyncSnapshot.CurrentProtocolVersion + ")";
                snapshot = null;
                return false;
            }

            return true;
        }
    }
}
