using System.Collections;
using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Rooms;

namespace CharacterModIsolation.Patches;

internal static class ActPatches
{
    public static void EncountersPostfix(ref IEnumerable<EncounterModel> __result)
    {
        try
        {
            if (!FilterPolicy.EncountersActive || __result is null)
                return;

            var before = __result.ToList();
            var kept = before.Where(static encounter => !FilterPolicy.ShouldRemoveEncounter(encounter.GetType())).ToList();
            if (kept.Count == before.Count)
                return;

            if (kept.Count == 0)
            {
                Plugin.Logger.Warn("[filter] all encounters would be removed; left unchanged");
                return;
            }

            __result = kept;
            FilterLog.Removed("encounters", before.Count - kept.Count, kept.Count);
        }
        catch (Exception ex)
        {
            Plugin.Logger.Error($"[filter] encounters failed, left unchanged: {ex}");
        }
    }

    public static void MonstersPostfix(ref IEnumerable<MonsterModel> __result)
    {
        try
        {
            if (!FilterPolicy.EncountersActive || __result is null)
                return;

            var before = __result.ToList();
            var kept = before.Where(static monster => !FilterPolicy.ShouldRemoveMonster(monster.GetType())).ToList();
            if (kept.Count == before.Count)
                return;

            if (kept.Count == 0)
            {
                Plugin.Logger.Warn("[filter] all monsters would be removed; left unchanged");
                return;
            }

            __result = kept;
            FilterLog.Removed("monsters", before.Count - kept.Count, kept.Count);
        }
        catch (Exception ex)
        {
            Plugin.Logger.Error($"[filter] monsters failed, left unchanged: {ex}");
        }
    }

    public static void EventsPostfix(ref IEnumerable<EventModel> __result)
    {
        try
        {
            if (!FilterPolicy.EventsActive || __result is null)
                return;

            var before = __result.ToList();
            var kept = before.Where(static evt => !FilterPolicy.ShouldRemoveEvent(evt.GetType())).ToList();
            if (kept.Count == before.Count)
                return;

            if (kept.Count == 0)
            {
                Plugin.Logger.Warn("[filter] all events would be removed; left unchanged");
                return;
            }

            __result = kept;
            FilterLog.Removed("events", before.Count - kept.Count, kept.Count);
        }
        catch (Exception ex)
        {
            Plugin.Logger.Error($"[filter] events failed, left unchanged: {ex}");
        }
    }

    public static void AncientsPostfix(ref IEnumerable<AncientEventModel> __result)
    {
        try
        {
            if (!FilterPolicy.AncientsActive || __result is null)
                return;

            var before = __result.ToList();
            var kept = before.Where(static ancient => !FilterPolicy.ShouldRemoveAncient(ancient.GetType())).ToList();
            if (kept.Count == before.Count)
                return;

            if (kept.Count == 0)
            {
                Plugin.Logger.Warn("[filter] all ancients would be removed; left unchanged");
                return;
            }

            __result = kept;
            FilterLog.Removed("ancients", before.Count - kept.Count, kept.Count);
        }
        catch (Exception ex)
        {
            Plugin.Logger.Error($"[filter] ancients failed, left unchanged: {ex}");
        }
    }

    public static void ActsByIndexPostfix(ref IReadOnlyList<IReadOnlyList<ActModel>> __result)
    {
        try
        {
            if (!FilterPolicy.ActsActive || __result is null)
                return;

            var before = __result;
            var filtered = new List<IReadOnlyList<ActModel>>(before.Count);
            var removed = 0;

            for (var i = 0; i < before.Count; i++)
            {
                var bucket = before[i];
                var kept = bucket.Where(static act => !FilterPolicy.ShouldRemoveAct(act.GetType())).ToList();
                removed += bucket.Count - kept.Count;

                if (kept.Count == 0)
                {
                    var fallback = DefaultAct(i);
                    if (fallback is not null)
                        kept.Add(fallback);
                }

                filtered.Add(kept.Count > 0 ? kept : bucket);
            }

            if (removed == 0)
                return;

            __result = filtered;
            FilterLog.Removed("acts-index", removed, filtered.Count);
        }
        catch (Exception ex)
        {
            Plugin.Logger.Error($"[filter] acts-by-index failed, left unchanged: {ex}");
        }
    }

    public static void GetRandomListPostfix(ref IEnumerable<ActModel> __result)
    {
        try
        {
            if (!FilterPolicy.ActsActive || __result is null)
            {
                RunContext.ClearPending();
                return;
            }

            var before = __result.ToList();
            var kept = new List<ActModel>(before.Count);
            var removed = 0;

            foreach (var act in before)
            {
                if (!FilterPolicy.ShouldRemoveAct(act.GetType()))
                {
                    kept.Add(act);
                    continue;
                }

                removed++;
                var fallback = DefaultAct(act.Index);
                if (fallback is not null)
                {
                    kept.Add(fallback);
                }
                else
                {
                    kept.Add(act);
                    Plugin.Logger.Warn($"[filter] no fallback act for index={act.Index}; kept original");
                }
            }

            __result = kept;
            if (removed > 0)
                FilterLog.Removed("acts", removed, kept.Count);
        }
        catch (Exception ex)
        {
            Plugin.Logger.Error($"[filter] act selection failed, left unchanged: {ex}");
        }
        finally
        {
            RunContext.ClearPending();
        }
    }

