using System;
using System.Collections.Generic;
using System.Linq;
using STS2RitsuLib;
using STS2RitsuLib.Data;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;
using MegaCrit.Sts2.Core.Modding;

namespace CharacterModIsolation;

internal enum IsolationSource
{
    Auto = 0,
    Manual = 1,
}

internal sealed class SettingsModel
{
    public int SchemaVersion { get; set; }

    public IsolationSource Source { get; set; } = IsolationSource.Auto;

    public bool MasterEnabled { get; set; } = true;

    public bool FilterCards { get; set; } = true;

    public bool FilterRelics { get; set; } = true;

    public bool FilterPotions { get; set; } = true;

    public bool FilterEvents { get; set; } = true;

    public bool FilterEncounters { get; set; } = true;

    public bool FilterAncients { get; set; } = true;

    public bool FilterActs { get; set; } = true;

    public bool VerboseLog { get; set; }

    public List<string>? ManualModIds { get; set; }
}

internal sealed class SettingsSnapshot
{
    public required IsolationSource Source { get; init; }
    public required bool MasterEnabled { get; init; }
    public required bool FilterCards { get; init; }
    public required bool FilterRelics { get; init; }
    public required bool FilterPotions { get; init; }
    public required bool FilterEvents { get; init; }
    public required bool FilterEncounters { get; init; }
    public required bool FilterAncients { get; init; }
    public required bool FilterActs { get; init; }
    public required bool VerboseLog { get; init; }
    public required IReadOnlySet<string> ManualModIds { get; init; }
}

internal static class ModSettings
{
    public const string DataKey = "settings";
    public const string FileName = "settings.json";
    public const int CurrentSchema = 1;

    private static bool _registered;
    private static bool _pageRegistered;
    private static int _version;
    private static SettingsSnapshot? _cached;
    private static int _cachedVersion = -1;

    private static ModDataStore Store => ModDataStore.For(Plugin.ModId);

    public static int Version => _version;

    private static IModSettingsValueBinding<IsolationSource> SourceBinding { get; } =
        ModSettingsBindings.WithDefault(
            ModSettingsBindings.Global<SettingsModel, IsolationSource>(
                Plugin.ModId,
                DataKey,
                static model => model.Source,
                static (model, value) =>
                {
                    model.Source = value;
                    Invalidate();
                }),
            static () => IsolationSource.Auto);

    private static IModSettingsValueBinding<bool> MasterBinding { get; } =
        ModSettingsBindings.WithDefault(
            ModSettingsBindings.Global<SettingsModel, bool>(
                Plugin.ModId,
                DataKey,
                static model => model.MasterEnabled,
                static (model, value) =>
                {
                    model.MasterEnabled = value;
                    Invalidate();
                }),
            static () => true);

    private static IModSettingsValueBinding<List<string>> ManualIdsBinding { get; } =
        ModSettingsBindings.WithDefault(
            ModSettingsBindings.Global<SettingsModel, List<string>>(
                Plugin.ModId,
                DataKey,
                static model => model.ManualModIds ?? new List<string>(),
                static (model, value) =>
                {
                    model.ManualModIds = value;
                    Invalidate();
                }),
            static () => new List<string>());

    private static IModSettingsValueBinding<bool> CardsBinding { get; } = Category(
        static model => model.FilterCards,
        static (model, value) => model.FilterCards = value);

    private static IModSettingsValueBinding<bool> RelicsBinding { get; } = Category(
        static model => model.FilterRelics,
        static (model, value) => model.FilterRelics = value);

    private static IModSettingsValueBinding<bool> PotionsBinding { get; } = Category(
        static model => model.FilterPotions,
        static (model, value) => model.FilterPotions = value);

    private static IModSettingsValueBinding<bool> EventsBinding { get; } = Category(
        static model => model.FilterEvents,
        static (model, value) => model.FilterEvents = value);

    private static IModSettingsValueBinding<bool> EncountersBinding { get; } = Category(
        static model => model.FilterEncounters,
        static (model, value) => model.FilterEncounters = value);

    private static IModSettingsValueBinding<bool> AncientsBinding { get; } = Category(
        static model => model.FilterAncients,
        static (model, value) => model.FilterAncients = value);

    private static IModSettingsValueBinding<bool> ActsBinding { get; } = Category(
        static model => model.FilterActs,
        static (model, value) => model.FilterActs = value);

