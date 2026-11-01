using System;
using System.Collections.Generic;
using System.Linq;

namespace DewShopShare.controller;

/// <summary>单个商人的共享记录；商品数组仍由游戏原有的 SyncDictionary 保存和同步。</summary>
internal sealed class SharedShopState
{
    internal sealed class Offer
    {
        public MerchandiseData Item;
        public int Remaining;
        public PurchaseAttempt Pending;
    }

    internal sealed class PurchaseAttempt
    {
        internal Offer Offer;
        internal string Player;
        internal int Index;
        internal int BeforeCount;
        internal bool ConsumptionCaptured;
        internal bool Completed;
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

        Offer offer = new Offer { Item = item, Remaining = item.count };
        _offers.Add(offer);
        slots[index] = offer;

        // 写回整个新数组，触发 Mirror 的字典变更；保留所有原有下标，兼容在途购买和标记。
        foreach (string player in inventories.Keys.ToArray())
        {
            AppendMissingOffers(inventories, player);
        }

        return true;
    }

    // 购买事件可能立即刷新商店，覆盖原数组前先结算该次扣库存，再保存引用识别刷新结果。
    public MerchandiseData[] CaptureInventory(IDictionary<string, MerchandiseData[]> inventories, string player)
    {
        if (!inventories.TryGetValue(player, out MerchandiseData[] items) || items == null)
        {
            return null;
        }

        foreach (Offer offer in _offers)
        {
            if (offer.Pending != null && offer.Pending.Player == player)
            {
                CaptureConsumption(inventories, offer.Pending);
                SynchronizeOffer(inventories, offer);
            }
        }

        return items;
    }

    // 刷新只更换个人原生商品；共享商品保留。晚加入者读取当前全队剩余库存，售罄不补货。
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

    // 进入原生购买前使用房主库存，并锁住同一来源，防止购买事件重入和售罄后的过期请求。
    public bool BeginPurchase(IDictionary<string, MerchandiseData[]> inventories, string player, int index,
        out PurchaseAttempt attempt)
    {
        attempt = null;
        if (string.IsNullOrEmpty(player) || !_slots.TryGetValue(player, out Dictionary<int, Offer> slots) ||
            !slots.TryGetValue(index, out Offer offer) ||
            !inventories.TryGetValue(player, out MerchandiseData[] items) || items == null ||
            index < 0 || index >= items.Length || !SameItem(offer.Item, items[index]))
        {
            return true;
        }

        if (offer.Pending != null)
        {
            return false;
        }

        if (offer.Remaining <= 0)
        {
            SynchronizeOffer(inventories, offer);
            return false;
        }

        attempt = new PurchaseAttempt
        {
            Offer = offer, Player = player, Index = index, BeforeCount = offer.Remaining
        };
        offer.Pending = attempt;
        try
        {
            SynchronizeOffer(inventories, offer);
            return true;
        }
        catch
        {
            offer.Pending = null;
            throw;
        }
    }

    // 原生扣库存才算成功；余额不足、购买条件失败或扣库存前抛异常均不消耗全队库存。
    public void FinishPurchase(IDictionary<string, MerchandiseData[]> inventories, PurchaseAttempt attempt)
    {
        if (attempt == null || attempt.Completed)
        {
            return;
        }

        attempt.Completed = true;
        Offer offer = attempt.Offer;
        try
        {
            CaptureConsumption(inventories, attempt);
            SynchronizeOffer(inventories, offer);
        }
        finally
        {
            offer.Pending = null;
        }
    }

    // 一次原生购买只结算一次，避免购买事件刷新数组后遗漏扣库存或重复扣库存。
    private static void CaptureConsumption(IDictionary<string, MerchandiseData[]> inventories,
        PurchaseAttempt attempt)
    {
        if (attempt.ConsumptionCaptured ||
            !inventories.TryGetValue(attempt.Player, out MerchandiseData[] items) || items == null ||
            attempt.Index >= items.Length || !SameItem(attempt.Offer.Item, items[attempt.Index]))
        {
            return;
        }

        int consumed = Math.Max(0, attempt.BeforeCount - Math.Max(0, items[attempt.Index].count));
        if (consumed > 0)
        {
            attempt.Offer.Remaining = Math.Max(0, attempt.Offer.Remaining - consumed);
            attempt.ConsumptionCaptured = true;
        }
    }

    // 原商品与所有共享槽位写回同一个数量，保留下标，用原生同步通知未安装 Mod 的客机。
    private void SynchronizeOffer(IDictionary<string, MerchandiseData[]> inventories, Offer offer)
    {
        foreach (KeyValuePair<string, Dictionary<int, Offer>> player in _slots.ToArray())
        {
            if (!inventories.TryGetValue(player.Key, out MerchandiseData[] items) || items == null)
            {
                continue;
            }

            MerchandiseData[] updated = null;
            foreach (KeyValuePair<int, Offer> slot in player.Value)
            {
                if (slot.Value != offer || slot.Key >= items.Length ||
                    !SameItem(offer.Item, items[slot.Key]) || items[slot.Key].count == offer.Remaining)
                {
                    continue;
                }

                if (updated == null) updated = (MerchandiseData[])items.Clone();
                updated[slot.Key].count = offer.Remaining;
            }

            if (updated != null) inventories[player.Key] = updated;
        }
    }

    // 各玩家使用独立数组显示同一份库存，扣款、折扣、生成物品仍全部交给原生购买流程。
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
            item.count = offer.Remaining;
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
