using System;
using System.Runtime.CompilerServices;
using System.Text;
using DewLanMode;
using DewLanMode.patch;
using HarmonyLib;

// 使用真实 Harmony 和两个 MOD 的原始补丁，替身仅负责模拟游戏入口及窗口按钮。
internal static class Program
{
    private static void Main()
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        Run(false, false);
        Run(true, false);
        Run(true, true);
        Console.WriteLine("PASS: standalone, both patch orders, cancel/retry, bypass, settings and exception cleanup");
    }

    private static void Run(bool morePlayers, bool reverseOrder)
    {
        var harmony = new Harmony("DewLanMode.ContinueFlowTests");
        try
        {
            if (morePlayers && !reverseOrder) PatchMorePlayers(harmony);
            harmony.CreateClassProcessor(typeof(ContinueModePatch)).Patch();
            harmony.CreateClassProcessor(typeof(TransitionManagerPatch)).Patch();
            if (morePlayers && reverseOrder) PatchMorePlayers(harmony);

            var transition = new TransitionManager();
            ManagerBase<TransitionManager>.instance = transition;
            var title = new TitleManager();
            DewMorePlayers.ContinuePlayerCountDialog.Shown = 0;
            ContinueModeDialog.Shown = 0;
            LanModeRuntime.Enabled = false;

            // 人数先确认，再选择模式；确认模式后不得再次进入标题页或弹出任何窗口。
            Begin(title, morePlayers);
            Assert(ContinueModeDialog.Shown == 1 && transition.Starts == 0, "one mode dialog before start");
            var savedSettings = ContinueModeDialog.Pending;
            Assert(savedSettings.maxPlayers == 9, "player count preserved");
            LanModeRuntime.Enabled = true;
            ContinueModeDialog.Confirm();
            Assert(title.Entries == 1 && transition.Starts == 1, "no re-entry or repeated start");
            Assert(ReferenceEquals(savedSettings, transition.Last) && transition.Last.lanMode, "same settings and LAN preparation");
            Assert(ContinueModeDialog.Shown == 1, "mode dialog stays closed");
            Assert(DewMorePlayers.ContinuePlayerCountDialog.Shown == (morePlayers ? 1 : 0), "one player dialog");

            // 取消后再点继续，仍应重新询问模式，不能沿用上次放行状态。
            transition.state = TransitionManager.StateType.Idle;
            Begin(title, morePlayers);
            ContinueModeDialog.Cancel();
            Assert(transition.Starts == 1, "cancel does not start");
            Begin(title, morePlayers);
            Assert(ContinueModeDialog.Shown == 3, "retry shows mode dialog again");
            LanModeRuntime.Enabled = false;
            ContinueModeDialog.Confirm();
            Assert(transition.Starts == 2 && !transition.Last.lanMode, "official mode resumes once");

            // 新建大厅、客机、单人续局都不进入模式窗口。
            foreach (var settings in new[]
            {
                new DewNetworkStartSettings { networkMode = DewNetworkMode.MultiplayerHost },
                new DewNetworkStartSettings { networkMode = DewNetworkMode.Singleplayer, continueData = new DewPersistence.GameData() },
                new DewNetworkStartSettings { networkMode = DewNetworkMode.MultiplayerJoinLobby, continueData = new DewPersistence.GameData() }
            })
            {
                transition.state = TransitionManager.StateType.Idle;
                transition.PlayGame(settings);
            }
            Assert(ContinueModeDialog.Shown == 3 && transition.Starts == 5, "unrelated flows unchanged");

            // 加载中的重复请求被忽略；加载异常后同一份设置也必须重新确认。
            var retry = new DewNetworkStartSettings { networkMode = DewNetworkMode.MultiplayerHost, continueData = new DewPersistence.GameData() };
            transition.PlayGame(retry);
            Assert(ContinueModeDialog.Shown == 3 && transition.Starts == 5, "loading request ignored");
            transition.state = TransitionManager.StateType.Idle;
            transition.PlayGame(retry);
            transition.ThrowOnStart = true;
            try { ContinueModeDialog.Confirm(); throw new Exception("expected start exception"); }
            catch (InvalidOperationException) { }
            transition.ThrowOnStart = false;
            transition.PlayGame(retry);
            Assert(ContinueModeDialog.Shown == 5 && transition.Starts == 5, "exception clears approval");
            ContinueModeDialog.Cancel();
            Console.WriteLine($"PASS: morePlayers={morePlayers}, reverseOrder={reverseOrder}");
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    private static void PatchMorePlayers(Harmony harmony) => harmony.CreateClassProcessor(
        typeof(DewMorePlayers.patch.TitleManager_EnterContinueDreaming_Patch)).Patch();

    private static void Begin(TitleManager title, bool morePlayers)
    {
        title.EnterContinueDreaming();
        if (morePlayers) DewMorePlayers.patch.TitleManager_EnterContinueDreaming_Patch.Continue(title);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}

public enum DewNetworkMode { Singleplayer, MultiplayerHost, MultiplayerJoinLobby }
public class DewNetworkStartSettings
{
    public DewNetworkMode networkMode;
    public DewPersistence.GameData continueData;
    public int maxPlayers;
    public bool lanMode;
}
public static class DewPersistence
{
    public class GameData { public bool isMultiplayer = true; }
    public static T FromJson<T>(string json) where T : new() => new T();
}
public static class DewSave
{
    public static Profile profileContinue = new Profile();
    public class Profile { public string continueData = "save"; }
}
public class ManagerBase<T> { public static T instance; }
public class TitleManager
{
    public int Entries;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void EnterContinueDreaming()
    {
        Entries++;
        ManagerBase<TransitionManager>.instance.PlayGame(new DewNetworkStartSettings
        {
            networkMode = DewNetworkMode.MultiplayerHost,
            continueData = new DewPersistence.GameData(), maxPlayers = 9
        });
    }
}
public class TransitionManager
{
    public enum StateType { Idle, Loading }
    public StateType state;
    public int Starts;
    public bool ThrowOnStart;
    public DewNetworkStartSettings Last;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void PlayGame(DewNetworkStartSettings settings)
    {
        if (ThrowOnStart) throw new InvalidOperationException("simulated loading failure");
        Starts++;
        Last = settings;
        state = StateType.Loading;
    }
}
public class MessageManager { public void ShowMessage(DewMessageSettings settings) => throw new Exception("unexpected error dialog"); }
public class DewMessageSettings
{
    public enum ButtonType { Ok }
    public string rawContent;
    public ButtonType buttons;
}
namespace DewMorePlayers
{
    internal static class ContinuePlayerCountDialog
    {
        public static int Shown;
        public static void Show(TitleManager title) => Shown++;
    }
}
namespace DewLanMode
{
    internal static class ContinueModeDialog
    {
        public static int Shown;
        public static DewNetworkStartSettings Pending;
        private static TransitionManager _manager;
        public static void Show(TransitionManager manager, DewNetworkStartSettings settings)
        {
            Shown++;
            _manager = manager;
            Pending = settings;
        }
        public static void Confirm()
        {
            var settings = Pending;
            Pending = null;
            ContinueModePatch.Continue(_manager, settings);
        }
        public static void Cancel() => Pending = null;
    }
    internal static class LanModeRuntime
    {
        public static bool Enabled;
        public static void PrepareSettings(DewNetworkStartSettings settings)
        {
            if (Enabled) settings.lanMode = true;
        }
    }
    internal sealed class LanLobbyController
    {
        public static LanLobbyController Current => null;
        public bool IsLanSelected => false;
        public bool TryPrepareHost(out string error) { error = null; return true; }
    }
}
