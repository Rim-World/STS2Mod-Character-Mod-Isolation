using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace P0Probe;

/// <summary>
/// Test-only P4 probes for content paths that do not use the normal reward screen.
/// Enabled only with CMI_PROBE_P4=1 so the normal P0 runs stay unchanged.
/// </summary>
internal static class P4Audit
{
    public static void Run(RunState state)
    {
        if (Environment.GetEnvironmentVariable("CMI_PROBE_P4") != "1")
            return;

        try
        {
            var player = state.Players.FirstOrDefault();
            if (player is null)
            {
                Log.Error($"{Probe.Prefix} p4 no player");
                return;
            }

            Log.Info($"{Probe.Prefix} p4 run character={player.Character.Id.Entry} " +
                     $"contextOwner={Reflection.Owner(player.Character.GetType()) ?? "vanilla"}");

            SplashAudit(player);
            KaleidoscopeAudit(player);
            OrobasAudit();
            SplashInject(state, player);
            TaskHelper.RunSafely(ColorAuditAsync(player));
        }
        catch (Exception ex)
        {
            Log.Error($"{Probe.Prefix} p4 audit failed: {ex}");
        }
    }

    private static void SplashAudit(Player player)
    {
        try
        {
            var ownPool = player.Character.CardPool;
            var pools = player.UnlockState.CharacterCardPools.ToList();
            var removedOwn = pools.Count > 1 && pools.Remove(ownPool);
            var attacks = pools
                .SelectMany(pool => pool.GetUnlockedCards(
                    player.UnlockState, player.RunState.CardMultiplayerConstraint))
                .Where(card => card.Type == CardType.Attack)
                .ToList();
            var isolated = attacks
                .Where(card => Reflection.Owner(card.GetType()) is { Length: > 0 })
                .ToList();
            var ids = string.Join(",", isolated
                .Select(card => card.Id.Entry)
                .Distinct()
                .OrderBy(id => id, StringComparer.Ordinal));
            var poolIds = string.Join(",", pools
                .Select(pool => pool.Id.Entry)
                .OrderBy(id => id, StringComparer.Ordinal));

            Log.Info($"{Probe.Prefix} p4 splash-audit player={player.Character.Id.Entry} " +
                     $"ownRemoved={removedOwn} pools=[{poolIds}] attackCandidates={attacks.Count} " +
                     $"isolated={isolated.Count} ids=[{ids}]");
        }
        catch (Exception ex)
        {
            Log.Error($"{Probe.Prefix} p4 splash-audit failed: {ex}");
        }
    }

    private static void SplashInject(RunState state, Player player)
    {
        try
        {
            for (var i = 0; i < 5; i++)
            {
                var card = state.CreateCard(ModelDb.Card<Splash>(), player);
                card.FloorAddedToDeck = 1;
                player.Deck.AddInternal(card);
            }

            Log.Info($"{Probe.Prefix} p4 splash-inject count=5 deck={player.Deck.Cards.Count} " +
                     $"deckIds=[{string.Join(",", player.Deck.Cards.Select(card => card.Id.Entry))}]");
        }
        catch (Exception ex)
        {
            Log.Error($"{Probe.Prefix} p4 splash-inject failed: {ex}");
        }
    }

    private static void KaleidoscopeAudit(Player player)
    {
        try
        {
            var all = ModelDb.AllCharacters.ToList();
            var pools = player.UnlockState.CharacterCardPools.ToList();
            var removed = all.Count(character =>
                FilterPolicyOwner(character.GetType()) is { Length: > 0 });
            var raw = pools.Count == all.Count;
            var allowed = ModelDb.Relic<Kaleidoscope>().IsAllowedAtNeow(player);
            Log.Info($"{Probe.Prefix} p4 kaleidoscope player={player.Character.Id.Entry} " +
                     $"pools={pools.Count} all={all.Count} removed={removed} raw={raw} allowed={allowed}");
        }
        catch (Exception ex)
        {
            Log.Error($"{Probe.Prefix} p4 kaleidoscope failed: {ex}");
        }
    }

