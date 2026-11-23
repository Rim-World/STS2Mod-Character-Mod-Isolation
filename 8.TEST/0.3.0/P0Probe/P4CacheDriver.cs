using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;

namespace P0Probe;

/// <summary>
/// Test-only three-run driver for the P4 cache gate.
/// Run A: automatic isolation, vanilla Regent.
/// Run B: manual {Watcher}, vanilla Regent.
/// Run C: automatic isolation, MGR character.
/// The MGR pool must be 0 in A, 91 in B, and 91 in C without cross-run reuse.
/// </summary>
[HarmonyPatch(typeof(NMainMenu), "_Ready")]
internal static class P4CacheMenuReadyPatch
{
    private static bool _started;

    private static void Postfix()
    {
        if (_started || Environment.GetEnvironmentVariable("CMI_PROBE_CACHE3") != "1")
            return;

        _started = true;
        Log.Info("[P0Probe] p4-cache-driver schedule");
        TaskHelper.RunSafely(P4CacheDriver.RunAsync());
    }
}

internal static class P4CacheDriver
{
    private const string Prefix = "[P0Probe]";
    private const string MgRPoolId = "MGR_MOD_CARD_POOL_MGR_CARD_POOL";
    private const string WatcherPoolId = "WATCHER_CARD_POOL";
    private const string MgRCharacterId = "MGR_MOD_CHARACTER_MGR_CHARACTER";

    private static object? _originalSource;
    private static object? _originalManual;
    private static bool _settingsCaptured;
    private static bool _settingsRestored;

    public static async Task RunAsync()
    {
        try
        {
            await Frames(30);
            EnsurePatches();
            Log.Info($"{Prefix} p4-cache-driver start");
            CaptureSettings();

            if (Environment.GetEnvironmentVariable("CMI_PROBE_MANUAL_MGR") == "1")
            {
                SetManualWatcher();
                await StartRun(MgRCharacterId);
                await Frames(30);
                Dump("manual-watcher-mgr");
                return;
            }

            SetAuto();
            await StartRun("REGENT");
            await Frames(30);
            Dump("run-a-vanilla-auto");
            await ReturnToMainMenu();

            SetManualWatcher();
            await StartRun("REGENT");
            await Frames(30);
            Dump("run-b-vanilla-manual-watcher");
            await ReturnToMainMenu();

            SetAuto();
            await StartRun(MgRCharacterId);
            await Frames(30);
            Dump("run-c-mgr-auto");

            Log.Info($"{Prefix} p4-cache-driver complete");
        }
        catch (Exception ex)
        {
            Log.Error($"{Prefix} p4-cache-driver failed: {ex}");
        }
        finally
        {
            RestoreSettings();
            await Frames(10);
            NGame.Instance?.GetTree().Quit(0);
        }
    }

