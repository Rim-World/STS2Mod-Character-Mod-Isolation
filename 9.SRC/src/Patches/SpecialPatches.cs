using MegaCrit.Sts2.Core.Models;

namespace CharacterModIsolation.Patches;

/// <summary>
/// Scoped filter for vanilla code that reads <see cref="ModelDb.AllCharacters"/> directly.
/// Only Orobas' Sea Glass options need this today; the scope avoids mutating global caches.
/// </summary>
internal static class SpecialPatches
{
    [ThreadStatic]
    private static int _depth;

    public static void OrobasPrefix()
    {
        if (FilterPolicy.CardsActive)
            _depth++;
    }

    public static void OrobasFinalizer()
    {
        if (_depth > 0)
            _depth--;
    }

    public static void AllCharactersPostfix(ref IEnumerable<CharacterModel> __result)
    {
        if (_depth <= 0 || !FilterPolicy.CardsActive || __result is null)
            return;

        var before = __result.ToList();
        var kept = before.Where(static character => !FilterPolicy.ShouldRemoveCharacter(character.GetType())).ToList();
        if (kept.Count == before.Count)
            return;

        if (kept.Count == 0)
        {
            Plugin.Logger.Warn("[filter] Orobas character list would be empty; left unchanged");
            return;
        }

        __result = kept;
        FilterLog.Removed("orobas-characters", before.Count - kept.Count, kept.Count);
    }
}
