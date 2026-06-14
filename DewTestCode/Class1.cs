namespace DewTestCode;

public class Class1
{
    public static void Main(string[] args)
    {
        SpawnAnityaShrine();
    }

    private static void SpawnAnityaShrine()
    {
        Dew.CreateActor<Shrine_Anitya>(DewConsoleCommands.GetCursorWorldPos(), null);

        Dew.CreateSkillTrigger<St_Q_Fleche>(DewConsoleCommands.GetCursorWorldPos(),1, null);
    }
}
