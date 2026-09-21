using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace DewSafeShare.util;

public static class SafeShareController
{
    private const float DefaultVisibleSecondsAfterPing = 10f;

    private static readonly Dictionary<Actor, int> RelockTokens = new Dictionary<Actor, int>();
    private static readonly Dictionary<Actor, DewPlayer> RevealOwners = new Dictionary<Actor, DewPlayer>();
    private static int _nextRelockToken;

    public struct PingState
    {
        public IItem Item;
        public DewPlayer Owner;
        public bool ShouldScheduleRelock;
    }

    public static void LockDroppedItem(IItem item, DewPlayer owner)
    {
        if (!NetworkServer.active || owner == null || !IsGroundItem(item))
        {
            return;
        }

        Actor actor = (Actor)item;
        InvalidateReveal(actor);
        item.tempOwner = owner;
    }

    public static PingState CapturePingState(PingManager.Ping ping, DewPlayer sender)
    {
        PingState state = default;
        if (sender == null || ping.target is not IItem item || !IsGroundItem(item))
        {
            return state;
        }

        Actor actor = (Actor)item;
        bool isOwnerUnlockPing = item.tempOwner == sender;
        bool isOwnerRefreshPing = item.tempOwner == null &&
                                  RevealOwners.TryGetValue(actor, out DewPlayer revealOwner) &&
                                  revealOwner == sender;

        if (!isOwnerUnlockPing && !isOwnerRefreshPing)
        {
            return state;
        }

        state.Item = item;
        state.Owner = sender;
        state.ShouldScheduleRelock = true;
        return state;
    }

    public static void ScheduleRelockAfterPing(PingState state)
    {
        if (!state.ShouldScheduleRelock || state.Owner == null || !IsGroundItem(state.Item) || state.Item.tempOwner != null)
        {
            return;
        }

        Actor actor = (Actor)state.Item;
        int token = ++_nextRelockToken;
        RelockTokens[actor] = token;
        RevealOwners[actor] = state.Owner;

        float seconds = GetVisibleSecondsAfterPing();
        if (DewSafeShare.Instance != null)
        {
            DewSafeShare.Instance.StartCoroutine(RelockAfterDelay(actor, state.Owner, token, seconds));
        }
        else
        {
            Dew.CallDelayed(() => TryRelock(actor, state.Owner, token), Mathf.CeilToInt(seconds * 60f));
        }
    }

    private static IEnumerator RelockAfterDelay(Actor actor, DewPlayer owner, int token, float seconds)
    {
        if (seconds > 0f)
        {
            yield return new WaitForSeconds(seconds);
        }

        TryRelock(actor, owner, token);
    }

    private static void TryRelock(Actor actor, DewPlayer owner, int token)
    {
        if (!NetworkServer.active || actor == null || owner == null || !RelockTokens.TryGetValue(actor, out int currentToken) || currentToken != token)
        {
            return;
        }

        RelockTokens.Remove(actor);
        RevealOwners.Remove(actor);

        if (actor is not IItem item || !IsGroundItem(item) || item.tempOwner != null)
        {
            return;
        }

        item.tempOwner = owner;
    }

    private static void InvalidateReveal(Actor actor)
    {
        if (actor == null)
        {
            return;
        }

        RelockTokens.Remove(actor);
        RevealOwners.Remove(actor);
    }

    private static bool IsGroundItem(IItem item)
    {
        if (item == null || item is not Actor actor || actor == null || !actor.isActive)
        {
            return false;
        }

        return item.owner == null && item.handOwner == null;
    }

    private static float GetVisibleSecondsAfterPing()
    {
        return Mathf.Max(0f, DewSafeShare.Instance?.config.VisibleSecondsAfterPing ?? DefaultVisibleSecondsAfterPing);
    }
}
