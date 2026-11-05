using System.Diagnostics;
using System.Text.RegularExpressions;
using NexusOptimizer.Models;

namespace NexusOptimizer.Services;

/// <summary>Switches the active Windows power plan through powercfg, with a backup of the previous plan.</summary>
public static class PowerPlanService
{
    public const string HiveName = "PowerPlan";
    public const string ValueName = "ActiveScheme";
    public const string HighPerformanceName = "High performance";

    private static readonly Regex GuidPattern = new(
        @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})");

    public static string GetActiveGuid()
    {
        var output = Run("/getactivescheme");
        var m = GuidPattern.Match(output ?? "");
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>Finds a plan's GUID by its display name. Returns null if this PC doesn't have that plan.</summary>
    public static string FindGuidByName(string name)
    {
        var output = Run("/list") ?? "";
        foreach (var line in output.Split('\n'))
        {
            if (line.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0) continue;
            var m = GuidPattern.Match(line);
            if (m.Success) return m.Groups[1].Value;
        }
        return null;
    }

    public static bool SetActive(string guid) => Run("/setactive " + guid) != null;

    public static OperationResult ActivateHighPerformance(RegistryBackupService backups)
    {
        var previous = GetActiveGuid();
        if (previous == null)
            return OperationResult.Fail("Could not read the current power plan.");

        var target = FindGuidByName(HighPerformanceName);
        if (target == null)
            return OperationResult.Fail("The High performance plan is not available on this PC.");

        if (string.Equals(previous, target, StringComparison.OrdinalIgnoreCase))
            return OperationResult.Ok("High performance is already the active plan.");

        var entry = new BackupEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Description = "Power plan: High performance",
            Hive = HiveName,
            SubKey = "",
            ValueName = ValueName,
            OriginalExisted = true,
            OriginalKind = "String",
            OriginalValue = previous,
            NewKind = "String",
            NewValue = target
        };

        backups.AddPendingEntry(entry);

        if (!SetActive(target))
        {
            backups.RemovePendingEntry(entry);
            LoggingService.Error("Could not switch to High performance power plan.");
            return OperationResult.Fail("Windows did not switch the power plan.");
        }

        LoggingService.Info($"Power plan switched from {previous} to {target}");
        return OperationResult.Ok("High performance plan is now active.");
    }

    private static string Run(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo("powercfg.exe", arguments)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process == null) return null;

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(10000)) { process.Kill(); return null; }
            return process.ExitCode == 0 ? output : null;
        }
        catch (Exception ex)
        {
            LoggingService.Error("powercfg failed: " + arguments, ex);
            return null;
        }
    }
}