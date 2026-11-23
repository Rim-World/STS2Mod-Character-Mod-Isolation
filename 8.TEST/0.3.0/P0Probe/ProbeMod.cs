using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.AutoSlay.Helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

namespace P0Probe;

[ModInitializer(nameof(Initialize))]
public static class Probe
{
    internal const string Prefix = "[P0Probe]";

    internal static string Target =>
        System.Environment.GetEnvironmentVariable("CMI_PROBE_TARGET")?.Trim() is { Length: > 0 } value
            ? value
            : "REGENT";

    public static void Initialize()
    {
        Log.Info($"{Prefix} loaded target={Target}");
        try
        {
            new Harmony("P0Probe").PatchAll(typeof(Probe).Assembly);
            Log.Info($"{Prefix} harmony installed");
        }
        catch (Exception ex)
        {
            Log.Error($"{Prefix} harmony install failed: {ex}");
        }

        if (System.Environment.GetEnvironmentVariable("CMI_PROBE_INSTALL") == "1")
        {
            try
            {
                var type = Reflection.IsolationAssembly?.GetType("CharacterModIsolation.PatchRegistry");
                type?.GetMethod("InstallAll", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    ?.Invoke(null, null);
                Log.Info($"{Prefix} forced CharacterModIsolation patch installation");
            }
            catch (Exception ex)
            {
                Log.Error($"{Prefix} forced patch installation failed: {ex}");
            }
        }
    }
}

internal static class Reflection
{
    internal static readonly Assembly? IsolationAssembly = AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(assembly => assembly.GetName().Name == "CharacterModIsolation");

    internal static readonly MethodInfo? OwnerMethod = IsolationAssembly?
        .GetType("CharacterModIsolation.ContentOwnership")?
        .GetMethod("GetOwnerModId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

    internal static string? Owner(Type type)
    {
        try
        {
            return OwnerMethod?.Invoke(null, [type]) as string;
        }
        catch
        {
            return null;
        }
    }

    internal static object? InvokeStatic(string typeName, string methodName)
    {
        try
        {
            var type = IsolationAssembly?.GetType(typeName);
            var method = type?.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            return method?.Invoke(null, null);
        }
        catch
        {
            return null;
        }
    }

    internal static object? Property(string typeName, string propertyName)
    {
        try
        {
            var type = IsolationAssembly?.GetType(typeName);
            return type?.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
        }
        catch
        {
            return null;
        }
    }
}

[HarmonyPatch(typeof(NGame), nameof(NGame.IsReleaseGame))]
internal static class ReleaseGatePatch
{
    [HarmonyPostfix]
    private static void Postfix(ref bool __result)
    {
        __result = false;
    }
}

internal static class CharacterPick
{
    internal static bool IsTarget(NCharacterSelectButton button)
    {
        try
        {
            var character = button.Character;
            if (character is null)
                return false;
            var id = character.Id.Entry;
            var type = character.GetType().Name;
            return string.Equals(id, Probe.Target, StringComparison.Ordinal) ||
                   string.Equals(type, Probe.Target, StringComparison.Ordinal) ||
                   string.Equals("CHARACTER." + id, Probe.Target, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    internal static void SelectTarget(Node root, string phase)
    {
        try
        {
            var buttons = UiHelper.FindAll<NCharacterSelectButton>(root).ToList();
            var target = buttons.FirstOrDefault(IsTarget);
            if (target is null)
            {
                var ids = string.Join(",", buttons.Select(button =>
                    button.Character?.Id.Entry ?? "null"));
                Log.Warn($"{Probe.Prefix} {phase} target={Probe.Target} missing buttons=[{ids}]");
                return;
            }

            foreach (var button in buttons)
            {
                try { button.UnlockIfPossible(); }
                catch (Exception ex) { Log.Warn($"{Probe.Prefix} unlock failed: {ex.Message}"); }
            }

            if (target.IsSelected)
            {
                Log.Info($"{Probe.Prefix} {phase} target={Probe.Target} already selected");
                return;
            }

            var previous = buttons.FirstOrDefault(button => button.IsSelected)?.Character?.Id.Entry ?? "none";
            Log.Info($"{Probe.Prefix} {phase} target={Probe.Target} was={previous} redirect");
            target.Select();
        }
        catch (Exception ex)
        {
            Log.Error($"{Probe.Prefix} {phase} selection failed: {ex}");
        }
    }
}

[HarmonyPatch(typeof(NCharacterSelectButton), nameof(NCharacterSelectButton.Select))]
internal static class ForceCharacterSelectPatch
{
    private static bool Prefix(NCharacterSelectButton __instance)
    {
        if (CharacterPick.IsTarget(__instance))
            return true;
        CharacterPick.SelectTarget(__instance.GetParent(), "select");
        return false;
    }
}

[HarmonyPatch(typeof(NCharacterSelectScreen), "OnSubmenuOpened")]
internal static class ForceCharacterOpenedPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCharacterSelectScreen __instance)
    {
        CharacterPick.SelectTarget(__instance, "opened");
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewSingleplayer))]
internal static class RunStartPatch
{
    [HarmonyPostfix]
    private static void Postfix(RunState state)
    {
        try
        {
            Audit.Run(state);
        }
        catch (Exception ex)
        {
            Log.Error($"{Probe.Prefix} audit failed: {ex}");
        }
    }
}

internal static class Audit
{
    public static void Run(RunState state)
    {
        var player = state.Players.FirstOrDefault();
        if (player is null)
        {
            Log.Error($"{Probe.Prefix} run-start no player");
            return;
        }

        Log.Info($"{Probe.Prefix} run-start target={Probe.Target} " +
                 $"character={player.Character.Id.Entry} type={player.Character.GetType().FullName} " +
                 $"asm={player.Character.GetType().Assembly.GetName().Name} " +
                 $"owner={Reflection.Owner(player.Character.GetType()) ?? "vanilla"}");

        LogContext();
        LogAllCharacters();
        LogRunCollections(state, player);
        LogPools(player);
        LogStartingContents(player);
        LogDirectPaths(state, player);
        LogSourceCache();
        P4Audit.Run(state);
    }

