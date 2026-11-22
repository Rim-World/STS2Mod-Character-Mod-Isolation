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
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;

namespace P0Probe;

/// <summary>
/// Test-only driver for the SourceCache cross-run gate.
/// Run A: vanilla Regent with automatic isolation, which builds the filtered cache for MGR and Watcher pools.
/// Run B: vanilla Regent with manual isolation selecting only Watcher. MGR should now be kept, but the old
/// cache entry still contains the Run A filtered array. The probe logs the pool result and the cache refs.
/// </summary>
[HarmonyPatch(typeof(NMainMenu), "_Ready")]
internal static class CacheMenuReadyPatch
{
    private static bool _started;

    private static void Postfix()
    {
        if (_started || Environment.GetEnvironmentVariable("CMI_PROBE_CACHE") != "1")
            return;

        _started = true;
        Log.Info("[P0Probe] cache-driver schedule");
        TaskHelper.RunSafely(CacheDriver.RunAsync());
    }
}

internal static class CacheDriver
{
    private const string Prefix = "[P0Probe]";
    private const string MgRPoolId = "MGR_MOD_CARD_POOL_MGR_CARD_POOL";
    private const string WatcherPoolId = "WATCHER_CARD_POOL";

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
            Log.Info($"{Prefix} cache-driver start");

            CaptureSettings();
            await StartVanillaRun();
            await Frames(30);
            Dump("run-a");

            Log.Info($"{Prefix} cache-driver return");
            await NGame.Instance.ReturnToMainMenu();
            await Frames(30);
            Log.Info($"{Prefix} cache-driver at-main-menu");

            SetManualWatcher();
            await StartVanillaRun();
            await Frames(30);
            Dump("run-b");

            Log.Info($"{Prefix} cache-driver complete");
        }
        catch (Exception ex)
        {
            Log.Error($"{Prefix} cache-driver failed: {ex}");
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
            Log.Info($"{Prefix} cache-driver patches ensured");
        }
        catch (Exception ex)
        {
            Log.Warn($"{Prefix} cache-driver patch install failed: {ex.Message}");
        }
    }

    private static async Task StartVanillaRun()
    {
        var character = ModelDb.Character<Regent>();
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
            ];
        }

        Log.Info($"{Prefix} cache-driver start-run character={character.Id.Entry} parameters={parameters.Length}");
        if (method.Invoke(NGame.Instance, args) is not Task task)
            throw new InvalidOperationException("StartNewSingleplayerRun did not return a Task");
        await task;
        await Frames(5);
    }

    private static void Dump(string phase)
    {
        var active = Value("CharacterModIsolation.RunContext", "IsActive");
        var reason = Value("CharacterModIsolation.RunContext", "Reason");
        var source = Value("CharacterModIsolation.RunContext", "Source");
        var effective = Reflection.Property("CharacterModIsolation.RunContext", "EffectiveModIds");
        var ids = effective is IEnumerable<string> set
            ? string.Join(",", set.OrderBy(static id => id, StringComparer.Ordinal))
            : "null";
        Log.Info($"{Prefix} cache phase={phase} active={active} reason={reason} source={source} effective=[{ids}]");

        DumpPool(phase, MgRPoolId);
        DumpPool(phase, WatcherPoolId);
        LogCache();
    }

    private static void DumpPool(string phase, string poolId)
    {
        try
        {
            var pool = ModelDb.AllCardPools.FirstOrDefault(candidate =>
                string.Equals(candidate.Id.Entry, poolId, StringComparison.Ordinal));
            if (pool is null)
            {
                Log.Warn($"{Prefix} cache-pool phase={phase} id={poolId} missing");
                return;
            }

            var state = RunManager.Instance?.DebugOnlyGetState();
            var unlock = state?.Players.FirstOrDefault()?.UnlockState;
            if (unlock is null)
            {
                Log.Warn($"{Prefix} cache-pool phase={phase} id={poolId} unlock-missing");
                return;
            }

            var cards = pool.GetUnlockedCards(unlock, CardMultiplayerConstraint.None).ToList();
            var owners = cards
                .GroupBy(card => Reflection.Owner(card.GetType()) ?? "vanilla", StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Key}:{group.Count()}");
            Log.Info($"{Prefix} cache-pool phase={phase} id={poolId} total={cards.Count} owners=[{string.Join(",", owners)}]");
        }
        catch (Exception ex)
        {
            Log.Warn($"{Prefix} cache-pool phase={phase} id={poolId} failed: {ex.Message}");
        }
    }

    private static void LogCache()
    {
        try
        {
            var type = Reflection.IsolationAssembly?.GetType("CharacterModIsolation.Patches.PoolPatches");
            var field = type?.GetField("SourceCaches", BindingFlags.NonPublic | BindingFlags.Static);
            var table = field?.GetValue(null);
            if (table is not IEnumerable entries)
            {
                Log.Warn($"{Prefix} cache-table unavailable");
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
                Log.Info($"{Prefix} cache-entry pool={key?.Id.Entry ?? "null"} " +
                         $"source={source?.Length ?? -1} sourceRef={(source is null ? 0 : RuntimeHelpers.GetHashCode(source))} " +
                         $"filtered={filtered?.Length ?? -1} filteredRef={(filtered is null ? 0 : RuntimeHelpers.GetHashCode(filtered))}");
            }

            Log.Info($"{Prefix} cache entries={count}");
        }
        catch (Exception ex)
        {
            Log.Warn($"{Prefix} cache read failed: {ex.Message}");
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
            Log.Info($"{Prefix} cache settings captured source={_originalSource} manual={Describe(_originalManual)}");
        }
        catch (Exception ex)
        {
            Log.Warn($"{Prefix} cache settings capture failed: {ex.Message}");
        }
    }

    private static void SetManualWatcher()
    {
        try
        {
            var sourceBinding = SettingsBinding("SourceBinding");
            var manualBinding = SettingsBinding("ManualIdsBinding");
            var sourceType = Reflection.IsolationAssembly?.GetType("CharacterModIsolation.IsolationSource");
            if (sourceBinding is null || manualBinding is null || sourceType is null)
            {
                Log.Error($"{Prefix} cache settings bindings unavailable");
                return;
            }

            var manual = Enum.ToObject(sourceType, 1);
            Write(sourceBinding, manual);
            Write(manualBinding, new List<string> { "Watcher" });
            Save(sourceBinding);
            Save(manualBinding);
            Log.Info($"{Prefix} cache settings set source=Manual manual=[Watcher]");
        }
        catch (Exception ex)
        {
            Log.Error($"{Prefix} cache settings set failed: {ex}");
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

            Log.Info($"{Prefix} cache settings restored source={_originalSource} manual={Describe(_originalManual)}");
        }
        catch (Exception ex)
        {
            Log.Error($"{Prefix} cache settings restore failed: {ex}");
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
