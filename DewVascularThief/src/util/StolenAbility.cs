namespace DewVascularThief.util;

internal readonly struct StolenAbility
{
    public readonly Hero Hero;
    public readonly AbilityTrigger Ability;
    public readonly int Index;
    public readonly string SourceAbilityType;

    public StolenAbility(Hero hero, AbilityTrigger ability, int index, string sourceAbilityType)
    {
        Hero = hero;
        Ability = ability;
        Index = index;
        SourceAbilityType = sourceAbilityType;
    }
}
