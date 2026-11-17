using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using CharacterModIsolation.Patches;

namespace CharacterModIsolation;

internal static class PatchRegistry
{
    private static readonly Harmony Harmony = new(Plugin.ModId + ".patches");
    private static bool _installed;
    private static int _applied;
    private static int _failed;

    public static void InstallAll()
    {
        if (_installed)
            return;
        _installed = true;

        InstallStable();
        InstallDynamic();

        Plugin.Logger.Info($"[patches] installed={_applied} failed={_failed}");
    }

    private static void InstallStable()
    {
        PatchProperty("characters", typeof(UnlockState), "Characters", null, Last(typeof(CharacterPatches), nameof(CharacterPatches.CharactersPostfix)));

        PatchMethod("cards", typeof(CardPoolModel), "GetUnlockedCards", null,
            null, Last(typeof(PoolPatches), nameof(PoolPatches.CardPoolPostfix)));

        PatchMethod("relic-pool-base", typeof(RelicPoolModel), "GetUnlockedRelics",
            [typeof(UnlockState)], null, Last(typeof(PoolPatches), nameof(PoolPatches.RelicPoolPostfix)));
        PatchMethod("potion-pool-base", typeof(PotionPoolModel), "GetUnlockedPotions",
            [typeof(UnlockState)], null, Last(typeof(PoolPatches), nameof(PoolPatches.PotionPoolPostfix)));

        PatchProperty("encounters", typeof(ActModel), "AllEncounters", null, Last(typeof(ActPatches), nameof(ActPatches.EncountersPostfix)));
        PatchProperty("weak-encounters", typeof(ActModel), "AllWeakEncounters", null, Last(typeof(ActPatches), nameof(ActPatches.EncountersPostfix)));
        PatchProperty("regular-encounters", typeof(ActModel), "AllRegularEncounters", null, Last(typeof(ActPatches), nameof(ActPatches.EncountersPostfix)));
        PatchProperty("elite-encounters", typeof(ActModel), "AllEliteEncounters", null, Last(typeof(ActPatches), nameof(ActPatches.EncountersPostfix)));
        PatchProperty("boss-encounters", typeof(ActModel), "AllBossEncounters", null, Last(typeof(ActPatches), nameof(ActPatches.EncountersPostfix)));
        PatchProperty("monsters", typeof(ActModel), "AllMonsters", null, Last(typeof(ActPatches), nameof(ActPatches.MonstersPostfix)));

        PatchProperty("shared-events", typeof(ModelDb), "AllSharedEvents", null, Last(typeof(ActPatches), nameof(ActPatches.EventsPostfix)));
        PatchProperty("shared-ancients", typeof(ModelDb), "AllSharedAncients", null, Last(typeof(ActPatches), nameof(ActPatches.AncientsPostfix)));
        PatchProperty("all-events", typeof(ModelDb), "AllEvents", null, Last(typeof(ActPatches), nameof(ActPatches.EventsPostfix)));
        PatchProperty("all-ancients", typeof(ModelDb), "AllAncients", null, Last(typeof(ActPatches), nameof(ActPatches.AncientsPostfix)));
        PatchProperty("acts-by-index", typeof(ModelDb), "ActsByIndex", null, Last(typeof(ActPatches), nameof(ActPatches.ActsByIndexPostfix)));
        PatchProperty("all-characters-scope", typeof(ModelDb), "AllCharacters", null, Last(typeof(SpecialPatches), nameof(SpecialPatches.AllCharactersPostfix)));
        PatchProperty("orobas-characters", typeof(Orobas), "SeaGlassOptions",
            Last(typeof(SpecialPatches), nameof(SpecialPatches.OrobasPrefix)), null,
            Last(typeof(SpecialPatches), nameof(SpecialPatches.OrobasFinalizer)));

        PatchMethod("generate-rooms-4", typeof(ActModel), "GenerateRooms",
            [typeof(Rng), typeof(UnlockState), typeof(bool), typeof(bool)],
            Last(typeof(ActPatches), nameof(ActPatches.GenerateRoomsPrefix)), Last(typeof(ActPatches), nameof(ActPatches.GenerateRoomsPostfix)));
        PatchMethod("generate-rooms-3", typeof(ActModel), "GenerateRooms",
            [typeof(Rng), typeof(UnlockState), typeof(bool)],
            Last(typeof(ActPatches), nameof(ActPatches.GenerateRoomsPrefix)), Last(typeof(ActPatches), nameof(ActPatches.GenerateRoomsPostfix)));

        PatchMethod("pull-next-event", typeof(ActModel), "PullNextEvent", [typeof(RunState)],
            Last(typeof(ActPatches), nameof(ActPatches.PullNextEventPrefix)));

        PatchMethod("get-random-list", typeof(ActModel), "GetRandomList",
            [typeof(Rng), typeof(UnlockState), typeof(bool)], null, Last(typeof(ActPatches), nameof(ActPatches.GetRandomListPostfix)));

        PatchMethod("begin-run", typeof(StartRunLobby), "BeginRunLocally",
            [typeof(string), typeof(List<ModifierModel>)], Last(typeof(ActPatches), nameof(ActPatches.BeginRunLocallyPrefix)));

        PatchMethod("kaleidoscope", typeof(Kaleidoscope), "IsAllowedAtNeow",
            [typeof(Player)], null, Last(typeof(ActPatches), nameof(ActPatches.KaleidoscopePostfix)));
    }