    private static IModSettingsValueBinding<bool> VerboseBinding { get; } =
        ModSettingsBindings.WithDefault(
            ModSettingsBindings.Global<SettingsModel, bool>(
                Plugin.ModId,
                DataKey,
                static model => model.VerboseLog,
                static (model, value) =>
                {
                    model.VerboseLog = value;
                    Invalidate();
                }),
            static () => false);

    public static SettingsSnapshot Current
    {
        get
        {
            var cached = _cached;
            if (cached is not null && _cachedVersion == _version)
                return cached;

            var snapshot = Build();
            _cached = snapshot;
            _cachedVersion = _version;
            return snapshot;
        }
    }

    public static void RegisterData()
    {
        if (_registered)
            return;

        using (RitsuLibFramework.BeginModDataRegistration(Plugin.ModId, true))
        {
            Store.Register<SettingsModel>(
                DataKey,
                FileName,
                SaveScope.Global,
                defaultFactory: static () => new SettingsModel { SchemaVersion = CurrentSchema },
                autoCreateIfMissing: true);
        }

        _registered = true;
        Invalidate();
    }

    public static void RegisterPage()
    {
        if (_pageRegistered)
            return;

        try
        {
            RitsuLibFramework.RegisterModSettings(Plugin.ModId, ConfigurePage);
            _pageRegistered = true;
            Plugin.Logger.Info("[settings] page registered");
        }
        catch (Exception ex)
        {
            Plugin.Logger.Error($"[settings] page registration failed: {ex}");
        }
    }

    private static void ConfigurePage(ModSettingsPageBuilder page)
    {
        page.WithModDisplayName(Localization.Text("cmi.mod.name", "Character Mod Isolation"))
            .WithTitle(Localization.Text("cmi.page.title", "Character Mod Isolation"))
            .WithDescription(Localization.Text("cmi.page.desc",
                "Hide content added by character mods when the current run has only vanilla characters. " +
                "Character mods stay enabled and selectable."));

        page.AddSection("general", AddGeneralSection);
        page.AddSection("mods", AddModsSection);
    }

    private static void AddGeneralSection(ModSettingsSectionBuilder section)
    {
        section.WithTitle(Localization.Text("cmi.section.general", "General"))
            .WithDescription(Localization.Text("cmi.general.desc",
                "Changes apply to the next run. The current run keeps its decision."))
            .AddToggle("master", Localization.Text("cmi.master.name", "Enable isolation"), MasterBinding,
                Localization.Text("cmi.master.desc", "Off makes this mod do nothing."))
            .AddEnumChoice<IsolationSource>(
                "source",
                Localization.Text("cmi.source.name", "Detection mode"),
                SourceBinding,
                static value => value == IsolationSource.Auto
                    ? Localization.Text("cmi.source.auto", "Automatic")
                    : Localization.Text("cmi.source.manual", "Manual selection"),
                Localization.Text("cmi.source.desc",
                    "Automatic detects character mods. Manual skips detection and isolates only the mods you check."),
                ModSettingsChoicePresentation.Dropdown)
            .AddToggle("cards", Localization.Text("cmi.filter.cards", "Cards"), CardsBinding, null)
            .AddToggle("relics", Localization.Text("cmi.filter.relics", "Relics"), RelicsBinding, null)
            .AddToggle("potions", Localization.Text("cmi.filter.potions", "Potions"), PotionsBinding, null)
            .AddToggle("events", Localization.Text("cmi.filter.events", "Events"), EventsBinding, null)
            .AddToggle("encounters", Localization.Text("cmi.filter.encounters", "Encounters"), EncountersBinding, null)
            .AddToggle("ancients", Localization.Text("cmi.filter.ancients", "Ancients"), AncientsBinding, null)
            .AddToggle("acts", Localization.Text("cmi.filter.acts", "Acts"), ActsBinding, null)
            .AddToggle("verbose", Localization.Text("cmi.verbose.name", "Verbose log"), VerboseBinding,
                Localization.Text("cmi.verbose.desc", "Logs every filter decision. Keep off unless diagnosing."));
    }

