using System;
using System.Collections.Generic;
using System.Linq;
using Entry = System.Tuple<string, string, bool, int>;
using Data = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<System.Tuple<string, string, bool, int>>>>;

public class ModMetadata { public string id; }
public class ModItem { public ModMetadata metadata; }
public class LoadedModInstance { public ModItem mod; public UnityEngine.GameObject container; }
public static class DewMod { public static List<LoadedModInstance> loadedInstances = new(); }

// 精确模拟已检查的 1.5.1 反射边界，不连接 Steam、不写真实需求文件。
namespace NineTailsCat.Mods.CatBDNeedResPing.Utils
{
    public static class ItemNeedDataManager
    {
        public static Data Disk = new();
        public static bool FailSave;
        public static Data Copy(Data source) => source.ToDictionary(h => h.Key,
            h => h.Value.ToDictionary(p => p.Key, p => new List<Entry>(p.Value)));
        public static List<Entry> LoadHeroLoadout(string hero, int page) =>
            Disk.TryGetValue(hero, out var pages) && pages.TryGetValue(page, out var entries) ? new List<Entry>(entries) : new List<Entry>();
        public static void Save(Data data) { if (!FailSave) Disk = Copy(data); }
    }
}
namespace NineTailsCat.Mods.CatBDNeedResPing.UI
{
    public class ItemNeedUIPanel
    {
        private Data _allData;
        private string[] _heroTypeNames = { "Hero_Other", "Hero_Test" };
        private int _selectedHeroIndex = 0;
        private int _selectedLoadoutIndex = 0;
        public List<Entry> Selected = new();
        public Data Data => _allData;
        public int Page => _selectedLoadoutIndex;
        public string Hero => _heroTypeNames[_selectedHeroIndex];
        public int DisplayUpdates;
        public ItemNeedUIPanel(Data data) { _allData = data; }
        private void ApplyCurrentLoadout() => Selected = _allData.TryGetValue(Hero, out var pages) && pages.TryGetValue(Page, out var entries) ? new List<Entry>(entries) : new List<Entry>();
        public void SaveSelection()
        {
            _allData[Hero][Page] = new List<Entry>(Selected);
            Utils.ItemNeedDataManager.Save(_allData);
        }
        private void UpdateHeroDisplay() { DisplayUpdates++; }
        private void UpdateLoadoutDisplay() { DisplayUpdates++; }
    }
}
namespace NineTailsCat.Mods.CatBDNeedResPing.Controllers
{
    public class ItemNeedController : UnityEngine.MonoBehaviour
    {
        public static ItemNeedController Instance;
        private UI.ItemNeedUIPanel _panel;
        private Data _cachedData;
        public UI.ItemNeedUIPanel Panel => _panel;
        public Data Cache => _cachedData;
        private void EnsurePanel()
        {
            _cachedData ??= Utils.ItemNeedDataManager.Copy(Utils.ItemNeedDataManager.Disk);
            _panel ??= new UI.ItemNeedUIPanel(_cachedData);
        }
    }
}
