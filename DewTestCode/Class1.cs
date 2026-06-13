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
    }
}
