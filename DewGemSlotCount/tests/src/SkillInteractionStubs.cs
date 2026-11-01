using System;
using Mirror;

// 仅记录交互路由和装备副作用；原版身份确认与动画仍需在游戏中验收。
public interface IInteractable { void OnInteract(Entity entity, bool alt); }
public enum Rarity { Common, Identity }
public enum CenterMessageType { Error }
public class SkillTrigger : IInteractable
{
    public Rarity rarity;
    public bool isCharacterSkill, locked;
    public bool isServer = true;
    public string characterSkillOwner;
    public int originalCalls, dismantleTaps;
    public bool IsLockedFor(DewPlayer player) => locked;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    void IInteractable.OnInteract(Entity entity, bool alt) { originalCalls++; }
    private void CmdDoDismantleTap(NetworkConnectionToClient sender = null) { dismantleTaps++; }
}
public class St_D_RegressionIdentity : SkillTrigger { }
public partial class HeroSkill
{
    public SkillTrigger Q, W, E, R, Movement, Identity;
    public SkillTrigger holdingObject;
    public int equipCalls, pickupCalls, holdCalls;
    public SkillTrigger GetSkill(HeroSkillLocation type) => type switch
    {
        HeroSkillLocation.Q => Q,
        HeroSkillLocation.W => W,
        HeroSkillLocation.E => E,
        HeroSkillLocation.R => R,
        HeroSkillLocation.Identity => Identity,
        HeroSkillLocation.Movement => Movement,
        _ => null
    };
    public void EquipSkill(HeroSkillLocation type, SkillTrigger skill)
    {
        equipCalls++;
        // 与游戏一致：不可编辑的空槽也拒绝自动装备。
        if (!CanReplaceSkill(type)) return;
        switch (type)
        {
            case HeroSkillLocation.Q: Q = skill; break;
            case HeroSkillLocation.W: W = skill; break;
            case HeroSkillLocation.E: E = skill; break;
            case HeroSkillLocation.R: R = skill; break;
            case HeroSkillLocation.Identity: Identity = skill; break;
            case HeroSkillLocation.Movement: Movement = skill; break;
        }
    }
    private void RpcInvokeOnSkillPickup(SkillTrigger skill) { pickupCalls++; }
    public void HoldInHand(SkillTrigger skill) { holdCalls++; holdingObject = skill; pickupCalls++; }
}
public class ControlManager
{
    public bool isDismantleDisabled;
    public Func<SkillTrigger, bool> dismantleConstraint;
}
public class FeedbackManager
{
    public int calls;
    public void PlayFeedbackEffect(string key) { calls++; }
}
public class InGameUIManager
{
    public static InGameUIManager instance = new();
    public void ShowCenterMessage(CenterMessageType type, string key) { }
}