    private static void LogContext()
    {
        Log.Info($"{Probe.Prefix} context active={Value("CharacterModIsolation.RunContext", "IsActive")} " +
                 $"reason={Value("CharacterModIsolation.RunContext", "Reason")} " +
                 $"source={Value("CharacterModIsolation.RunContext", "Source")} " +
                 $"owner={Value("CharacterModIsolation.RunContext", "CurrentCharacterOwner") ?? "vanilla"} " +
                 $"protected=[{string.Join(",", Set("ProtectedModIds"))}] " +
                 $"isolated=[{string.Join(",", Set("IsolatedModIds"))}] " +
                 $"effective=[{string.Join(",", Effective())}] " +
                 $"version={Value("CharacterModIsolation.RunContext", "PolicyVersion")}");
    }

    private static IEnumerable<string> Effective()
    {
        var value = Reflection.Property("CharacterModIsolation.RunContext", "EffectiveModIds");
        return value is IEnumerable<string> ids ? ids.OrderBy(id => id, StringComparer.Ordinal) : [];
    }

    private static IEnumerable<string> Set(string property)
    {
        var value = Reflection.Property("CharacterModIsolation.RunContext", property);
        return value is IEnumerable<string> ids ? ids.OrderBy(id => id, StringComparer.Ordinal) : [];
    }

    private static string Value(string type, string property) =>
        Reflection.Property(type, property)?.ToString() ?? "null";

    private static void LogAllCharacters()
    {
        try
        {
            foreach (var character in ModelDb.AllCharacters)
            {
                var type = character.GetType();
                Log.Info($"{Probe.Prefix} all-character id={character.Id.Entry} type={type.FullName} " +
                         $"asm={type.Assembly.GetName().Name} owner={Reflection.Owner(type) ?? "vanilla"}");
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"{Probe.Prefix} all-character failed: {ex.Message}");
        }
    }

    private static void LogRunCollections(RunState state, Player player)
    {
        LogModels("unlock-characters", player.UnlockState.Characters);
        LogModels("unlock-character-card-pools", player.UnlockState.CharacterCardPools);
        LogModels("unlock-card-pools", player.UnlockState.CardPools);
        LogModels("unlock-relics", player.UnlockState.Relics);
        LogModels("unlock-potions", player.UnlockState.Potions);
        LogModels("shared-ancients", player.UnlockState.SharedAncients);
        LogModels("shared-events", ModelDb.AllSharedEvents);
        LogModels("acts", state.Acts);
        foreach (var act in state.Acts)
        {
            LogModels($"act{act.Index}.encounters", act.AllEncounters);
            LogModels($"act{act.Index}.monsters", act.AllMonsters);
            LogModels($"act{act.Index}.events", act.AllEvents);
            LogModels($"act{act.Index}.ancients", act.AllAncients);
        }
    }

