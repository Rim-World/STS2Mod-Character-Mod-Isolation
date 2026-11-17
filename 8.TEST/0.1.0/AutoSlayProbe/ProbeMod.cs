using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.AutoSlay;
using MegaCrit.Sts2.Core.AutoSlay.Helpers;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

namespace AutoSlayProbe;

[ModInitializer(nameof(Initialize))]
public static class ProbeMod
{
    public static void Initialize()
    {
        try
        {
            new Harmony("AutoSlayProbe").PatchAll(typeof(ProbeMod).Assembly);
            Log.Info("[probe] installed");
        }
        catch (Exception ex)
        {
            Log.Error("[probe] install failed: " + ex);
        }

        try
        {
            var target = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "CharacterModIsolation")
                ?.GetType("CharacterModIsolation.PatchRegistry");
            var install = target?.GetMethod("InstallAll", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (install is null)
            {
                Log.Warn("[probe] CharacterModIsolation PatchRegistry.InstallAll not found");
            }
            else
            {
                install.Invoke(null, null);
                Log.Info("[probe] forced CharacterModIsolation patch installation");
            }
        }
        catch (Exception ex)
        {
            Log.Error("[probe] forced patch installation failed: " + ex);
        }

        try
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "CharacterModIsolation")
                ?.GetType("CharacterModIsolation.Patches.PoolPatches");
            var callback = type?.GetMethod("CardPoolPostfix",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (callback is null)
            {
                Log.Warn("[probe] CardPoolPostfix not found");
                return;
            }

            var prefix = new HarmonyMethod(typeof(CardPoolCallbackProbe).GetMethod(
                nameof(CardPoolCallbackProbe.Prefix), BindingFlags.Public | BindingFlags.Static));
            var callbackPostfix = new HarmonyMethod(typeof(CardPoolCallbackProbe).GetMethod(
                nameof(CardPoolCallbackProbe.Postfix), BindingFlags.Public | BindingFlags.Static));
            new Harmony("AutoSlayProbe.cardpool").Patch(callback, prefix: prefix, postfix: callbackPostfix);
            Log.Info("[probe] CardPoolPostfix callback probe installed");

            var policyType = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "CharacterModIsolation")
                ?.GetType("CharacterModIsolation.FilterPolicy");
            var shouldRemove = policyType?.GetMethod("ShouldRemoveCard",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (shouldRemove is not null)
            {
                var shouldPostfix = new HarmonyMethod(typeof(ShouldRemoveProbe).GetMethod(
                    nameof(ShouldRemoveProbe.Postfix), BindingFlags.Public | BindingFlags.Static));
                new Harmony("AutoSlayProbe.shouldremove").Patch(shouldRemove, postfix: shouldPostfix);
                Log.Info("[probe] ShouldRemoveCard probe installed");
            }
        }
        catch (Exception ex)
        {
            Log.Error("[probe] CardPoolPostfix callback probe failed: " + ex);
        }
    }
}

internal static class CardPoolCallbackProbe
{
    public static void Prefix([HarmonyArgument(0)] CardPoolModel pool)
    {
        var stateType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("CharacterModIsolation.RunContext"))
            .FirstOrDefault(type => type is not null);
        var active = stateType?.GetProperty("IsActive",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);

        var policyType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("CharacterModIsolation.FilterPolicy"))
            .FirstOrDefault(type => type is not null);
        var cardsActive = policyType?.GetProperty("CardsActive",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
        var shouldRemove = policyType?.GetMethod("ShouldRemoveCard",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        var sample = pool.AllCards.FirstOrDefault(card => CoverageProbe.OwnerOf(card.GetType()) is { Length: > 0 });
        var sampleId = sample?.Id.Entry ?? "none";
        var sampleShould = sample is null ? null : shouldRemove?.Invoke(null, new object[] { sample.GetType() });

        Log.Info($"[probe] CardPoolPostfix enter type={pool.GetType().Name} context={active} " +
                 $"cardsActive={cardsActive} sample={sampleId} sampleShould={sampleShould}");
    }

    public static void Postfix(object[] __args)
    {
        var pool = __args.Length > 0 ? __args[0] as CardPoolModel : null;
        var result = __args.Length > 1 ? __args[1] as IEnumerable<CardModel> : null;
        if (pool?.GetType().Name == "TokenCardPool")
            Log.Info($"[probe] CardPoolPostfix leave type={pool.GetType().Name} count={result?.Count() ?? -1}");
    }
}

