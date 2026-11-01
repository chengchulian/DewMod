using System;
using System.Reflection;

namespace DewBuildAssistant.util;

// 可选接入转职 Mod 的公开映射接口；不增加程序集依赖，也不读取客户端本地开关。
internal static class IdentityChangeCompatibility
{
    private static Type _mapper;
    private static Func<bool> _enabled;
    private static Func<string, string> _storageHero;

    private static void Bind()
    {
        if (_mapper != null) return;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType("DewIdentityChange.config.LoadoutPageMapper", false);
            if (type == null) continue;
            var getter = type.GetProperty("IsIdentityEnabled", BindingFlags.Public | BindingFlags.Static)?.GetGetMethod();
            var storage = type.GetMethod("GetStorageHeroType", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (getter == null || storage == null) throw new InvalidOperationException("Identity Change loadout API is missing.");
            _enabled = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), getter);
            _storageHero = (Func<string, string>)Delegate.CreateDelegate(typeof(Func<string, string>), storage);
            _mapper = type;
            return;
        }
    }

    public static bool Enabled { get { Bind(); return _enabled?.Invoke() == true; } }

    public static string StorageHero(string hero)
    {
        Bind();
        return _storageHero == null ? hero : _storageHero(hero);
    }
}
