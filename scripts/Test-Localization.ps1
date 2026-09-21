$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path $env:TEMP ('dewmod-localization-' + [Guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($testRoot) | Out-Null
$harness = @'
using System;
using System.IO;
using System.Reflection;
using System.Text;

// 用最小游戏对象替身验证真实本地化源码，不启动 Unity。
public class ModBehaviour { public Mod mod; }
public class Mod { public string path; }
public class Profile { public string language; }
public static class DewSave { public static Profile profileMain = new Profile(); }
namespace UnityEngine
{
    public static class Debug
    {
        // 记录测试中的预期加载错误。
        public static void LogWarning(object value) { }
        public static void LogError(object value) { }
    }
    public class Transform
    {
        public object[] children = new object[0];
        // 返回本地化控件测试数据。
        public T[] GetComponentsInChildren<T>(bool includeInactive)
        {
            return Array.ConvertAll(children, item => (T)item);
        }
    }
}
namespace TMPro { public class TMP_Text { public string text; } }
namespace DewVascularThief.config { internal static class VascularThiefText { public const string ModKey = "Test"; } }

internal static class Program
{
    private static int checks;
    // 比较实际输出，失败时让测试进程返回非零。
    private static void Equal(string actual, string expected)
    {
        if (actual != expected) throw new Exception("预期：" + expected + "；实际：" + actual);
        checks++;
    }
    // 调用不同项目中的统一入口，覆盖新增入口及重命名入口。
    private static string Text(Type type, string key, params object[] args)
    {
        return (string)type.GetMethod("GetLocalizationText").Invoke(null, new object[] { key, args });
    }
    // 使用 UTF-8 固定样本验证中文、回退、格式化、控件及重复初始化。
    public static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        string dataRoot = Path.Combine(args[0], "data");
        string localeRoot = Path.Combine(dataRoot, "i18n");
        Directory.CreateDirectory(localeRoot);
        File.WriteAllText(Path.Combine(localeRoot, "en-US.json"), "{\"hello\":\"Hello {0}\",\"englishOnly\":\"Fallback\",\"bad\":\"{3}\",\"unicode\":\"English\"}", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(localeRoot, "zh-CN.json"), "{\"hello\":\"你好 {0}\",\"unicode\":\"中文\"}", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(localeRoot, "de-DE.json"), "bad json", new UTF8Encoding(false));
        string emptyRoot = Path.Combine(args[0], "empty");
        Directory.CreateDirectory(Path.Combine(emptyRoot, "i18n"));
        Type[] types = {
            typeof(DewModConfigListSupport.config.LocalizationSource),
            typeof(DewShieldModDetection.config.LocalizationSource),
            typeof(DewTestCode.config.LocalizationSource),
            typeof(DewVascularThief.config.LocalizationSource)
        };
        foreach (Type type in types)
        {
            MethodInfo init = type.GetMethod("Init");
            init.Invoke(null, new object[] { new ModBehaviour { mod = new Mod { path = dataRoot } } });
            DewSave.profileMain = new Profile { language = "zh-CN" };
            Equal(Text(type, "hello", "世界"), "你好 世界");
            Equal(Text(type, "unicode"), "中文");
            Equal(Text(type, "englishOnly"), "Fallback");
            Equal(Text(type, "missing"), "missing");
            Equal(Text(type, "bad", 1), "{3}");
            MethodInfo localize = type.GetMethod("LocalizeUI");
            if (localize != null)
            {
                var text = new TMPro.TMP_Text { text = "unicode" };
                localize.Invoke(null, new object[] { new UnityEngine.Transform { children = new object[] { text } } });
                Equal(text.text, "中文");
            }
            DewSave.profileMain.language = "xx-XX";
            Equal(Text(type, "hello", "world"), "Hello world");
            DewSave.profileMain = null;
            Equal(Text(type, "unicode"), "English");
            init.Invoke(null, new object[] { new ModBehaviour { mod = new Mod { path = emptyRoot } } });
            Equal(Text(type, "unicode"), "unicode");
        }
        Console.WriteLine("本地化行为验证通过：" + types.Length + " 个入口，" + checks + " 项断言。");
        return 0;
    }
}
'@
$harnessPath = Join-Path $testRoot 'Program.cs'
Set-Content -LiteralPath $harnessPath -Value $harness -Encoding utf8NoBOM
$sources = @($harnessPath)
foreach ($mod in @('DewModConfigListSupport', 'DewShieldModDetection', 'DewTestCode', 'DewVascularThief')) {
    $sources += Join-Path $repoRoot ($mod + '/src/config/LocalizationSource.cs')
}
# 在 PowerShell 的 .NET 运行时中执行，避免独立进程依赖 Unity 的运行时程序集布局。
$references = @(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' | ForEach-Object FullName)
$references += [Newtonsoft.Json.JsonConvert].Assembly.Location
$types = Add-Type -Path $sources -ReferencedAssemblies $references -CompilerOptions '/nowarn:1701' -PassThru
$program = $types[0].Assembly.GetType('Program')
$invokeArguments = [object[]]::new(1)
$invokeArguments[0] = [string[]]@($testRoot)
$result = $program.GetMethod('Main').Invoke($null, $invokeArguments)
if ($result -ne 0) { throw '本地化行为验证失败。' }