    public static void GenerateRoomsPrefix(ActModel __instance)
    {
        try
        {
            if (!FilterPolicy.GenerationActive)
                return;

            var rooms = GetRooms(__instance);
            var subsetField = typeof(ActModel).GetField("_sharedAncientSubset", BindingFlags.NonPublic | BindingFlags.Instance);
            if (rooms is null || subsetField?.GetValue(__instance) is not IList subset)
                return;

            var before = subset.Count;
            for (var i = subset.Count - 1; i >= 0; i--)
            {
                if (subset[i] is AncientEventModel ancient && FilterPolicy.ShouldRemoveAncient(ancient.GetType()))
                    subset.RemoveAt(i);
            }

            if (subset.Count != before)
                FilterLog.Removed("ancient-subset", before - subset.Count, subset.Count);
        }
        catch (Exception ex)
        {
            Plugin.Logger.Error($"[filter] GenerateRooms prefix failed: {ex}");
        }
    }

    public static void GenerateRoomsPostfix(ActModel __instance)
    {
        try
        {
            if (!FilterPolicy.GenerationActive)
                return;

            var rooms = GetRooms(__instance);
            if (rooms is null)
                return;

            FilterEventList(rooms.events);
            FilterEncounterList(rooms.normalEncounters);
            FilterEncounterList(rooms.eliteEncounters);

            if (rooms.HasAncient && FilterPolicy.ShouldRemoveAncient(rooms.Ancient.GetType()))
            {
                var fallback = FindSafeAncient(__instance);
                if (fallback is not null)
                {
                    rooms.Ancient = fallback;
                    FilterLog.Removed("room-ancient", 1, 1);
                }
            }

            if (FilterPolicy.ShouldRemoveEncounter(rooms.Boss.GetType()))
            {
                var fallback = FindSafeBoss(__instance);
                if (fallback is not null)
                {
                    rooms.Boss = fallback;
                    FilterLog.Removed("room-boss", 1, 1);
                }
            }

            if (rooms.SecondBoss is not null && FilterPolicy.ShouldRemoveEncounter(rooms.SecondBoss.GetType()))
            {
                var fallback = FindSafeBoss(__instance);
                if (fallback is not null)
                {
                    rooms.SecondBoss = fallback;
                    FilterLog.Removed("room-second-boss", 1, 1);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.Error($"[filter] GenerateRooms postfix failed: {ex}");
        }
    }

    public static void PullNextEventPrefix(ActModel __instance)
    {
        try
        {
            if (!FilterPolicy.EventsActive)
                return;

            var rooms = GetRooms(__instance);
            if (rooms is null)
                return;

            FilterEventList(rooms.events);
        }
        catch (Exception ex)
        {
            Plugin.Logger.Error($"[filter] PullNextEvent prefix failed: {ex}");
        }
    }

    public static void BeginRunLocallyPrefix(StartRunLobby __instance)
    {
        try
        {
            RunContext.SetPendingFromLobby(__instance);
        }
        catch (Exception ex)
        {
            Plugin.Logger.Warn($"[context] BeginRunLocally prefix failed: {ex.Message}");
        }
    }

    public static void KaleidoscopePostfix(Kaleidoscope __instance, Player player, ref bool __result)
    {
        try
        {
            if (__result || !FilterPolicy.CardsActive)
                return;
            if (!__instance.IsAllowed(player.RunState))
                return;

            var all = ModelDb.AllCharacters.ToList();
            var removed = all.Count(static character => FilterPolicy.ShouldRemoveCharacter(character.GetType()));
            if (removed == 0)
                return;

            if (player.UnlockState.CharacterCardPools.Count() + removed == all.Count)
            {
                __result = true;
                FilterLog.Removed("kaleidoscope-count", 0, 1);
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.Error($"[filter] Kaleidoscope failed, left unchanged: {ex}");
        }
    }

    private static void FilterEventList(List<EventModel> list)
    {
        var before = list.Count;
        var kept = list.Where(static evt => !FilterPolicy.ShouldRemoveEvent(evt.GetType())).ToList();
        if (kept.Count == 0)
        {
            Plugin.Logger.Warn("[filter] event list would become empty; left unchanged");
            return;
        }

        if (kept.Count == before)
            return;

        list.Clear();
        list.AddRange(kept);
        FilterLog.Removed("room-events", before - kept.Count, kept.Count);
    }

    private static void FilterEncounterList(List<EncounterModel> list)
    {
        var before = list.Count;
        var kept = list.Where(static encounter => !FilterPolicy.ShouldRemoveEncounter(encounter.GetType())).ToList();
        if (kept.Count == 0)
        {
            Plugin.Logger.Warn("[filter] encounter list would become empty; left unchanged");
            return;
        }

        if (kept.Count == before)
            return;

        list.Clear();
        list.AddRange(kept);
        FilterLog.Removed("room-encounters", before - kept.Count, kept.Count);
    }

    private static AncientEventModel? FindSafeAncient(ActModel act)
    {
        try
        {
            return act.AllAncients.Concat(ModelDb.AllSharedAncients)
                .FirstOrDefault(static ancient => !FilterPolicy.ShouldRemoveAncient(ancient.GetType()));
        }
        catch
        {
            return null;
        }
    }

    private static EncounterModel? FindSafeBoss(ActModel act)
    {
        try
        {
            return act.AllBossEncounters.FirstOrDefault(static encounter =>
                !FilterPolicy.ShouldRemoveEncounter(encounter.GetType()));
        }
        catch
        {
            return null;
        }
    }

    private static RoomSet? GetRooms(ActModel act)
    {
        try
        {
            return typeof(ActModel)
                .GetField("_rooms", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(act) as RoomSet;
        }
        catch
        {
            return null;
        }
    }

    private static ActModel? DefaultAct(int index)
    {
        try
        {
            return ModelDb.Acts.FirstOrDefault(act =>
                act.Index == index && act.IsDefault && !FilterPolicy.ShouldRemoveActForRun(act.GetType()));
        }
        catch
        {
            return null;
        }
    }
}
