using System.Reflection;
using HarmonyLib;

namespace DewGemSlotCount.patch;

[HarmonyPatch(typeof(SkillTrigger))]
public class SkillTrigger_Patch
{
    private static readonly MethodInfo cmdDoDismantleTapMethod =
        AccessTools.Method(typeof(SkillTrigger), "CmdDoDismantleTap");

    private static readonly MethodInfo rpcInvokeOnSkillPickupMethod =
        AccessTools.Method(typeof(HeroSkill), "RpcInvokeOnSkillPickup");

    // 空槽还必须允许编辑；否则装备会被游戏拒绝，并阻断后续的手持替换流程。
    private static bool IsAvailableSlot(HeroSkill skill, HeroSkillLocation location)
    {
        return skill.GetSkill(location) == null && skill.CanReplaceSkill(location);
    }

    [HarmonyPrefix]
    [HarmonyPatch("IInteractable.OnInteract")]
    public static bool OnInteract_Prefix(SkillTrigger __instance, Entity entity, bool alt)
    {
        if (DewGemSlotCount.Instance == null) return true;
        // 未开启身份编辑时保留原版拾取流程，兼容旧身份已被毁灭之口吞掉的空槽。
        // 原版同时负责旅人类型校验、无旧身份的确认提示及服务端装备动画。
        if (__instance.rarity == Rarity.Identity && !DewGemSlotCount.Instance.GameplayConfig.EditIdentitySkill)
            return true;
        if (entity is not Hero hero || __instance.IsLockedFor(entity.owner))
        {
            return false; // 阻止原方法执行
        }

        if (alt)
        {
            if (hero.isOwned &&
                !ManagerBase<ControlManager>.instance.isDismantleDisabled &&
                (ManagerBase<ControlManager>.instance.dismantleConstraint == null ||
                 ManagerBase<ControlManager>.instance.dismantleConstraint(__instance)))
            {
                // 安全调用 CmdDoDismantleTap
                cmdDoDismantleTapMethod?.Invoke(__instance, [null]);
            }

            return false;
        }

        if (__instance.isCharacterSkill &&
            !string.IsNullOrEmpty(__instance.characterSkillOwner) &&
            __instance.characterSkillOwner != hero.owner.guid)
        {
            if (entity.isOwned)
            {
                InGameUIManager.instance.ShowCenterMessage(
                    CenterMessageType.Error,
                    "InGame_Message_CanOnlyEquipYourOwnCharacterSkill");
            }

            return false;
        }

        HeroSkillLocation? emptySkill = null;

        if (__instance.isCharacterSkill && __instance.GetType().Name.StartsWith("St_R_") && IsAvailableSlot(hero.Skill, HeroSkillLocation.R))
            emptySkill = HeroSkillLocation.R;

        else if (__instance.GetType().Name.StartsWith("St_M_") && IsAvailableSlot(hero.Skill, HeroSkillLocation.Movement))
            emptySkill = HeroSkillLocation.Movement;

        else if (__instance.GetType().Name.StartsWith("St_D_") && IsAvailableSlot(hero.Skill, HeroSkillLocation.Identity))
            emptySkill = HeroSkillLocation.Identity;

        else if (IsAvailableSlot(hero.Skill, HeroSkillLocation.Q))
            emptySkill = HeroSkillLocation.Q;
        else if (IsAvailableSlot(hero.Skill, HeroSkillLocation.W))
            emptySkill = HeroSkillLocation.W;
        else if (IsAvailableSlot(hero.Skill, HeroSkillLocation.E))
            emptySkill = HeroSkillLocation.E;
        else if (IsAvailableSlot(hero.Skill, HeroSkillLocation.R))
            emptySkill = HeroSkillLocation.R;
        else if (IsAvailableSlot(hero.Skill, HeroSkillLocation.Movement))
            emptySkill = HeroSkillLocation.Movement;
        else if (IsAvailableSlot(hero.Skill, HeroSkillLocation.Identity))
            emptySkill = HeroSkillLocation.Identity;

        if (emptySkill.HasValue)
        {
            if (__instance.isServer)
            {
                hero.Skill.EquipSkill(emptySkill.Value, __instance);

                // 安全调用 RpcInvokeOnSkillPickup
                rpcInvokeOnSkillPickupMethod?.Invoke(hero.Skill, [__instance]);
            }

            if (entity.isOwned)
            {
                ManagerBase<FeedbackManager>.instance.PlayFeedbackEffect("UI_EditSkill_SkillEquip");
            }
        }
        else if (__instance.isServer)
        {
            hero.Skill.HoldInHand(__instance);
        }

        return false; // 阻止原方法执行
    }
}
