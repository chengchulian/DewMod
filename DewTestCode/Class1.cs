namespace DewTestCode;

public class Class1
{
    public static void Main(string[] args)
    {
        SpawnAnityaShrine();
    }

    private static void SpawnAnityaShrine()
    {
        
        // 生成商人
        Dew.CreateActor<PropEnt_Merchant_Jonas>(DewConsoleCommands.GetCursorWorldPos(), null);
        
        // 堕落混沌
        Dew.CreateActor<Shrine_CorruptedChaos>(DewConsoleCommands.GetCursorWorldPos(), null);

        
        Dew.CreateActor<Shrine_Anitya>(DewConsoleCommands.GetCursorWorldPos(), null);
        //生成迷你传送门
        Dew.CreateActor<Shrine_MiniRift>(DewConsoleCommands.GetCursorWorldPos(), null);
        // 生成技能 血管小偷
        Dew.CreateSkillTrigger<St_U_VascularThief>(DewConsoleCommands.GetCursorWorldPos(), 1, null);
        // 生成突破祭坛
        Dew.CreateActor<Shrine_Ascension>(DewConsoleCommands.GetCursorWorldPos(), null);
    }
}