namespace DewIdentityChange.config;

public sealed class LoadoutSnapshot
{
    public AssetRef<SkillTrigger>[] Q;
    public AssetRef<SkillTrigger>[] R;
    public AssetRef<SkillTrigger>[] Trait;
    public AssetRef<SkillTrigger>[] Movement;

    public static AssetRef<SkillTrigger>[] Clone(AssetRef<SkillTrigger>[] src)
    {
        if (src == null) return null;
        var arr = new AssetRef<SkillTrigger>[src.Length];
        src.CopyTo(arr, 0);
        return arr;
    }

    public static LoadoutSnapshot Capture(HeroSkill hs)
    {
        return new LoadoutSnapshot
        {
            Q = Clone(hs.loadoutQ),
            R = Clone(hs.loadoutR),
            Trait = Clone(hs.loadoutTrait),
            Movement = Clone(hs.loadoutMovement)
        };
    }

    public void Restore(HeroSkill hs)
    {
        hs.loadoutQ        = Clone(Q);
        hs.loadoutR        = Clone(R);
        hs.loadoutTrait    = Clone(Trait);
        hs.loadoutMovement = Clone(Movement);
    }
}
