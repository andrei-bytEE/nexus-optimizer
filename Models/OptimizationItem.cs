using System.Windows.Media;
using Microsoft.Win32;

namespace NexusOptimizer.Models;

public class OptimizationItem
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Risk { get; init; } = "Low";

    public RegistryHive Hive { get; init; }
    public string SubKey { get; init; } = "";
    public string ValueName { get; init; } = "";
    public RegistryValueKind Kind { get; init; }

    /// <summary>The value that turns this optimization ON.</summary>
    public string AppliedValue { get; init; } = "";

    /// <summary>The Windows default value, used to turn it OFF when no backup exists.</summary>
    public string OffValue { get; init; } = "";

    /// <summary>Set by the view model after reading the registry.</summary>
    public bool IsApplied { get; set; }

    public string StatusText => IsApplied ? "Applied" : "Not applied";
    public string ButtonText => IsApplied ? "Applied" : "Apply";

    public Brush ButtonBrush => IsApplied
        ? new SolidColorBrush(Color.FromRgb(0x2E, 0xA0, 0x43))   // green
        : new SolidColorBrush(Color.FromRgb(0xDA, 0x36, 0x33));  // red
}