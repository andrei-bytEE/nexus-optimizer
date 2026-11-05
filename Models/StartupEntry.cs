using Microsoft.Win32;

namespace NexusOptimizer.Models;

public class StartupEntry
{
    public string Name { get; init; } = "";
    public string Command { get; init; } = "";
    public RegistryHive Hive { get; init; }

    /// <summary>True when Windows Settings shows this program as On.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Drives the switch color: green when disabled, red when enabled.</summary>
    public bool IsDisabledState => !IsEnabled;

    public string Scope => Hive == RegistryHive.CurrentUser ? "This user" : "All users (admin)";
    public string StateText => IsEnabled ? "On at startup" : "Off in Windows";
    public string ToggleText => IsEnabled ? "Disable" : "Enable";
}