    private static void InstallDynamic()
    {
        var assemblies = LoadedAssemblies();

        foreach (var type in AllTypes(assemblies))
        {
            if (type.IsAbstract || type.IsGenericTypeDefinition)
                continue;

            if (typeof(RelicPoolModel).IsAssignableFrom(type))
            {
                var method = type.GetMethod("GetUnlockedRelics",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                    null, [typeof(UnlockState)], null);
                if (method is not null && !method.IsAbstract)
                    Patch("relic-pool:" + type.Name, method, null, Last(typeof(PoolPatches), nameof(PoolPatches.RelicPoolPostfix)));
            }

            if (typeof(PotionPoolModel).IsAssignableFrom(type))
            {
                var method = type.GetMethod("GetUnlockedPotions",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                    null, [typeof(UnlockState)], null);
                if (method is not null && !method.IsAbstract)
                    Patch("potion-pool:" + type.Name, method, null, Last(typeof(PoolPatches), nameof(PoolPatches.PotionPoolPostfix)));
            }

            if (!typeof(ActModel).IsAssignableFrom(type))
                continue;

            var allEvents = type.GetProperty("AllEvents",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (allEvents?.GetMethod is { IsAbstract: false } eventGetter)
                Patch("act-events:" + type.Name, eventGetter, null, Last(typeof(ActPatches), nameof(ActPatches.EventsPostfix)));

            var allAncients = type.GetProperty("AllAncients",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (allAncients?.GetMethod is { IsAbstract: false } ancientGetter)
                Patch("act-ancients:" + type.Name, ancientGetter, null, Last(typeof(ActPatches), nameof(ActPatches.AncientsPostfix)));

            var unlockedAncients = type.GetMethod("GetUnlockedAncients",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                null, [typeof(UnlockState)], null);
            if (unlockedAncients is not null && !unlockedAncients.IsAbstract)
                Patch("act-unlocked-ancients:" + type.Name, unlockedAncients, null, Last(typeof(ActPatches), nameof(ActPatches.AncientsPostfix)));
        }
    }

    private static void PatchProperty(
        string id,
        Type owner,
        string property,
        HarmonyMethod? prefix,
        HarmonyMethod? postfix,
        HarmonyMethod? finalizer = null)
    {
        var getter = owner.GetProperty(property,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)?.GetMethod;
        if (getter is null || getter.IsAbstract)
        {
            _failed++;
            Plugin.Logger.Warn($"[patches] target missing: {id}");
            return;
        }

        Patch(id, getter, prefix, postfix, finalizer);
    }

    private static void PatchMethod(
        string id,
        Type owner,
        string name,
        Type[]? parameterTypes,
        HarmonyMethod? prefix,
        HarmonyMethod? postfix = null)
    {
        MethodInfo? method;
        try
        {
            method = parameterTypes is null
                ? AccessTools.Method(owner, name)
                : AccessTools.Method(owner, name, parameterTypes);
        }
        catch
        {
            method = null;
        }

        if (method is null)
        {
            // The 3-argument GenerateRooms exists on older hosts only. Missing optional
            // targets are not an installation failure.
            if (id is "generate-rooms-3" or "generate-rooms-4")
            {
                Plugin.Logger.Info($"[patches] optional target absent: {id}");
                return;
            }

            _failed++;
            Plugin.Logger.Warn($"[patches] target missing: {id}");
            return;
        }

        Patch(id, method, prefix, postfix);
    }

    private static void Patch(string id, MethodBase method, HarmonyMethod? prefix, HarmonyMethod? postfix,
        HarmonyMethod? finalizer = null)
    {
        try
        {
            Harmony.Patch(method, prefix, postfix, finalizer: finalizer);
            _applied++;
            Plugin.Logger.Info($"[patches] {id} -> {method.DeclaringType?.Name}.{method.Name}");
        }
        catch (Exception ex)
        {
            _failed++;
            Plugin.Logger.Error($"[patches] {id} failed on {method.DeclaringType?.Name}.{method.Name}: {ex.Message}");
        }
    }

    private static HarmonyMethod Last(Type type, string method)
    {
        var info = type.GetMethod(method, BindingFlags.Public | BindingFlags.Static)
                   ?? throw new MissingMethodException(type.FullName, method);
        return new HarmonyMethod(info) { priority = Priority.Last };
    }

    private static List<Assembly> LoadedAssemblies()
    {
        var list = new List<Assembly> { typeof(ModelDb).Assembly };
        try
        {
            foreach (var mod in MegaCrit.Sts2.Core.Modding.ModManager.GetLoadedMods())
            {
                var assembliesField = typeof(MegaCrit.Sts2.Core.Modding.Mod)
                    .GetField("assemblies", BindingFlags.Public | BindingFlags.Instance);
                if (assembliesField?.GetValue(mod) is IEnumerable<Assembly> assemblies)
                    list.AddRange(assemblies);

                var assemblyField = typeof(MegaCrit.Sts2.Core.Modding.Mod)
                    .GetField("assembly", BindingFlags.Public | BindingFlags.Instance);
                if (assemblyField?.GetValue(mod) is Assembly assembly)
                    list.Add(assembly);
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.Warn($"[patches] loaded assembly enumeration failed: {ex.Message}");
        }

        try
        {
            list.AddRange(AppDomain.CurrentDomain.GetAssemblies().Where(static assembly => !assembly.IsDynamic));
        }
        catch
        {
            // Assembly enumeration is best-effort.
        }

        return list.Distinct().ToList();
    }

    private static IEnumerable<Type> AllTypes(IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(static type => type is not null).Cast<Type>().ToArray();
            }
            catch
            {
                continue;
            }

            foreach (var type in types)
                yield return type;
        }
    }
}