    private static void OrobasAudit()
    {
        try
        {
            var property = typeof(Orobas).GetProperty(
                "SeaGlassOptions", BindingFlags.NonPublic | BindingFlags.Instance);
            if (property is null)
            {
                Log.Error($"{Probe.Prefix} p4 orobas property missing");
                return;
            }

            var options = (property.GetValue(ModelDb.Event<Orobas>()) as IEnumerable)?
                .Cast<EventOption>()
                .ToList() ?? [];
            var characters = options
                .Select(option => (option.Relic as SeaGlass)?.CharacterId?.Entry)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

            Log.Info($"{Probe.Prefix} p4 orobas options={options.Count} " +
                     $"seaGlassCharacters=[{string.Join(",", characters)}]");
        }
        catch (Exception ex)
        {
            Log.Error($"{Probe.Prefix} p4 orobas failed: {ex}");
        }
    }

    private static async Task ColorAuditAsync(Player player)
    {
        try
        {
            var colorful = (ColorfulPhilosophers)ModelDb.Event<ColorfulPhilosophers>().ToMutable();
            await colorful.BeginEvent(player, null, false);

            var pools = player.UnlockState.CharacterCardPools
                .Select(pool => pool.Id.Entry)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            var keys = colorful.CurrentOptions
                .Select(option => option.TextKey)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();

            Log.Info($"{Probe.Prefix} p4 color-event owner={player.Character.Id.Entry} " +
                     $"allowed={colorful.IsAllowed(player.RunState)} pools=[{string.Join(",", pools)}] " +
                     $"options={colorful.CurrentOptions.Count} keys=[{string.Join(",", keys)}]");
        }
        catch (Exception ex)
        {
            Log.Error($"{Probe.Prefix} p4 color-event failed: {ex}");
        }
    }

