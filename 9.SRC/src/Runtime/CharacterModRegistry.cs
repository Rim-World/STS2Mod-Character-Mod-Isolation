using System.Reflection;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;

namespace CharacterModIsolation;

internal enum CharacterModKind
{
    Character,
    LikelyCharacter,
    NotCharacter,
    Unknown,
}

internal readonly record struct AutoCharacterResult(CharacterModKind Kind, string Reason);

/// <summary>
/// Auto detection uses only role evidence: registered characters and concrete
/// <see cref="CharacterModel"/> subclasses. Manual mode never builds this map.
/// </summary>
internal static class CharacterModRegistry
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, AutoCharacterResult> Auto = new(StringComparer.Ordinal);
    private static bool _built;

    public static string? GetIsolatedOwner(Type type)
    {
        var owner = ContentOwnership.GetOwnerModId(type);
        return owner is not null && IsCharacterMod(owner) ? owner : null;
    }

    public static bool IsCharacterMod(string modId)
    {
        var settings = ModSettings.Current;
        if (settings.Source == IsolationSource.Manual)
            return settings.ManualModIds.Contains(modId);

        EnsureAuto();
        return Auto.TryGetValue(modId, out var result) &&
               result.Kind is CharacterModKind.Character or CharacterModKind.LikelyCharacter;
    }

    public static bool IsIsolatedMod(string modId) => IsCharacterMod(modId);

    public static bool HasAnyCharacterMod()
    {
        var settings = ModSettings.Current;
        if (settings.Source == IsolationSource.Manual)
            return settings.ManualModIds.Count > 0;

        EnsureAuto();
        return Auto.Values.Any(static result =>
            result.Kind is CharacterModKind.Character or CharacterModKind.LikelyCharacter);
    }

    public static IEnumerable<string> AutoCharacterModIds()
    {
        EnsureAuto();
        return Auto.Where(static pair =>
                pair.Value.Kind is CharacterModKind.Character or CharacterModKind.LikelyCharacter)
            .Select(static pair => pair.Key);
    }

    public static AutoCharacterResult GetAuto(string modId)
    {
        if (ModSettings.Current.Source == IsolationSource.Manual)
            return new AutoCharacterResult(CharacterModKind.Unknown, "manual");

        EnsureAuto();
        return Auto.TryGetValue(modId, out var result)
            ? result
            : new AutoCharacterResult(CharacterModKind.Unknown, "not-loaded");
    }

    public static string DescribeAuto(string modId)
    {
        if (ModSettings.Current.Source == IsolationSource.Manual)
            return Localization.Resolve("cmi.auto.manual", "Manual mode: automatic detection is not running.");

        var result = GetAuto(modId);
        var kind = result.Kind switch
        {
            CharacterModKind.Character => Localization.Resolve("cmi.auto.character", "Character mod"),
            CharacterModKind.LikelyCharacter => Localization.Resolve("cmi.auto.likely", "Likely character mod"),
            CharacterModKind.NotCharacter => Localization.Resolve("cmi.auto.not", "Not a character mod"),
            _ => Localization.Resolve("cmi.auto.unknown", "Unknown"),
        };

        return kind + " · " + result.Reason;
    }

    public static void RescanAuto()
    {
        if (ModSettings.Current.Source == IsolationSource.Manual)
        {
            Plugin.Logger.Info("[auto] skipped: manual mode");
            return;
        }

        lock (Gate)
        {
            Auto.Clear();
            _built = false;
        }

        EnsureAuto(forceLog: true);
    }

    public static void PrewarmAuto()
    {
        if (ModSettings.Current.Source == IsolationSource.Auto)
            EnsureAuto();
    }

    public static void EnsureAuto(bool forceLog = false)
    {
        if (_built)
            return;

        lock (Gate)
        {
            if (_built)
                return;

            Auto.Clear();
            var mods = LoadedMods();
            foreach (var mod in mods)
                Auto[mod.Id] = new AutoCharacterResult(CharacterModKind.NotCharacter, Localization.Resolve("cmi.auto.noEvidence", "no role evidence"));

            try
            {
                foreach (var character in ModelDb.AllCharacters)
                {
                    var type = character.GetType();
                    var owner = ContentOwnership.GetOwnerModId(type);
                    if (string.IsNullOrWhiteSpace(owner))
                        continue;

                    Auto[owner] = new AutoCharacterResult(
                        CharacterModKind.Character,
                        Localization.Resolve("cmi.auto.registered", "registered character"));
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.Warn($"[auto] ModelDb.AllCharacters read failed: {ex.Message}");
            }

            var modAssemblies = mods.SelectMany(static mod => GetAssemblies(mod.Mod)).Distinct().ToHashSet();
            foreach (var mod in mods)
            {
                try
                {
                    foreach (var assembly in GetAssemblies(mod.Mod))
                    {
                        if (assembly == typeof(CharacterModRegistry).Assembly)
                            continue;

                        ScanAssembly(assembly, mod.Id);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.Warn($"[auto] scan failed for {mod.Id}: {ex.Message}");
                    Auto[mod.Id] = new AutoCharacterResult(
                        CharacterModKind.Unknown,
                        Localization.Resolve("cmi.auto.scanFailed", "scan failed"));
                }
            }

            // Loader-based mods can load a payload assembly without putting it in Mod.assemblies.
            // Scan the remaining process assemblies and resolve ownership through the game map.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic || modAssemblies.Contains(assembly) ||
                    assembly == typeof(CharacterModRegistry).Assembly)
                    continue;

                try
                {
                    ScanAssembly(assembly, null);
                }
                catch
                {
                    // A foreign assembly must not break detection.
                }
            }

            _built = true;

            if (forceLog || ModSettings.Current.VerboseLog)
            {
                foreach (var pair in Auto.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
                    Plugin.Logger.Info($"[auto] {pair.Key} => {pair.Value.Kind} ({pair.Value.Reason})");
            }

            var characterCount = Auto.Count(static pair =>
                pair.Value.Kind is CharacterModKind.Character or CharacterModKind.LikelyCharacter);
            Plugin.Logger.Info($"[auto] built. mods={Auto.Count} character={characterCount}");

            foreach (var pair in Auto.Where(static pair =>
                         pair.Value.Kind is CharacterModKind.Character or CharacterModKind.LikelyCharacter))
                Plugin.Logger.Info($"[auto] character={pair.Key} kind={pair.Value.Kind} reason={pair.Value.Reason}");
        }
    }

    private static void ScanAssembly(Assembly assembly, string? knownModId)
    {
        foreach (var type in GetTypes(assembly))
        {
            if (!IsConcreteCharacter(type))
                continue;

            var owner = knownModId ?? ContentOwnership.GetOwnerModId(type);
            if (string.IsNullOrWhiteSpace(owner))
                continue;

            if (!Auto.TryGetValue(owner, out var existing) ||
                existing.Kind == CharacterModKind.NotCharacter)
            {
                Auto[owner] = new AutoCharacterResult(
                    CharacterModKind.LikelyCharacter,
                    Localization.Resolve("cmi.auto.concrete", "concrete CharacterModel subclass"));
            }
        }
    }

    private static bool IsConcreteCharacter(Type type)
    {
        try
        {
            return type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } &&
                   typeof(CharacterModel).IsAssignableFrom(type);
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<Type> GetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(static type => type is not null)!;
        }
        catch
        {
            return [];
        }
    }

    private static List<ModInfo> LoadedMods()
    {
        try
        {
            return ModManager.GetLoadedMods()
                .Where(static mod => mod.manifest?.id is { Length: > 0 })
                .Select(static mod => new ModInfo(
                    mod,
                    mod.manifest!.id!,
                    string.IsNullOrWhiteSpace(mod.manifest.name) ? mod.manifest.id! : mod.manifest.name!))
                .ToList();
        }
        catch (Exception ex)
        {
            Plugin.Logger.Warn($"[auto] failed to enumerate loaded mods: {ex.Message}");
            return [];
        }
    }

    private static IEnumerable<Assembly> GetAssemblies(Mod mod)
    {
        var list = new List<Assembly>();
        try
        {
            var assembliesField = typeof(Mod).GetField("assemblies", BindingFlags.Public | BindingFlags.Instance);
            if (assembliesField?.GetValue(mod) is IEnumerable<Assembly> assemblies)
                list.AddRange(assemblies);

            var assemblyField = typeof(Mod).GetField("assembly", BindingFlags.Public | BindingFlags.Instance);
            if (assemblyField?.GetValue(mod) is Assembly assembly)
                list.Add(assembly);
        }
        catch
        {
            // Ignore shape differences.
        }

        return list.Distinct();
    }

    private readonly record struct ModInfo(Mod Mod, string Id, string Name);
}