    private static void EnsurePatches()
    {
        try
        {
            var type = Reflection.IsolationAssembly?.GetType("CharacterModIsolation.PatchRegistry");
            type?.GetMethod("InstallAll", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                ?.Invoke(null, null);
            Log.Info($"{Prefix} p4-cache-driver patches ensured");
        }
        catch (Exception ex)
        {
            Log.Warn($"{Prefix} p4-cache-driver patch install failed: {ex.Message}");
        }
    }

    private static async Task StartRun(string characterId)
    {
        var character = ModelDb.AllCharacters.FirstOrDefault(candidate =>
            string.Equals(candidate.Id.Entry, characterId, StringComparison.Ordinal));
        if (character is null)
            throw new InvalidOperationException("character not found: " + characterId);

        var method = typeof(NGame)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(candidate =>
                candidate.Name == "StartNewSingleplayerRun" &&
                candidate.GetParameters().Length >= 8);
        if (method is null)
            throw new MissingMethodException(nameof(NGame), "StartNewSingleplayerRun");

        var parameters = method.GetParameters();
        object?[] args;
        if (parameters[0].ParameterType.Name == "Player")
        {
            var unlock = new UnlockState(SaveManager.Instance.Progress);
            var player = Player.CreateForNewRun(character, unlock, 1UL);
            args =
            [
                player,
                false,
                ActModel.GetDefaultList(),
                Array.Empty<ModifierModel>(),
                "1",
                GameMode.Standard,
                false,
                0,
                null,
            ];
        }
        else
        {
            args =
            [
                character,
                false,
                ActModel.GetDefaultList(),
                Array.Empty<ModifierModel>(),
                "1",
                GameMode.Standard,
                0,
                null,
            ];
        }

        Log.Info($"{Prefix} p4-cache start-run character={character.Id.Entry} parameters={parameters.Length}");
        if (method.Invoke(NGame.Instance, args) is not Task task)
            throw new InvalidOperationException("StartNewSingleplayerRun did not return a Task");
        await task;
        await Frames(5);
    }

    private static async Task ReturnToMainMenu()
    {
        Log.Info($"{Prefix} p4-cache return-to-main-menu");
        await NGame.Instance.ReturnToMainMenu();
        await Frames(30);
    }

    private static void Dump(string phase)
    {
        var active = Value("CharacterModIsolation.RunContext", "IsActive");
        var reason = Value("CharacterModIsolation.RunContext", "Reason");
        var source = Value("CharacterModIsolation.RunContext", "Source");
        var owner = Value("CharacterModIsolation.RunContext", "CurrentCharacterOwner");
        var effective = Reflection.Property("CharacterModIsolation.RunContext", "EffectiveModIds");
        var ids = effective is IEnumerable<string> set
            ? string.Join(",", set.OrderBy(static id => id, StringComparer.Ordinal))
            : "null";
        Log.Info($"{Prefix} p4-cache phase={phase} active={active} reason={reason} " +
                 $"source={source} owner={owner} isolated=[{ids}]");

        DumpPool(phase, MgRPoolId);
        DumpPool(phase, WatcherPoolId);
        LogCache(phase);
    }

    private static void DumpPool(string phase, string poolId)
    {
        try
        {
            var pool = ModelDb.AllCardPools.FirstOrDefault(candidate =>
                string.Equals(candidate.Id.Entry, poolId, StringComparison.Ordinal));
            if (pool is null)
            {
                Log.Warn($"{Prefix} p4-cache-pool phase={phase} id={poolId} missing");
                return;
            }

            var state = RunManager.Instance?.DebugOnlyGetState();
            var unlock = state?.Players.FirstOrDefault()?.UnlockState;
            if (unlock is null)
            {
                Log.Warn($"{Prefix} p4-cache-pool phase={phase} id={poolId} unlock-missing");
                return;
            }

            var cards = pool.GetUnlockedCards(unlock, CardMultiplayerConstraint.None).ToList();
            var owners = cards
                .GroupBy(card => Reflection.Owner(card.GetType()) ?? "vanilla", StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Key}:{group.Count()}");
            Log.Info($"{Prefix} p4-cache-pool phase={phase} id={poolId} total={cards.Count} " +
                     $"owners=[{string.Join(",", owners)}]");
        }
        catch (Exception ex)
        {
            Log.Warn($"{Prefix} p4-cache-pool phase={phase} id={poolId} failed: {ex.Message}");
        }
    }

    private static void LogCache(string phase)
    {
        try
        {
            var type = Reflection.IsolationAssembly?.GetType("CharacterModIsolation.Patches.PoolPatches");
            var field = type?.GetField("SourceCaches", BindingFlags.NonPublic | BindingFlags.Static);
            var table = field?.GetValue(null);
            if (table is not IEnumerable entries)
            {
                Log.Warn($"{Prefix} p4-cache-table unavailable");
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
                Log.Info($"{Prefix} p4-cache-entry phase={phase} pool={key?.Id.Entry ?? "null"} " +
                         $"source={source?.Length ?? -1} sourceRef={(source is null ? 0 : RuntimeHelpers.GetHashCode(source))} " +
                         $"filtered={filtered?.Length ?? -1} filteredRef={(filtered is null ? 0 : RuntimeHelpers.GetHashCode(filtered))}");
            }

            Log.Info($"{Prefix} p4-cache entries phase={phase} count={count}");
        }
        catch (Exception ex)
        {
            Log.Warn($"{Prefix} p4-cache read failed: {ex.Message}");
        }
    }

    private static void CaptureSettings()
    {
        try
        {
            var sourceBinding = SettingsBinding("SourceBinding");
            var manualBinding = SettingsBinding("ManualIdsBinding");
            _originalSource = sourceBinding?.GetType().GetMethod("Read")?.Invoke(sourceBinding, null);
            _originalManual = manualBinding?.GetType().GetMethod("Read")?.Invoke(manualBinding, null);
            _settingsCaptured = true;
            Log.Info($"{Prefix} p4-cache settings captured source={_originalSource} " +
                     $"manual={Describe(_originalManual)}");
        }
        catch (Exception ex)
        {
            Log.Warn($"{Prefix} p4-cache settings capture failed: {ex.Message}");
        }
    }

    private static void SetAuto()
    {
        SetSource(0, null, "auto");
    }

    private static void SetManualWatcher()
    {
        SetSource(1, new List<string> { "Watcher" }, "manual-watcher");
    }

    private static void SetSource(int sourceValue, List<string>? manual, string label)
    {
        try
        {
            var sourceBinding = SettingsBinding("SourceBinding");
            var manualBinding = SettingsBinding("ManualIdsBinding");
            var sourceType = Reflection.IsolationAssembly?.GetType("CharacterModIsolation.IsolationSource");
            if (sourceBinding is null || manualBinding is null || sourceType is null)
            {
                Log.Error($"{Prefix} p4-cache settings bindings unavailable");
                return;
            }

            Write(sourceBinding, Enum.ToObject(sourceType, sourceValue));
            Save(sourceBinding);
            if (manual is not null)
            {
                Write(manualBinding, manual);
                Save(manualBinding);
            }

            Log.Info($"{Prefix} p4-cache settings set {label} source={sourceValue} " +
                     $"manual={Describe(manual)}");
        }
        catch (Exception ex)
        {
            Log.Error($"{Prefix} p4-cache settings set failed: {ex}");
        }
    }

    private static void RestoreSettings()
    {
        if (_settingsRestored || !_settingsCaptured)
            return;

        _settingsRestored = true;
        try
        {
            var sourceBinding = SettingsBinding("SourceBinding");
            var manualBinding = SettingsBinding("ManualIdsBinding");
            if (sourceBinding is not null && _originalSource is not null)
            {
                Write(sourceBinding, _originalSource);
                Save(sourceBinding);
            }

            if (manualBinding is not null && _originalManual is not null)
            {
                Write(manualBinding, _originalManual);
                Save(manualBinding);
            }

            Log.Info($"{Prefix} p4-cache settings restored source={_originalSource} " +
                     $"manual={Describe(_originalManual)}");
        }
        catch (Exception ex)
        {
            Log.Error($"{Prefix} p4-cache settings restore failed: {ex}");
        }
    }

    private static object? SettingsBinding(string property)
    {
        var type = Reflection.IsolationAssembly?.GetType("CharacterModIsolation.ModSettings");
        return type?.GetProperty(property, BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
    }

    private static void Write(object binding, object value)
    {
        binding.GetType().GetMethod("Write")?.Invoke(binding, [value]);
    }

    private static void Save(object binding)
    {
        binding.GetType().GetMethod("Save")?.Invoke(binding, null);
    }

    private static string Describe(object? value)
    {
        if (value is IEnumerable<string> items)
            return "[" + string.Join(",", items) + "]";
        return value?.ToString() ?? "null";
    }

    private static string Value(string type, string property) =>
        Reflection.Property(type, property)?.ToString() ?? "null";

    private static async Task Frames(int count)
    {
        for (var i = 0; i < count; i++)
            await NGame.Instance.AwaitProcessFrame();
    }
}
