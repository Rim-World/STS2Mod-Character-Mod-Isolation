using System;
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

[HarmonyPatch]
internal static class ForceMgrCharacterPatch
{
    private const string TargetCharacter = "MGR_MOD_CHARACTER_MGR_CHARACTER";

    private static MethodBase? TargetMethod()
    {
        var type = typeof(AutoSlayer).Assembly.GetType("MegaCrit.Sts2.Core.AutoSlay.Launchers.ModeScreen");
        return type?.GetMethod("SelectRandomCharacterAsync", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
    }

    private static bool Prefix(Control screen, ref Task __result)
    {
        try
        {
            var container = screen.GetNode("CharSelectButtons/ButtonContainer");
            var buttons = UiHelper.FindAll<NCharacterSelectButton>(container);
            foreach (var button in buttons)
            {
                try { button.UnlockIfPossible(); }
                catch (Exception ex) { Log.Warn("[probe] unlock failed: " + ex.Message); }
            }

            var target = buttons.FirstOrDefault(button =>
                string.Equals(button.Character?.Id.Entry, TargetCharacter, StringComparison.Ordinal));
            if (target is null)
            {
                Log.Warn("[probe] MGR character button not found");
                return true;
            }

            target.Select();
            Log.Info("[probe] forced character=" + target.Character!.Id.Entry);
            __result = Task.CompletedTask;
            return false;
        }
        catch (Exception ex)
        {
            Log.Error("[probe] force MGR selection failed: " + ex);
            return true;
        }
    }
}

[HarmonyPatch(typeof(AutoSlayer), "LogRunStartedAndCheckMode")]
internal static class RunStartProbePatch
{
    private static void Postfix(RunState runState)
    {
        try
        {
            var player = runState.Players[0];
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
        }
        catch (Exception ex)
        {
            Log.Error("[probe] run-start inspection failed: " + ex);
        }
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
