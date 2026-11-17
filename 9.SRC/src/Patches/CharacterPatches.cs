using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Unlocks;

namespace CharacterModIsolation.Patches;

internal static class CharacterPatches
{
    public static void CharactersPostfix(UnlockState __instance, ref IEnumerable<CharacterModel> __result)
    {
        try
        {
            if (!FilterPolicy.CardsActive || __result is null || !RunContext.ShouldFilterUnlockState(__instance))
                return;

            var before = __result.ToList();
            var kept = before.Where(character => !FilterPolicy.ShouldRemoveCharacter(character.GetType())).ToList();
            if (kept.Count == before.Count)
                return;

            __result = kept;
            FilterLog.Removed("characters", before.Count - kept.Count, kept.Count);
        }
        catch (Exception ex)
        {
            Plugin.Logger.Error($"[filter] characters failed, left unchanged: {ex}");
        }
    }
}