    private static void AddModsSection(ModSettingsSectionBuilder section)
    {
        var mods = GetSelectableMods();

        section.WithTitle(Localization.Text("cmi.section.mods", "Loaded mods"))
            .WithDescription(Localization.Text("cmi.mods.desc",
                "Automatic mode uses role evidence. Manual mode isolates exactly the checked mods."));

        if (mods.Count == 0)
        {
            section.AddParagraph("empty",
                Localization.Text("cmi.mods.empty", "No loaded mods found."),
                Localization.Text("cmi.mods.empty.desc", "Content from built-in game assemblies is never isolated."));
            return;
        }

        section.AddButton(
            "rescan",
            Localization.Text("cmi.rescan.name", "Re-detect character mods"),
            Localization.Text("cmi.rescan.button", "Re-detect"),
            static host =>
            {
                CharacterModRegistry.RescanAuto();
                host.RequestRefresh();
            },
            description: Localization.Text("cmi.rescan.desc", "Only used in automatic mode."));

        for (var i = 0; i < mods.Count; i++)
        {
            var mod = mods[i];
            var id = mod.Id;
            var display = mod.Name;
            var index = i;

            section.AddParagraph(
                "auto_" + index,
                ModSettingsText.Dynamic(() => display + " · " + id),
                ModSettingsText.Dynamic(() => CharacterModRegistry.DescribeAuto(id)));

            var toggle = ModSettingsBindings.WithDefault(
                ModSettingsBindings.Project<List<string>, bool>(
                    ManualIdsBinding,
                    id,
                    list => list.Contains(id, StringComparer.Ordinal),
                    (list, selected) => Toggle(list, id, selected)),
                () => false);

            section.AddToggle(
                    "manual_" + index,
                    ModSettingsText.Dynamic(() => Localization.Resolve("cmi.mods.check", "Isolate") + " · " + display),
                    toggle,
                    ModSettingsText.Dynamic(() => id))
                .WithEntryVisibleWhen("manual_" + index, () => Current.Source == IsolationSource.Manual)
                .WithEntryVisibleWhen("auto_" + index, () => Current.Source == IsolationSource.Auto);
        }
    }

    private static IModSettingsValueBinding<bool> Category(
        Func<SettingsModel, bool> getter,
        Action<SettingsModel, bool> setter) =>
        ModSettingsBindings.WithDefault(
            ModSettingsBindings.Global<SettingsModel, bool>(
                Plugin.ModId,
                DataKey,
                model => getter(model),
                (model, value) =>
                {
                    setter(model, value);
                    Invalidate();
                }),
            () => true);

    private static List<LoadedMod> GetSelectableMods()
    {
        try
        {
            return ModManager.GetLoadedMods()
                .Where(static mod => mod.manifest?.id is { Length: > 0 } id && id != Plugin.ModId)
                .Select(static mod => new LoadedMod(
                    mod.manifest!.id!,
                    string.IsNullOrWhiteSpace(mod.manifest.name) ? mod.manifest.id! : mod.manifest.name!))
                .OrderBy(static mod => mod.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            Plugin.Logger.Warn($"[settings] failed to enumerate mods: {ex.Message}");
            return [];
        }
    }

    private static SettingsSnapshot Build()
    {
        if (!_registered)
            return Defaults();

        try
        {
            var manual = new HashSet<string>(ManualIdsBinding.Read(), StringComparer.Ordinal);
            return new SettingsSnapshot
            {
                Source = SourceBinding.Read(),
                MasterEnabled = MasterBinding.Read(),
                FilterCards = CardsBinding.Read(),
                FilterRelics = RelicsBinding.Read(),
                FilterPotions = PotionsBinding.Read(),
                FilterEvents = EventsBinding.Read(),
                FilterEncounters = EncountersBinding.Read(),
                FilterAncients = AncientsBinding.Read(),
                FilterActs = ActsBinding.Read(),
                VerboseLog = VerboseBinding.Read(),
                ManualModIds = manual,
            };
        }
        catch (Exception ex)
        {
            Plugin.Logger.Error($"[settings] read failed; using defaults: {ex}");
            return Defaults();
        }
    }

    private static SettingsSnapshot Defaults() => new()
    {
        Source = IsolationSource.Auto,
        MasterEnabled = true,
        FilterCards = true,
        FilterRelics = true,
        FilterPotions = true,
        FilterEvents = true,
        FilterEncounters = true,
        FilterAncients = true,
        FilterActs = true,
        VerboseLog = false,
        ManualModIds = new HashSet<string>(StringComparer.Ordinal),
    };

    private static void Invalidate()
    {
        _version++;
        _cached = null;
    }

    private static List<string> Toggle(List<string> source, string id, bool selected)
    {
        var copy = new List<string>(source);
        if (selected)
        {
            if (!copy.Contains(id, StringComparer.Ordinal))
                copy.Add(id);
        }
        else
        {
            copy.RemoveAll(item => string.Equals(item, id, StringComparison.Ordinal));
        }

        return copy;
    }

    private readonly record struct LoadedMod(string Id, string Name);
}