    private static void LogModels(string category, IEnumerable models)
    {
        try
        {
            var list = models.Cast<object>().ToList();
            var groups = list
                .GroupBy(model => Reflection.Owner(model.GetType()) ?? "vanilla", StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Key}:{group.Count()}");
            Log.Info($"{Probe.Prefix} coverage category={category} total={list.Count} owners=[{string.Join(",", groups)}]");
        }
        catch (Exception ex)
        {
            Log.Warn($"{Probe.Prefix} coverage {category} failed: {ex.Message}");
        }
    }

    private static void LogPools(Player player)
    {
        var unlock = player.UnlockState;
        foreach (var pool in unlock.CardPools)
        {
            try
            {
                var cards = pool.GetUnlockedCards(unlock, CardMultiplayerConstraint.None).ToList();
                var groups = cards
                    .GroupBy(card => Reflection.Owner(card.GetType()) ?? "vanilla", StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => $"{group.Key}:{group.Count()}");
                Log.Info($"{Probe.Prefix} cardpool id={pool.Id.Entry} type={pool.GetType().Name} " +
                         $"owner={Reflection.Owner(pool.GetType()) ?? "vanilla"} total={cards.Count} " +
                         $"owners=[{string.Join(",", groups)}]");
            }
            catch (Exception ex)
            {
                Log.Warn($"{Probe.Prefix} cardpool {pool.Id.Entry} failed: {ex.Message}");
            }
        }
    }

    private static void LogStartingContents(Player player)
    {
        LogCards("starting-deck", player.Deck.Cards);
        LogModels("starting-relics", player.Relics);
        LogModels("starting-potions", player.Potions);
    }

    private static void LogCards(string category, IEnumerable<CardModel> cards)
    {
        try
        {
            var list = cards.ToList();
            var groups = list
                .GroupBy(card => Reflection.Owner(card.GetType()) ?? "vanilla", StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Key}:{group.Count()}");
            var ids = string.Join(",", list.Select(card => card.Id.Entry).Distinct().OrderBy(id => id, StringComparer.Ordinal));
            Log.Info($"{Probe.Prefix} {category} total={list.Count} owners=[{string.Join(",", groups)}] ids=[{ids}]");
        }
        catch (Exception ex)
        {
            Log.Warn($"{Probe.Prefix} {category} failed: {ex.Message}");
        }
    }

    private static void LogDirectPaths(RunState state, Player player)
    {
        LogModels("direct-modeldb-all-characters", ModelDb.AllCharacters);
        LogModels("direct-modeldb-all-cards", ModelDb.AllCards);
        LogModels("direct-modeldb-all-relics", ModelDb.AllRelics);
        LogModels("direct-modeldb-all-potions", ModelDb.AllPotions);
        LogModels("direct-modeldb-all-events", ModelDb.AllEvents);
        LogModels("direct-unlock-character-card-pools", player.UnlockState.CharacterCardPools);
        LogModels("direct-run-characters", player.UnlockState.Characters);
        LogModels("direct-run-acts", state.Acts);
    }

    private static void LogSourceCache()
    {
        try
        {
            var type = Reflection.IsolationAssembly?
                .GetType("CharacterModIsolation.Patches.PoolPatches");
            var field = type?.GetField("SourceCaches", BindingFlags.NonPublic | BindingFlags.Static);
            var table = field?.GetValue(null);
            if (table is not IEnumerable entries)
            {
                Log.Warn($"{Probe.Prefix} source-cache table unavailable");
                return;
            }

            var count = 0;
            foreach (var entry in entries)
            {
                count++;
                var entryType = entry.GetType();
                var key = entryType.GetProperty("Key")?.GetValue(entry) as CardPoolModel;
                var value = entryType.GetProperty("Value")?.GetValue(entry);
                if (value is null)
                    continue;
                var valueType = value.GetType();
                var source = valueType.GetField("Source")?.GetValue(value) as Array;
                var filtered = valueType.GetField("Filtered")?.GetValue(value) as Array;
                Log.Info($"{Probe.Prefix} source-cache pool={key?.Id.Entry ?? "null"} " +
                         $"source={source?.Length ?? -1} sourceRef={(source is null ? 0 : RuntimeHelpers.GetHashCode(source))} " +
                         $"filtered={filtered?.Length ?? -1} filteredRef={(filtered is null ? 0 : RuntimeHelpers.GetHashCode(filtered))}");
            }
            Log.Info($"{Probe.Prefix} source-cache entries={count}");
        }
        catch (Exception ex)
        {
            Log.Warn($"{Probe.Prefix} source-cache failed: {ex.Message}");
        }
    }
}
