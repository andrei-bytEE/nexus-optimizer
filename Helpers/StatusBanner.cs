namespace NexusOptimizer.Helpers;

/// <summary>Picks a banner color from a status message. Reads text only, so no view model changes are needed.</summary>
public static class StatusBanner
{
    private static readonly string[] ErrorWords =
        { "failed", "could not", "not valid", "access denied", "error", "stopped" };

    private static readonly string[] WarningWords =
        { "cancelled", "skipped", "not available", "requires", "needs", "restart", "already", "nothing", "warning" };

    public static string Kind(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "Neutral";

        var text = message.ToLowerInvariant();
        if (ErrorWords.Any(w => text.Contains(w))) return "Error";
        if (WarningWords.Any(w => text.Contains(w))) return "Warning";
        if (text.Contains("applied") || text.Contains("reverted") || text.Contains("restored")
            || text.Contains("removed") || text.Contains("added") || text.Contains("unlocked")
            || text.Contains("installed") || text.Contains("active") || text.Contains("success"))
            return "Success";
        return "Neutral";
    }
}