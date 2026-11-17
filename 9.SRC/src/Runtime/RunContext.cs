using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

namespace CharacterModIsolation;

internal static class RunContext
{
    private static readonly object Gate = new();
    private static RunState? _cachedRun;
    private static bool _active;
    private static string _reason = "not initialized";
    private static IsolationSource _source;
    private static HashSet<string> _effective = new(StringComparer.Ordinal);

    private static bool _pendingSet;
    private static bool _pendingActive;
    private static string _pendingReason = string.Empty;
    private static IsolationSource _pendingSource;
    private static HashSet<string> _pendingEffective = new(StringComparer.Ordinal);

    public static bool IsActive
    {
        get
        {
            EnsureForCurrentRun();
            return _active;
        }
    }

    public static string Reason
    {
        get
        {
            EnsureForCurrentRun();
            return _reason;
        }
    }

    public static IsolationSource Source
    {
        get
        {
            EnsureForCurrentRun();
            return _source;
        }
    }

    public static IReadOnlySet<string> EffectiveModIds
    {
        get
        {
            EnsureForCurrentRun();
            return _effective;
        }
    }

    public static bool IsActiveForActs
    {
        get
        {
            if (_pendingSet)
                return _pendingActive;
            return IsActive;
        }
    }

    public static string ActsReason => _pendingSet ? _pendingReason : Reason;

    public static void EnsureForCurrentRun()
    {
        RunState? state;
        try
        {
            state = RunManager.Instance?.DebugOnlyGetState();
        }
        catch (Exception ex)
        {
            SetInactive(null, "state-read-failed");
            Plugin.Logger.Warn($"[context] state read failed: {ex.Message}");
            return;
        }

        if (state is null)
        {
            SetInactive(null, "not-in-run");
            return;
        }

        lock (Gate)
        {
            if (ReferenceEquals(state, _cachedRun))
                return;

            _cachedRun = state;
            Evaluate(state);
        }
    }

    public static bool ShouldFilterUnlockState(UnlockState state)
    {
        if (!IsActive)
            return false;

        try
        {
            var run = _cachedRun;
            if (run is null)
                return false;
            if (ReferenceEquals(run.UnlockState, state))
                return true;
            foreach (var player in run.Players)
            {
                if (ReferenceEquals(player.UnlockState, state))
                    return true;
            }
        }
        catch
        {
            // Fall through to false.
        }

        return false;
    }

    public static void SetPendingFromLobby(object lobby)
    {
        try
        {
            var settings = ModSettings.Current;
            _pendingSet = true;
            _pendingSource = settings.Source;

            if (!settings.MasterEnabled)
            {
                SetPending(false, "master-off");
                return;
            }

            if (settings.Source == IsolationSource.Manual)
            {
                _pendingEffective = new HashSet<string>(settings.ManualModIds, StringComparer.Ordinal);
                if (_pendingEffective.Count == 0)
                {
                    SetPending(false, "manual-empty");
                    return;
                }
            }
            else
            {
                CharacterModRegistry.EnsureAuto();
                if (!CharacterModRegistry.HasAnyCharacterMod())
                {
                    SetPending(false, "no-character-mods");
                    return;
                }
            }

            var players = lobby.GetType().GetProperty("Players", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(lobby) as System.Collections.IEnumerable;
            if (players is null)
            {
                SetPending(false, "players-unavailable");
                return;
            }

            var count = 0;
            foreach (var player in players)
            {
                count++;
                var character = player?.GetType()
                    .GetField("character", BindingFlags.Public | BindingFlags.Instance)
                    ?.GetValue(player) as CharacterModel;

                if (character is null)
                {
                    SetPending(false, "character-unavailable");
                    return;
                }

                var name = character.GetType().Name;
                if (name is "RandomCharacter" or "RotatingCharacter")
                {
                    SetPending(false, "random-character");
                    return;
                }

                if (ContentOwnership.GetOwnerModId(character.GetType()) is not null)
                {
                    SetPending(false, "mod-character");
                    return;
                }
            }

            if (count != 1)
            {
                SetPending(false, count > 1 ? "multiplayer" : "no-players");
                return;
            }

            SetPending(true, "vanilla-run");
        }
        catch (Exception ex)
        {
            _pendingSet = true;
            SetPending(false, "lobby-read-failed");
            Plugin.Logger.Warn($"[context] lobby read failed: {ex.Message}");
        }
    }

    public static void ClearPending()
    {
        _pendingSet = false;
        _pendingActive = false;
        _pendingReason = string.Empty;
        _pendingEffective.Clear();
    }

    private static void Evaluate(RunState state)
    {
        var settings = ModSettings.Current;
        _source = settings.Source;

        if (!settings.MasterEnabled)
        {
            SetInactive(state, "master-off");
            return;
        }

        if (settings.Source == IsolationSource.Manual)
        {
            _effective = new HashSet<string>(settings.ManualModIds, StringComparer.Ordinal);
            if (_effective.Count == 0)
            {
                SetInactive(state, "manual-empty");
                return;
            }
        }
        else
        {
            CharacterModRegistry.EnsureAuto();
            if (!CharacterModRegistry.HasAnyCharacterMod())
            {
                SetInactive(state, "no-character-mods");
                return;
            }

            _effective = new HashSet<string>(
                ModSettings.Current.Source == IsolationSource.Auto
                    ? CharacterModRegistry.AutoCharacterModIds()
                    : [],
                StringComparer.Ordinal);
        }

        try
        {
            if (state.Players.Count != 1)
            {
                SetInactive(state, state.Players.Count > 1 ? "multiplayer" : "no-players");
                return;
            }

            foreach (var player in state.Players)
            {
                if (player.Character is null)
                {
                    SetInactive(state, "character-unavailable");
                    return;
                }

                var name = player.Character.GetType().Name;
                if (name is "RandomCharacter" or "RotatingCharacter")
                {
                    SetInactive(state, "random-character");
                    return;
                }

                if (ContentOwnership.GetOwnerModId(player.Character.GetType()) is not null)
                {
                    SetInactive(state, "mod-character");
                    return;
                }
            }

            _active = true;
            _reason = "vanilla-run";
        }
        catch (Exception ex)
        {
            SetInactive(state, "run-read-failed");
            Plugin.Logger.Warn($"[context] run read failed: {ex.Message}");
        }
    }

    private static void SetInactive(RunState? state, string reason)
    {
        _cachedRun = state;
        _active = false;
        _reason = reason;
        _effective.Clear();
        _source = ModSettings.Current.Source;
    }

    private static void SetPending(bool active, string reason)
    {
        _pendingActive = active;
        _pendingReason = reason;
        _source = _pendingSource;
    }
}