    private static string? FilterPolicyOwner(Type type)
    {
        try
        {
            var policy = Reflection.IsolationAssembly?.GetType("CharacterModIsolation.FilterPolicy");
            var method = policy?.GetMethod(
                "ShouldRemoveCharacter",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (method is null)
                return null;

            var removed = method.Invoke(null, [type]) is true;
            return removed ? "removed" : null;
        }
        catch
        {
            return null;
        }
    }
}

[HarmonyPatch(typeof(Splash), "OnPlay")]
internal static class P4SplashPlayPatch
{
    private static void Prefix(Splash __instance)
    {
        if (Environment.GetEnvironmentVariable("CMI_PROBE_P4") != "1")
            return;

        try
        {
            Log.Info($"{Probe.Prefix} p4 splash-play " +
                     $"owner={__instance.Owner?.Character.Id.Entry ?? "null"} " +
                     $"upgraded={__instance.IsUpgraded} pile={__instance.Pile?.Type.ToString() ?? "null"}");
        }
        catch (Exception ex)
        {
            Log.Warn($"{Probe.Prefix} p4 splash-play log failed: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(CardFactory), nameof(CardFactory.GetDistinctForCombat))]
internal static class P4CombatGenerationPatch
{
    private static void Prefix(Player player, IEnumerable<CardModel> cards, int count)
    {
        if (Environment.GetEnvironmentVariable("CMI_PROBE_P4") != "1")
            return;

        try
        {
            var source = cards.ToList();
            var isolated = source
                .Where(card => Reflection.Owner(card.GetType()) is { Length: > 0 })
                .ToList();
            Log.Info($"{Probe.Prefix} p4 combat-generation-enter " +
                     $"player={player.Character.Id.Entry} count={count} source={source.Count} " +
                     $"isolated={isolated.Count} ids=[{Ids(isolated)}]");
        }
        catch (Exception ex)
        {
            Log.Warn($"{Probe.Prefix} p4 combat-generation enter failed: {ex.Message}");
        }
    }

    private static void Postfix(Player player, ref IEnumerable<CardModel> __result)
    {
        if (Environment.GetEnvironmentVariable("CMI_PROBE_P4") != "1")
            return;

        try
        {
            var selected = __result?.ToList() ?? [];
            var isolated = selected
                .Where(card => Reflection.Owner(card.GetType()) is { Length: > 0 })
                .ToList();
            Log.Info($"{Probe.Prefix} p4 combat-generation-result " +
                     $"player={player.Character.Id.Entry} count={selected.Count} " +
                     $"isolated={isolated.Count} ids=[{Ids(selected)}]");
        }
        catch (Exception ex)
        {
            Log.Warn($"{Probe.Prefix} p4 combat-generation result failed: {ex.Message}");
        }
    }

    private static string Ids(IEnumerable<CardModel> cards) =>
        string.Join(",", cards.Select(card => card.Id.Entry)
            .Distinct()
            .OrderBy(id => id, StringComparer.Ordinal));
}

[HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromChooseACardScreen))]
internal static class P4SplashSelectPatch
{
    private static void Prefix(object[] __args)
    {
        if (Environment.GetEnvironmentVariable("CMI_PROBE_P4") != "1")
            return;

        try
        {
            var cards = __args.Length > 1
                ? (__args[1] as IEnumerable<CardModel>)?.ToList()
                : null;
            if (cards is null)
            {
                Log.Warn($"{Probe.Prefix} p4 splash-select candidates missing");
                return;
            }

            var isolated = cards
                .Where(card => Reflection.Owner(card.GetType()) is { Length: > 0 })
                .ToList();
            Log.Info($"{Probe.Prefix} p4 splash-select-candidates count={cards.Count} " +
                     $"isolated={isolated.Count} ids=[{string.Join(",", cards.Select(card => card.Id.Entry))}]");
        }
        catch (Exception ex)
        {
            Log.Warn($"{Probe.Prefix} p4 splash-select failed: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(ColorfulPhilosophers), "GenerateInitialOptions")]
internal static class P4ColorOptionsPatch
{
    private static void Postfix(ColorfulPhilosophers __instance, ref IReadOnlyList<EventOption> __result)
    {
        if (Environment.GetEnvironmentVariable("CMI_PROBE_P4") != "1")
            return;

        try
        {
            var owner = __instance.Owner;
            var run = owner?.RunState;
            var pools = owner is null
                ? Array.Empty<string>()
                : owner.UnlockState.CharacterCardPools
                    .Select(pool => pool.Id.Entry)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray();
            Log.Info($"{Probe.Prefix} p4 color-options owner={owner?.Character.Id.Entry ?? "null"} " +
                     $"allowed={(run is not null && __instance.IsAllowed(run))} " +
                     $"pools=[{string.Join(",", pools)}] count={__result.Count} " +
                     $"keys=[{string.Join(",", __result.Select(option => option.TextKey))}]");
        }
        catch (Exception ex)
        {
            Log.Warn($"{Probe.Prefix} p4 color-options failed: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(CardFactory), nameof(CardFactory.CreateForReward),
    new[] { typeof(Player), typeof(int), typeof(CardCreationOptions) })]
internal static class P4ColorRewardPatch
{
    private static void Postfix(Player player, CardCreationOptions options,
        ref IEnumerable<CardCreationResult> __result)
    {
        if (Environment.GetEnvironmentVariable("CMI_PROBE_P4") != "1")
            return;

        try
        {
            var results = __result.ToList();
            Log.Info($"{Probe.Prefix} p4 color-reward player={player.Character.Id.Entry} " +
                     $"pools=[{string.Join(",", options.CardPools.Select(pool => pool.Id.Entry))}] " +
                     $"cards=[{string.Join(",", results.Select(result => result.Card.Id.Entry))}]");
        }
        catch (Exception ex)
        {
            Log.Warn($"{Probe.Prefix} p4 color-reward failed: {ex.Message}");
        }
    }
}

public sealed class P4ConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "p4probe";

    public override string Args => "state|pick";

    public override string Description => "Inspect or pick the Colorful Philosophers option.";

    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        try
        {
            var run = RunManager.Instance.DebugOnlyGetState();
            if (run?.CurrentRoom is not EventRoom room)
                return new CmdResult(false, "not in an event room");

            if (room.LocalMutableEvent is not ColorfulPhilosophers colorful)
                return new CmdResult(false, "current event is " + room.LocalMutableEvent.GetType().Name);

            Log.Info($"{Probe.Prefix} p4 color-command event={colorful.Id.Entry} " +
                     $"allowed={colorful.IsAllowed(run)} options={colorful.CurrentOptions.Count}");
            foreach (var option in colorful.CurrentOptions)
            {
                Log.Info($"{Probe.Prefix} p4 color-option key={option.TextKey} " +
                         $"locked={option.IsLocked} proceed={option.IsProceed}");
            }

            if (args.Length == 0 || args[0].Equals("state", StringComparison.OrdinalIgnoreCase))
                return new CmdResult(true, "state logged");

            if (!args[0].Equals("pick", StringComparison.OrdinalIgnoreCase))
                return new CmdResult(false, "expected state or pick");

            var choice = colorful.CurrentOptions.FirstOrDefault(option => !option.IsLocked && !option.IsProceed);
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
