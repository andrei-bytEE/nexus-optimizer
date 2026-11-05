using System.Diagnostics;
using System.IO;
using NexusOptimizer.Models;

namespace NexusOptimizer.Services;

/// <summary>Reverts applied changes, then removes the app's own data folder and optionally its executable.</summary>
public static class UninstallService
{
    public static string DataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexusOptimizer");

    public static int PendingChangeCount(RegistryBackupService backups) =>
        backups.History.Count(e => !e.Restored);

    /// <summary>Reverts every unreverted change, newest first. Stops reporting failure if any step fails.</summary>
    public static OperationResult RevertAll(RegistryBackupService backups)
    {
        var pending = backups.History
            .Where(e => !e.Restored)
            .OrderByDescending(e => e.Timestamp)
            .ToList();

        var failures = new List<string>();
        foreach (var entry in pending)
        {
            var result = backups.RestoreEntry(entry.Id);
            if (!result.Success)
                failures.Add($"{entry.Description}: {result.Message}");
        }

        if (failures.Count > 0)
            return OperationResult.Fail(
                "Some changes could not be reverted, so nothing was deleted. " +
                "Fix the problem (for example, run as administrator) and try again:\n\n" +
                string.Join("\n", failures));

        return OperationResult.Ok($"Reverted {pending.Count} change(s).");
    }

    public static OperationResult DeleteAppData()
    {
        try
        {
            if (Directory.Exists(DataDir))
                Directory.Delete(DataDir, true);
            return OperationResult.Ok("Deleted logs and backup data.");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail("Could not delete " + DataDir + ": " + ex.Message);
        }
    }

    /// <summary>The running .exe can't delete itself, so a short delayed command removes it after the app closes.</summary>
    public static void ScheduleExeDeletion()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe) || !exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return;

        var psi = new ProcessStartInfo("cmd.exe",
            $"/c ping 127.0.0.1 -n 4 >nul & del /f /q \"{exe}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        Process.Start(psi);
    }
}