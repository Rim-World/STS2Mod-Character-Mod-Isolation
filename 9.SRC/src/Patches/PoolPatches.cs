using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Models;

namespace CharacterModIsolation.Patches;

internal static class PoolPatches
{
    private sealed class SourceCache
    {
        public CardModel[]? Source;
        public CardModel[]? Filtered;
        public int PolicyEpoch;
        public int SettingsVersion;
    }

    private static readonly ConditionalWeakTable<CardPoolModel, SourceCache> SourceCaches = new();

    public static void CardPoolSourcePostfix(CardPoolModel __instance, ref IEnumerable<CardModel> __result)
    {
        try
        {
            if (!FilterPolicy.CardsActive || __result is not CardModel[] source)
                return;

            var policyEpoch = RunContext.PolicyEpoch;
            var settingsVersion = ModSettings.Version;
            var cache = SourceCaches.GetOrCreateValue(__instance);
            if (!ReferenceEquals(cache.Source, source) ||
                cache.PolicyEpoch != policyEpoch ||
                cache.SettingsVersion != settingsVersion)
            {
                cache.Source = source;
                cache.PolicyEpoch = policyEpoch;
                cache.SettingsVersion = settingsVersion;
                cache.Filtered = source
                    .Where(static card => !FilterPolicy.ShouldRemoveCard(card.GetType()))
                    .ToArray();

                var removed = source.Length - cache.Filtered.Length;
                if (removed > 0)
                    FilterLog.Removed("cards-source", removed, cache.Filtered.Length);
            }

            if (cache.Filtered is { } filtered && filtered.Length != source.Length)
                __result = filtered;
        }
        catch (Exception ex)
        {
            Plugin.Logger.Error($"[filter] card source failed, left unchanged: {ex}");
        }
    }

    public static void CardPoolPostfix(CardPoolModel __instance, ref IEnumerable<CardModel> __result)
    {
        try
        {
            if (!FilterPolicy.CardsActive || __result is null)
                return;

            if (FilterPolicy.ShouldEmptyCardPool(__instance.GetType()))
            {
                __result = Array.Empty<CardModel>();
                FilterLog.Removed("card-pool", 1, 0);
                return;
            }

            var before = __result.ToList();
            var kept = before.Where(static card => !ShouldRemove(card)).ToList();
            if (kept.Count == before.Count)
                return;

            __result = kept;
            FilterLog.Removed("cards", before.Count - kept.Count, kept.Count);
        }
        catch (Exception ex)
        {
            Plugin.Logger.Error($"[filter] card pool failed, left unchanged: {ex}");
        }
    }

    public static void RelicPoolPostfix(RelicPoolModel __instance, ref IEnumerable<RelicModel> __result)
    {
        try
        {
            if (!FilterPolicy.RelicsActive || __result is null)
                return;

            if (FilterPolicy.ShouldEmptyRelicPool(__instance.GetType()))
            {
                __result = Array.Empty<RelicModel>();
                FilterLog.Removed("relic-pool", 1, 0);
                return;
            }

            var before = __result.ToList();
            var kept = before.Where(static relic => !FilterPolicy.ShouldRemoveRelic(relic.GetType())).ToList();
            if (kept.Count == before.Count)
                return;

            __result = kept;
            FilterLog.Removed("relics", before.Count - kept.Count, kept.Count);
        }
        catch (Exception ex)
        {
            Plugin.Logger.Error($"[filter] relic pool failed, left unchanged: {ex}");
        }
    }

    public static void PotionPoolPostfix(PotionPoolModel __instance, ref IEnumerable<PotionModel> __result)
    {
        try
        {
            if (!FilterPolicy.PotionsActive || __result is null)
                return;

            if (FilterPolicy.ShouldEmptyPotionPool(__instance.GetType()))
            {
                __result = Array.Empty<PotionModel>();
                FilterLog.Removed("potion-pool", 1, 0);
                return;
            }

            var before = __result.ToList();
            var kept = before.Where(static potion => !FilterPolicy.ShouldRemovePotion(potion.GetType())).ToList();
            if (kept.Count == before.Count)
                return;

            __result = kept;
            FilterLog.Removed("potions", before.Count - kept.Count, kept.Count);
        }
        catch (Exception ex)
        {
            Plugin.Logger.Error($"[filter] potion pool failed, left unchanged: {ex}");
        }
    }

    private static bool ShouldRemove(CardModel card)
    {
        if (FilterPolicy.ShouldRemoveCard(card.GetType()))
            return true;

        try
        {
            return card.Pool is not null && FilterPolicy.ShouldRemoveCard(card.Pool.GetType());
        }
        catch
        {
            return false;
        }
    }
}
