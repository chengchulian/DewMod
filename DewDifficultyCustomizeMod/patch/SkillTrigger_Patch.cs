using DewDifficultyCustomizeMod.config;
using HarmonyLib;
using System.Reflection;

[HarmonyPatch(typeof(SkillTrigger))]
public class SkillTrigger_Patch
{
    private static readonly MethodInfo cmdDoDismantleTapMethod =
        AccessTools.Method(typeof(SkillTrigger), "CmdDoDismantleTap");

    private static readonly MethodInfo cmdEquipIdentitySkillMethod =
        AccessTools.Method(typeof(SkillTrigger), "CmdEquipIdentitySkill");

    private static readonly MethodInfo rpcInvokeOnSkillPickupMethod =
        AccessTools.Method(typeof(HeroSkill), "RpcInvokeOnSkillPickup");

    [HarmonyPrefix]
    [HarmonyPatch("IInteractable.OnInteract")]
    public static bool OnInteract_Prefix(SkillTrigger __instance, Entity entity, bool alt)
    {
        if (!(entity is Hero hero) || __instance.IsLockedFor(entity.owner))
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
                cmdDoDismantleTapMethod?.Invoke(__instance, new object[] { null });
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

        // 如果不启用所有技能编辑，单独处理 Identity 技能
        if (!AttrCustomizeResources.Config.enableAllSkillEdit)
        {
            if (__instance.rarity == Rarity.Identity)
            {
                if (!entity.isOwned || !(entity is Hero h))
                    return false;

                string msg = string.Format(
                    DewLocalization.GetUIValue("InGame_Message_ConfirmEmbraceNewIdentity"),
                    h.Skill.Identity.GetFormattedSkillTitle(),
                    __instance.GetFormattedSkillTitle());

                ManagerBase<MessageManager>.instance.ShowMessage(new DewMessageSettings
                {
                    owner = __instance,
                    rawContent = msg,
                    buttons = DewMessageSettings.ButtonType.Yes | DewMessageSettings.ButtonType.No,
                    defaultButton = DewMessageSettings.ButtonType.No,
                    destructiveConfirm = true,
                    onClose = delegate(DewMessageSettings.ButtonType b)
                    {
                        if (b == DewMessageSettings.ButtonType.Yes)
                        {
                            cmdEquipIdentitySkillMethod?.Invoke(__instance, new object[] { null });
                        }
                    },
                    validator = () => !__instance.IsNullOrInactive() &&
                                      InGameUIManager.ValidateInGameActionMessage()
                });

                return false;
            }
        }

        HeroSkillLocation? emptySkill = null;

        if (__instance.isCharacterSkill && __instance.GetType().Name.StartsWith("St_R_") && hero.Skill.R == null)
            emptySkill = HeroSkillLocation.R;
        else if (hero.Skill.Q == null)
            emptySkill = HeroSkillLocation.Q;
        else if (hero.Skill.W == null)
            emptySkill = HeroSkillLocation.W;
        else if (hero.Skill.E == null)
            emptySkill = HeroSkillLocation.E;
        else if (hero.Skill.R == null)
            emptySkill = HeroSkillLocation.R;

        if (emptySkill.HasValue)
        {
            if (__instance.isServer)
            {
                hero.Skill.EquipSkill(emptySkill.Value, __instance);

                // 安全调用 RpcInvokeOnSkillPickup
                rpcInvokeOnSkillPickupMethod?.Invoke(hero.Skill, new object[] { __instance });
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