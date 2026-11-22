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
    private static string? _currentOwner;
    private static HashSet<string> _protected = new(StringComparer.Ordinal);
    private static HashSet<string> _isolated = new(StringComparer.Ordinal);
    private static int _policyVersion;

    private static bool _pendingSet;
    private static bool _pendingActive;
    private static string _pendingReason = string.Empty;
    private static IsolationSource _pendingSource;
    private static string? _pendingOwner;
    private static HashSet<string> _pendingProtected = new(StringComparer.Ordinal);
    private static HashSet<string> _pendingIsolated = new(StringComparer.Ordinal);

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
            if (_pendingSet)
                return _pendingReason;
            EnsureForCurrentRun();
            return _reason;
        }
    }

    public static IsolationSource Source
    {
        get
        {
            if (_pendingSet)
                return _pendingSource;
            EnsureForCurrentRun();
            return _source;
        }
    }

    public static string? CurrentCharacterOwner
    {
        get
        {
            EnsureForCurrentRun();
            return _currentOwner;
        }
    }

    public static IReadOnlySet<string> ProtectedModIds
    {
        get
        {
            EnsureForCurrentRun();
            return _protected;
        }
    }

    public static IReadOnlySet<string> IsolatedModIds
    {
        get
        {
            EnsureForCurrentRun();
            return _isolated;
        }
    }

    /// <summary>
    /// Compatibility name for the settings page. It is the isolated set, not the protected set.
    /// </summary>
    public static IReadOnlySet<string> EffectiveModIds => IsolatedModIds;

    public static int PolicyVersion
    {
        get
        {
            EnsureForCurrentRun();
            return _policyVersion;
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

    public static bool IsIsolated(string? owner)
    {
        if (owner is null)
            return false;
        if (_pendingSet)
            return _pendingIsolated.Contains(owner);
        return IsolatedModIds.Contains(owner);
    }

    public static bool IsProtected(string? owner)
    {
        if (owner is null)
            return false;
        if (_pendingSet)
            return _pendingProtected.Contains(owner);
        return ProtectedModIds.Contains(owner);
    }

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
            if (!TryBuildCandidates(out var source, out var candidates, out var inactiveReason))
            {
                SetPending(false, inactiveReason, source, null,
                    new HashSet<string>(StringComparer.Ordinal),
                    new HashSet<string>(StringComparer.Ordinal));
                return;
            }

            var players = lobby.GetType().GetProperty("Players", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(lobby) as System.Collections.IEnumerable;
            if (players is null)
            {
                SetPending(false, "players-unavailable", source, null,
                    new HashSet<string>(StringComparer.Ordinal),
                    new HashSet<string>(StringComparer.Ordinal));
                return;
            }

            var count = 0;
            CharacterModel? character = null;
            foreach (var player in players)
            {
                count++;
                character = player?.GetType()
                    .GetField("character", BindingFlags.Public | BindingFlags.Instance)
                    ?.GetValue(player) as CharacterModel;
            }

            if (count == 0)
            {
                SetPending(false, "no-players", source, null,
                    new HashSet<string>(StringComparer.Ordinal),
                    new HashSet<string>(StringComparer.Ordinal));
                return;
            }

            if (count != 1)
            {
                SetPending(false, "multiplayer", source, null,
                    new HashSet<string>(StringComparer.Ordinal),
                    new HashSet<string>(StringComparer.Ordinal));
                return;
            }

            if (character is null)
            {
                SetPending(false, "character-unavailable", source, null,
                    new HashSet<string>(StringComparer.Ordinal),
                    new HashSet<string>(StringComparer.Ordinal));
                return;
            }

            var name = character.GetType().Name;
            if (name is "RandomCharacter" or "RotatingCharacter")
            {
                SetPending(false, "random-character", source, null,
                    new HashSet<string>(StringComparer.Ordinal),
                    new HashSet<string>(StringComparer.Ordinal));
                return;
            }

            if (!TryResolveOwner(character, out var owner, out var ownerReason))
            {
                SetPending(false, ownerReason, source, null,
                    new HashSet<string>(StringComparer.Ordinal),
                    new HashSet<string>(StringComparer.Ordinal));
                return;
            }

            var protectedSet = owner is null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal) { owner };
            var isolatedSet = new HashSet<string>(
                candidates.Where(id => !string.Equals(id, owner, StringComparison.Ordinal)),
                StringComparer.Ordinal);
            SetPendingForOwner(source, owner, protectedSet, isolatedSet);
        }
        catch (Exception ex)
        {
            SetPending(false, "lobby-read-failed", ModSettings.Current.Source, null,
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal));
            Plugin.Logger.Warn($"[context] lobby read failed: {ex.Message}");
        }
    }

    public static void ClearPending()
    {
        _pendingSet = false;
        _pendingActive = false;
        _pendingReason = string.Empty;
        _pendingOwner = null;
        _pendingProtected.Clear();
        _pendingIsolated.Clear();
    }

    private static void SetPendingForOwner(
        IsolationSource source,
        string? owner,
        HashSet<string> protectedSet,
        HashSet<string> isolatedSet)
    {
        if (isolatedSet.Count == 0)
        {
            var reason = source == IsolationSource.Manual
                ? "manual-no-other"
                : "no-other-character-mods";
            SetPending(false, reason, source, owner, protectedSet, isolatedSet);
            return;
        }

        var activeReason = owner is null ? "vanilla-run" : "mod-character-other";
        SetPending(true, activeReason, source, owner, protectedSet, isolatedSet);
    }

    private static void Evaluate(RunState state)
    {
        if (!TryBuildCandidates(out var source, out var candidates, out var inactiveReason))
        {
            SetInactive(state, inactiveReason, source);
            return;
        }

        try
        {
            if (state.Players.Count != 1)
            {
                SetInactive(state, state.Players.Count > 1 ? "multiplayer" : "no-players", source);
                return;
            }

            var player = state.Players[0];
            if (player.Character is null)
            {
                SetInactive(state, "character-unavailable", source);
                return;
            }

            var name = player.Character.GetType().Name;
            if (name is "RandomCharacter" or "RotatingCharacter")
            {
                SetInactive(state, "random-character", source);
                return;
            }

            if (!TryResolveOwner(player.Character, out var owner, out var ownerReason))
            {
                SetInactive(state, ownerReason, source);
                return;
            }

            var protectedSet = owner is null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal) { owner };
            var isolatedSet = new HashSet<string>(
                candidates.Where(id => !string.Equals(id, owner, StringComparison.Ordinal)),
                StringComparer.Ordinal);

            if (isolatedSet.Count == 0)
            {
                var reason = source == IsolationSource.Manual
                    ? "manual-no-other"
                    : "no-other-character-mods";
                SetState(state, false, reason, source, owner, protectedSet, isolatedSet);
                return;
            }

            var activeReason = owner is null ? "vanilla-run" : "mod-character-other";
            SetState(state, true, activeReason, source, owner, protectedSet, isolatedSet);
        }
        catch (Exception ex)
        {
            SetInactive(state, "run-read-failed", source);
            Plugin.Logger.Warn($"[context] run read failed: {ex.Message}");
        }
    }

    private static bool TryBuildCandidates(
        out IsolationSource source,
        out HashSet<string> candidates,
        out string inactiveReason)
    {
        var settings = ModSettings.Current;
        source = settings.Source;
        candidates = new HashSet<string>(StringComparer.Ordinal);
        inactiveReason = string.Empty;

        if (!settings.MasterEnabled)
        {
            inactiveReason = "master-off";
            return false;
        }

        if (settings.Source == IsolationSource.Manual)
        {
            candidates.UnionWith(settings.ManualModIds.Where(CharacterModRegistry.IsModEnabled));
            if (candidates.Count == 0)
            {
                inactiveReason = "manual-empty";
                return false;
            }

            return true;
        }

        CharacterModRegistry.EnsureAuto();
        candidates.UnionWith(
            CharacterModRegistry.AutoCharacterModIds().Where(CharacterModRegistry.IsModEnabled));
        if (candidates.Count == 0)
        {
            inactiveReason = "no-character-mods";
            return false;
        }

        return true;
    }

    private static bool TryResolveOwner(
        CharacterModel character,
        out string? owner,
        out string reason)
    {
        owner = null;
        reason = string.Empty;
        try
        {
            var type = character.GetType();
            var origin = ContentOwnership.GetOrigin(type);
            if (origin == ContentOrigin.Vanilla)
                return true;

            if (origin != ContentOrigin.Mod)
            {
                reason = "character-owner-unknown";
                return false;
            }

            owner = ContentOwnership.GetOwnerModId(type);
            if (string.IsNullOrWhiteSpace(owner))
            {
                reason = "character-owner-unknown";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            reason = "character-owner-read-failed";
            Plugin.Logger.Warn($"[context] character owner read failed: {ex.Message}");
            return false;
        }
    }

    private static void SetInactive(
        RunState? state,
        string reason,
        IsolationSource? source = null)
    {
        SetState(
            state,
            false,
            reason,
            source ?? ModSettings.Current.Source,
            null,
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal));
    }

    private static void SetState(
        RunState? state,
        bool active,
        string reason,
        IsolationSource source,
        string? owner,
        HashSet<string> protectedSet,
        HashSet<string> isolatedSet)
    {
        var changed = !ReferenceEquals(_cachedRun, state) ||
                      _active != active ||
                      !string.Equals(_reason, reason, StringComparison.Ordinal) ||
                      _source != source ||
                      !string.Equals(_currentOwner, owner, StringComparison.Ordinal) ||
                      !_protected.SetEquals(protectedSet) ||
                      !_isolated.SetEquals(isolatedSet);

        _cachedRun = state;
        _active = active;
        _reason = reason;
        _source = source;
        _currentOwner = owner;
        _protected = protectedSet;
        _isolated = isolatedSet;
        _pendingSet = false;
        _pendingActive = false;
        _pendingReason = string.Empty;
        _pendingOwner = null;
        _pendingProtected.Clear();
        _pendingIsolated.Clear();

        if (!changed)
            return;

        unchecked
        {
            _policyVersion++;
        }

        Plugin.Logger.Info(
            $"[context] active={active} reason={reason} source={source} " +
            $"characterOwner={owner ?? "vanilla"} " +
            $"protected=[{string.Join(",", protectedSet.OrderBy(static id => id, StringComparer.Ordinal))}] " +
            $"isolated=[{string.Join(",", isolatedSet.OrderBy(static id => id, StringComparer.Ordinal))}]");
    }

    private static void SetPending(
        bool active,
        string reason,
        IsolationSource source,
        string? owner,
        HashSet<string> protectedSet,
        HashSet<string> isolatedSet)
    {
        _pendingSet = true;
        _pendingActive = active;
        _pendingReason = reason;
        _pendingSource = source;
        _pendingOwner = owner;
        _pendingProtected = protectedSet;
        _pendingIsolated = isolatedSet;
        unchecked
        {
            _policyVersion++;
        }

        Plugin.Logger.Info(
            $"[context] pending active={active} reason={reason} source={source} " +
            $"characterOwner={owner ?? "vanilla"} " +
            $"protected=[{string.Join(",", protectedSet.OrderBy(static id => id, StringComparer.Ordinal))}] " +
            $"isolated=[{string.Join(",", isolatedSet.OrderBy(static id => id, StringComparer.Ordinal))}]");
    }
}
