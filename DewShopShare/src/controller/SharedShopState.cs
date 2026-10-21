using System;
using System.Collections.Generic;
using System.Linq;

namespace DewShopShare.controller;

/// <summary>单个商人的共享记录；商品数组仍由游戏原有的 SyncDictionary 保存和同步。</summary>
internal sealed class SharedShopState
{
    private sealed class Offer
    {
        public MerchandiseData Item;
        public readonly Dictionary<string, int> Remaining = new Dictionary<string, int>();
    }

    private readonly List<Offer> _offers = new List<Offer>();
    private readonly Dictionary<string, Dictionary<int, Offer>> _slots =
        new Dictionary<string, Dictionary<int, Offer>>();

    // 只接受服务端库存中的有效商品。原商品和追加的副本都指向同一记录，阻止二次传播。
    public bool Share(IDictionary<string, MerchandiseData[]> inventories, string sender, int index)
    {
        if (string.IsNullOrEmpty(sender) || !inventories.TryGetValue(sender, out MerchandiseData[] items) ||
            items == null || index < 0 || index >= items.Length)
        {
            return false;
        }

        MerchandiseData item = items[index];
        if (item.type == MerchandiseType.Empty || string.IsNullOrEmpty(item.itemName) || item.count <= 0)
        {
            return false;
        }

        Dictionary<int, Offer> slots = GetSlots(sender);
        if (slots.TryGetValue(index, out Offer existing) && SameItem(existing.Item, item))
        {
            return false;
        }

        Offer offer = new Offer { Item = item };
        offer.Remaining[sender] = item.count;
        _offers.Add(offer);
        slots[index] = offer;

        // 写回整个新数组，触发 Mirror 的字典变更；保留所有原有下标，兼容在途购买和标记。
        foreach (string player in inventories.Keys.ToArray())
        {
            AppendMissingOffers(inventories, player);
        }

        return true;
    }

    // 刷新前保存每个人实际剩余的库存，包含标记者原始槽位的购买结果。
    public MerchandiseData[] CaptureInventory(IDictionary<string, MerchandiseData[]> inventories, string player)
    {
        if (!inventories.TryGetValue(player, out MerchandiseData[] items) || items == null)
        {
            return null;
        }

        if (_slots.TryGetValue(player, out Dictionary<int, Offer> slots))
        {
            foreach (KeyValuePair<int, Offer> slot in slots)
            {
                if (slot.Key < items.Length && SameItem(slot.Value.Item, items[slot.Key]))
                {
                    slot.Value.Remaining[player] = Math.Max(0, items[slot.Key].count);
                }
            }
        }

        return items;
    }

    // 刷新只更换个人原生商品；共享商品保留，售罄数量也保留。新加入者获得初始共享库存。
    public void OnInventoryPopulated(IDictionary<string, MerchandiseData[]> inventories, string player,
        MerchandiseData[] previous)
    {
        if (!inventories.TryGetValue(player, out MerchandiseData[] current) || current == null ||
            ReferenceEquals(previous, current))
        {
            return;
        }

        _slots[player] = new Dictionary<int, Offer>();
        AppendMissingOffers(inventories, player);
    }

    // 每位玩家使用独立的数组和数量，扣款、折扣、生成物品仍全部交给原生购买流程。
    private void AppendMissingOffers(IDictionary<string, MerchandiseData[]> inventories, string player)
    {
        if (!inventories.TryGetValue(player, out MerchandiseData[] items) || items == null)
        {
            return;
        }

        Dictionary<int, Offer> slots = GetSlots(player);
        HashSet<Offer> present = new HashSet<Offer>(slots.Where(p => p.Key < items.Length &&
            SameItem(p.Value.Item, items[p.Key])).Select(p => p.Value));
        List<Offer> missing = _offers.Where(offer => !present.Contains(offer)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        MerchandiseData[] updated = new MerchandiseData[items.Length + missing.Count];
        Array.Copy(items, updated, items.Length);
        for (int i = 0; i < missing.Count; i++)
        {
            Offer offer = missing[i];
            MerchandiseData item = offer.Item;
            if (!offer.Remaining.TryGetValue(player, out int count))
            {
                count = item.count;
                offer.Remaining[player] = count;
            }

            item.count = count;
            int index = items.Length + i;
            updated[index] = item;
            slots[index] = offer;
        }

        inventories[player] = updated;
    }

    // 玩家 GUID 在断线重连后保持稳定，库存记录按 GUID 而非连接对象保存。
    private Dictionary<int, Offer> GetSlots(string player)
    {
        if (!_slots.TryGetValue(player, out Dictionary<int, Offer> slots))
        {
            slots = new Dictionary<int, Offer>();
            _slots.Add(player, slots);
        }

        return slots;
    }

    // 数量会随购买变化，其余字段用于防止其他 Mod 替换数组后误认旧槽位。
    private static bool SameItem(MerchandiseData left, MerchandiseData right)
    {
        return left.type == right.type && left.itemName == right.itemName && left.level == right.level &&
               left.price.Equals(right.price) && left.customData == right.customData;
    }
}
