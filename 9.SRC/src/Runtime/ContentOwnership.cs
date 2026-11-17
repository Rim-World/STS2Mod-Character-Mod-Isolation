using System.Collections.Concurrent;
using System.Reflection;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;

namespace CharacterModIsolation;

internal enum ContentOrigin
{
    Vanilla,
    Framework,
    Mod,
    Unknown,
}

/// <summary>
/// Maps a model type to the mod that owns its assembly. The game already builds this map in
/// <see cref="AssemblyInfo"/>; we use it first and keep a local fallback for old versions and
/// loader assemblies.
/// </summary>
internal static class ContentOwnership
{
    private static readonly ConcurrentDictionary<Type, ContentOrigin> Origins = new();
    private static readonly ConcurrentDictionary<Type, string> TypeOwners = new();
    private static readonly ConcurrentDictionary<Assembly, string> AssemblyOwners = new();
    private static readonly object Gate = new();
    private static bool _built;

    private static readonly HashSet<string> FrameworkAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "sts2",
        "0Harmony",
        "GodotSharp",
        "GodotSharpEditor",
        "STS2-RitsuLib",
        "STS2-RitsuLib.Runtime",
        "STS2-RitsuLib.Shared",
        "STS2-RitsuLib.Ui",
        "STS2-RitsuLib.Settings",
        "BaseLib",
        "BaseLib.StS2",
        "System.Private.CoreLib",
    };

    public static ContentOrigin GetOrigin(Type type)
    {
        if (Origins.TryGetValue(type, out var cached))
            return cached;

        EnsureBuilt();

        ContentOrigin result;
        try
        {
            if (type.Assembly == typeof(ModelDb).Assembly)
            {
                result = ContentOrigin.Vanilla;
            }
            else if (GetOwnerModId(type) is { Length: > 0 })
            {
                result = ContentOrigin.Mod;
            }
            else if (FrameworkAssemblies.Contains(type.Assembly.GetName().Name ?? string.Empty))
            {
                result = ContentOrigin.Framework;
            }
            else
            {
                result = ContentOrigin.Unknown;
            }
        }
        catch
        {
            result = ContentOrigin.Unknown;
        }

        Origins[type] = result;
        return result;
    }

    public static string? GetOwnerModId(Type type)
    {
        if (TypeOwners.TryGetValue(type, out var cached))
            return cached.Length == 0 ? null : cached;

        EnsureBuilt();

        var fromMap = GetFromAssembly(type.Assembly);
        if (fromMap is not null)
        {
            TypeOwners[type] = fromMap;
            return fromMap;
        }

        try
        {
            var method = typeof(AssemblyInfo).GetMethod(
                "ModForType",
                BindingFlags.Public | BindingFlags.Static,
                null,
                [typeof(Type), typeof(bool).MakeByRefType()],
                null);
            if (method is null)
                return null;

            object?[] args = [type, false];
            var mod = method.Invoke(null, args) as Mod;
            var result = args[1] is true ? null : mod?.manifest?.id;
            TypeOwners[type] = result ?? string.Empty;
            return result;
        }
        catch
        {
            TypeOwners[type] = string.Empty;
            return null;
        }
    }

    public static void EnsureBuilt()
    {
        if (_built)
            return;

        lock (Gate)
        {
            if (_built)
                return;

            try
            {
                foreach (var mod in ModManager.GetLoadedMods())
                {
                    var modId = mod.manifest?.id;
                    if (string.IsNullOrWhiteSpace(modId))
                        continue;

                    foreach (var assembly in GetAssemblies(mod))
                        AssemblyOwners.TryAdd(assembly, modId);

                    try
                    {
                        var root = Path.GetFullPath(mod.path);
                        foreach (var assembly in GetAssemblies(mod))
                        {
                            var location = assembly.Location;
                            if (string.IsNullOrWhiteSpace(location))
                                continue;
                            if (Path.GetFullPath(location).StartsWith(root, StringComparison.OrdinalIgnoreCase))
                                AssemblyOwners.TryAdd(assembly, modId);
                        }
                    }
                    catch
                    {
                        // A malformed path must not prevent the rest of the map.
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.Warn($"[ownership] failed to enumerate loaded mods: {ex.Message}");
            }

            _built = true;
        }
    }

    private static string? GetFromAssembly(Assembly assembly)
    {
        if (assembly == typeof(ModelDb).Assembly)
            return null;

        return AssemblyOwners.TryGetValue(assembly, out var known) ? known : null;
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
            // Old or new host shape; fall through with an empty list.
        }

        return list.Distinct();
    }
}