internal static class ShouldRemoveProbe
{
    public static void Postfix(Type type, ref bool __result)
    {
        var assembly = type.Assembly.GetName().Name;
        if (assembly is "Watcher" or "MGRMod")
            Log.Info($"[probe] ShouldRemoveCard type={type.Name} result={__result}");
    }
}

[HarmonyPatch(typeof(CardPoolModel), nameof(CardPoolModel.GetUnlockedCards))]
[HarmonyPriority(Priority.First)]
internal static class CardPoolResultProbeFirst
{
    [HarmonyPostfix]
    private static void Postfix(CardPoolModel __instance, ref IEnumerable<CardModel> __result)
    {
        if (__instance.GetType().Name == "TokenCardPool")
            Log.Info($"[probe] token-pool result before-filter count={__result?.Count() ?? -1}");
    }
}

[HarmonyPatch(typeof(CardPoolModel), nameof(CardPoolModel.GetUnlockedCards))]
[HarmonyPriority(Priority.Last)]
internal static class CardPoolResultProbeLast
{
    [HarmonyPostfix]
    private static void Postfix(CardPoolModel __instance, ref IEnumerable<CardModel> __result)
    {
        if (__instance.GetType().Name == "TokenCardPool")
            Log.Info($"[probe] token-pool result after-filter count={__result?.Count() ?? -1}");
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

[HarmonyPatch(typeof(NCharacterSelectButton), nameof(NCharacterSelectButton.Select))]
internal static class ForceVanillaRegentPatch
{
    private static bool IsRegent(NCharacterSelectButton button) =>
        button.Character?.Id.Entry is "REGENT" or "CHARACTER.REGENT";

    private static bool Prefix(NCharacterSelectButton __instance)
    {
        try
        {
            if (IsRegent(__instance))
                return true;

            var buttons = UiHelper.FindAll<NCharacterSelectButton>(__instance.GetParent());
            foreach (var button in buttons)
            {
                try { button.UnlockIfPossible(); }
                catch (Exception ex) { Log.Warn("[probe] unlock failed: " + ex.Message); }
            }

            var target = buttons.FirstOrDefault(IsRegent);
            if (target is null)
                return true;

            Log.Info($"[probe] redirect character {__instance.Character?.Id.Entry ?? "random"} -> REGENT");
            target.Select();
            return false;
        }
        catch (Exception ex)
        {
            Log.Error("[probe] force vanilla Regent selection failed: " + ex);
            return true;
        }
    }
}

[HarmonyPatch(typeof(NCharacterSelectScreen), "OnSubmenuOpened")]
internal static class ForceVanillaRegentOpenedPatch
{
    private static bool IsRegent(NCharacterSelectButton button) =>
        button.Character?.Id.Entry is "REGENT" or "CHARACTER.REGENT";

    [HarmonyPostfix]
    private static void Postfix(NCharacterSelectScreen __instance)
    {
        try
        {
            var buttons = UiHelper.FindAll<NCharacterSelectButton>(__instance);
            foreach (var button in buttons)
            {
                try { button.UnlockIfPossible(); }
                catch (Exception ex) { Log.Warn("[probe] unlock failed: " + ex.Message); }
            }

            var target = buttons.FirstOrDefault(IsRegent);
            if (target is null)
            {
                var ids = string.Join(",", buttons.Select(button => button.Character?.Id.Entry ?? "null"));
                Log.Warn($"[probe] Regent after-open not found buttons=[{ids}]");
                return;
            }

            if (target.IsSelected)
            {
                Log.Info("[probe] Regent already selected after-open");
                return;
            }

            Log.Info($"[probe] force Regent after-open; was={buttons.FirstOrDefault(button => button.IsSelected)?.Character?.Id.Entry ?? "none"}");
            target.Select();
        }
        catch (Exception ex)
        {
            Log.Error("[probe] force Regent after-open failed: " + ex);
        }
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewSingleplayer))]
internal static class RunStartProbePatch
{
    private static void Postfix(RunState state)
    {
        try
        {
            var player = state.Players[0];
            var deck = string.Join(",", player.Deck.Cards.Select(card => card.Id.Entry));
            var contextType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("CharacterModIsolation.RunContext"))
                .FirstOrDefault(type => type is not null);
            var ensure = contextType?.GetMethod("EnsureForCurrentRun",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            ensure?.Invoke(null, null);
            var active = contextType?.GetProperty("IsActive",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            var reason = contextType?.GetProperty("Reason",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            Log.Info($"[probe] run character={player.Character.Id.Entry} cmi_active={active} cmi_reason={reason} deck=[{deck}]");
            CoverageProbe.Run(state);
        }
        catch (Exception ex)
        {
            Log.Error("[probe] run-start inspection failed: " + ex);
        }
    }
}

internal static class CoverageProbe
{
    private static readonly MethodInfo? IsolatedOwner = AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType("CharacterModIsolation.CharacterModRegistry"))
        .Where(type => type is not null)
        .Select(type => type!.GetMethod("GetIsolatedOwner",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        .FirstOrDefault(method => method is not null);

    private static readonly Dictionary<Type, string?> OwnerCache = new();
    private static readonly MethodInfo? PoolGetUnlockedCards = typeof(CardPoolModel).GetMethod(
        nameof(CardPoolModel.GetUnlockedCards),
        BindingFlags.Public | BindingFlags.Instance,
        null, [typeof(UnlockState), typeof(CardMultiplayerConstraint)], null);
    private static readonly MethodInfo? ShouldRemoveCard = AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType("CharacterModIsolation.FilterPolicy"))
        .Where(type => type is not null)
        .Select(type => type!.GetMethod("ShouldRemoveCard",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        .FirstOrDefault(method => method is not null);

    public static void Run(RunState runState)
    {
        try
        {
            var player = runState.Players[0];
            var unlock = player.UnlockState;

            LogSettings();
            LogPoolPatchInfo();
            LogList("characters", unlock.Characters);
            LogList("card-pools", unlock.CardPools);
            LogList("cards", unlock.CardPools.SelectMany(pool =>
                pool.GetUnlockedCards(unlock, CardMultiplayerConstraint.None)));
            foreach (var pool in unlock.CardPools)
            {
                var cards = pool.GetUnlockedCards(unlock, CardMultiplayerConstraint.None).ToList();
                var isolated = cards.Where(card => IsIsolated(card.GetType())).ToList();
                var reflected = QueryByReflection(pool, unlock);
                Log.Info($"[probe] cardpool id={pool.Id.Entry} type={pool.GetType().FullName} " +
                         $"owner={OwnerOf(pool.GetType()) ?? "null"} total={cards.Count} isolated={isolated.Count} " +
                         $"reflectTotal={reflected.Count} reflectIsolated={reflected.Count(card => IsIsolated(card.GetType()))}");
                foreach (var card in isolated)
                    LogCard(card);
            }
            LogList("relics", unlock.Relics);
            LogList("potions", unlock.Potions);
            LogList("shared-events", ModelDb.AllSharedEvents);
            LogList("shared-ancients", unlock.SharedAncients);
            LogList("acts", runState.Acts);

            foreach (var act in runState.Acts)
            {
                LogList($"act{act.Index}.encounters", act.AllEncounters);
                LogList($"act{act.Index}.monsters", act.AllMonsters);
                LogList($"act{act.Index}.events", act.AllEvents);
                LogList($"act{act.Index}.ancients", act.AllAncients);
            }
        }
        catch (Exception ex)
        {
            Log.Error("[probe] coverage failed: " + ex);
        }
    }

    private static void LogList(string category, IEnumerable models)
    {
        var list = models.Cast<object>().ToList();
        var isolated = list.Where(model => IsIsolated(model.GetType())).ToList();
        var ids = string.Join(",", isolated.Select(Id).Distinct().OrderBy(id => id, StringComparer.Ordinal));
        Log.Info($"[probe] coverage category={category} total={list.Count} isolated={isolated.Count} ids=[{ids}]");
    }

    private static List<CardModel> QueryByReflection(CardPoolModel pool, UnlockState unlock)
    {
        if (PoolGetUnlockedCards is null)
            return [];
        return ((IEnumerable<CardModel>?)PoolGetUnlockedCards.Invoke(
            pool, [unlock, CardMultiplayerConstraint.None]))?.ToList() ?? [];
    }

    private static void LogPoolPatchInfo()
    {
        try
        {
            var method = typeof(CardPoolModel).GetMethod(
                nameof(CardPoolModel.GetUnlockedCards),
                BindingFlags.Public | BindingFlags.Instance,
                null, [typeof(UnlockState), typeof(CardMultiplayerConstraint)], null);
            var info = method is null ? null : Harmony.GetPatchInfo(method);
            if (info is null)
            {
                Log.Warn("[probe] card-pool patch info not found");
                return;
            }

            foreach (var patch in info.Postfixes.OrderBy(p => p.priority))
            {
                Log.Info($"[probe] card-pool postfix owner={patch.owner} " +
                         $"method={patch.PatchMethod.DeclaringType?.FullName}.{patch.PatchMethod.Name} " +
                         $"priority={patch.priority}");
            }
        }
        catch (Exception ex)
        {
            Log.Warn("[probe] card-pool patch info failed: " + ex.Message);
        }
    }

    private static void LogSettings()
    {
        try
        {
            var settingsType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("CharacterModIsolation.ModSettings"))
                .FirstOrDefault(type => type is not null);
            var settings = settingsType?.GetProperty("Current",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            if (settings is null)
            {
                Log.Warn("[probe] settings object not found");
                return;
            }
            var snapshotType = settings.GetType();

            string Read(string name) => snapshotType.GetProperty(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(settings)?.ToString() ?? "?";

            Log.Info($"[probe] filter-flags source={Read("Source")} master={Read("MasterEnabled")} " +
                     $"cards={Read("FilterCards")} relics={Read("FilterRelics")} potions={Read("FilterPotions")} " +
                     $"events={Read("FilterEvents")} encounters={Read("FilterEncounters")} " +
                     $"ancients={Read("FilterAncients")} acts={Read("FilterActs")}");
        }
        catch (Exception ex)
        {
            Log.Warn("[probe] settings inspection failed: " + ex.Message);
        }
    }

    private static void LogCard(CardModel card)
    {
        var type = card.GetType();
        var remove = false;
        try
        {
            remove = ShouldRemoveCard?.Invoke(null, new object[] { type }) is true;
        }
        catch (Exception ex)
        {
            Log.Warn("[probe] ShouldRemoveCard failed: " + ex.Message);
        }

        Log.Info($"[probe] isolated-card id={card.Id.Entry} type={type.FullName} " +
                 $"asm={type.Assembly.GetName().Name} owner={OwnerOf(type) ?? "null"} " +
                 $"pool={card.Pool?.Id.Entry ?? "null"} shouldRemove={remove}");
    }

    private static bool IsIsolated(Type type) => OwnerOf(type) is { Length: > 0 };

    internal static string? OwnerOf(Type type)
    {
        if (IsolatedOwner is null)
            return null;
        if (OwnerCache.TryGetValue(type, out var cached))
            return cached;

        string? result = null;
        try
        {
            result = IsolatedOwner.Invoke(null, new object[] { type }) as string;
        }
        catch
        {
            result = null;
        }

        OwnerCache[type] = result;
        return result;
    }

    private static string Id(object model)
    {
        if (model is AbstractModel abstractModel)
        {
            try { return abstractModel.Id.Entry; }
            catch { return abstractModel.GetType().Name; }
        }

        return model.GetType().Name;
    }
}

[HarmonyPatch(typeof(ColorfulPhilosophers), "GenerateInitialOptions")]
internal static class EventOptionsPatch
{
    [HarmonyPostfix]
    private static void Postfix(ColorfulPhilosophers __instance, ref IReadOnlyList<EventOption> __result)
    {
        var owner = __instance.Owner;
        var run = owner?.RunState;
        Log.Info($"[probe] event=ColorfulPhilosophers owner={owner?.Character.Id.Entry ?? "null"} allowed={run is not null && __instance.IsAllowed(run)}");
        if (owner is not null)
        {
            var characters = owner.UnlockState.Characters.Select(c => c.Id.Entry).OrderBy(x => x).ToArray();
            var pools = owner.UnlockState.CharacterCardPools.Select(p => p.Id.Entry).OrderBy(x => x).ToArray();
            Log.Info($"[probe] unlock characters=[{string.Join(",", characters)}] pools=[{string.Join(",", pools)}]");
        }

        Log.Info($"[probe] options count={__result.Count}");
        foreach (var option in __result)
        {
            Log.Info($"[probe] option key={option.TextKey} title={option.Title.GetRawText()} locked={option.IsLocked} proceed={option.IsProceed}");
        }
    }
}

[HarmonyPatch(typeof(ColorfulPhilosophers), "OfferRewards")]
internal static class EventRewardPatch
{
    [HarmonyPrefix]
    private static void Prefix(CardPoolModel pool)
    {
        Log.Info($"[probe] reward pool={pool.Id.Entry} type={pool.GetType().Name}");
    }
}

[HarmonyPatch(typeof(CardFactory), nameof(CardFactory.CreateForReward), new[] { typeof(Player), typeof(int), typeof(CardCreationOptions) })]
internal static class CardRewardPatch
{
    [HarmonyPostfix]
    private static void Postfix(Player player, int cardCount, CardCreationOptions options, ref IEnumerable<CardCreationResult> __result)
    {
        var cards = __result.ToList();
        var pools = options.CardPools.Select(p => p.Id.Entry).ToArray();
        Log.Info($"[probe] reward cards player={player.Character.Id.Entry} count={cards.Count} pools=[{string.Join(",", pools)}] cards=[{string.Join(",", cards.Select(c => c.Card.Id.Entry))}]");
    }
}

public sealed class ProbeConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "cmiprobe";
    public override string Args => "state|pick";
    public override string Description => "Probe Colorful Philosophers options and choose the first option.";
    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        try
        {
            var run = RunManager.Instance.DebugOnlyGetState();
            if (run?.CurrentRoom is not EventRoom room)
                return new CmdResult(false, "not in an event room");
            var ev = room.LocalMutableEvent;
            if (ev is not ColorfulPhilosophers cp)
                return new CmdResult(false, "current event is " + ev.GetType().Name);
            Log.Info($"[probe] command event={cp.Id.Entry} allowed={cp.IsAllowed(run)} options={cp.CurrentOptions.Count}");
            foreach (var option in cp.CurrentOptions)
                Log.Info($"[probe] command option key={option.TextKey} title={option.Title.GetRawText()} locked={option.IsLocked} proceed={option.IsProceed}");

            if (args.Length == 0 || args[0].Equals("state", StringComparison.OrdinalIgnoreCase))
                return new CmdResult(true, "state logged");

            if (!args[0].Equals("pick", StringComparison.OrdinalIgnoreCase))
                return new CmdResult(false, "expected state or pick");

            var choice = cp.CurrentOptions.FirstOrDefault(o => !o.IsLocked && !o.IsProceed);
            if (choice is null)
                return new CmdResult(false, "no selectable option");
            TaskHelper.RunSafely(choice.Chosen());
            return new CmdResult(true, "picked " + choice.TextKey);
        }
        catch (Exception ex)
        {
            return new CmdResult(false, ex.GetType().Name + ": " + ex.Message);
        }
    }
}
