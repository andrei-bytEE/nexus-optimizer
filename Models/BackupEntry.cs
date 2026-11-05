namespace NexusOptimizer.Models;

/// <summary>One reversible registry change, with the value it replaced.</summary>
public class BackupEntry
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }          // UTC
    public string Description { get; set; } = "";

    public string Hive { get; set; } = "";           // "CurrentUser" or "LocalMachine"
    public string SubKey { get; set; } = "";
    public string ValueName { get; set; } = "";

    public bool OriginalExisted { get; set; }        // false = value did not exist before
    public string OriginalKind { get; set; }         // RegistryValueKind name
    public string OriginalValue { get; set; }

    public string NewKind { get; set; } = "";
    public string NewValue { get; set; } = "";

    public bool Restored { get; set; }
    public DateTime? RestoredAt { get; set; }
}