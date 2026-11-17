namespace CharacterModIsolation;

internal static class FilterLog
{
    private static readonly HashSet<string> Logged = new(StringComparer.Ordinal);

    public static void Removed(string category, int removed, int kept)
    {
        if (removed <= 0)
            return;

        var key = category + ":" + RunContext.Reason;
        var verbose = ModSettings.Current.VerboseLog;
        if (!verbose && !Logged.Add(key))
            return;

        Plugin.Logger.Info($"[filter] category={category} removed={removed} kept={kept} reason={RunContext.Reason}");
    }

    public static void Skipped(string category, string reason)
    {
        if (ModSettings.Current.VerboseLog)
            Plugin.Logger.Info($"[filter] category={category} skipped={reason}");
    }
}
