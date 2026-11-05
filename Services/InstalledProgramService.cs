using Microsoft.Win32;
using NexusOptimizer.Models;

namespace NexusOptimizer.Services;

/// <summary>Reads installed desktop programs from the Windows uninstall registry.</summary>
public static class InstalledProgramService
{
    private static readonly string[] UninstallRoots =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    };

    public static List<InstalledProgram> GetPrograms()
    {
        var results = new Dictionary<string, InstalledProgram>(StringComparer.OrdinalIgnoreCase);

        foreach (var (hive, root) in new[]
        {
            (RegistryHive.LocalMachine, UninstallRoots[0]),
            (RegistryHive.LocalMachine, UninstallRoots[1]),
            (RegistryHive.CurrentUser, UninstallRoots[0])
        })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64).OpenSubKey(root, false);
                if (baseKey == null) continue;

                foreach (var keyName in baseKey.GetSubKeyNames())
                {
                    using var key = baseKey.OpenSubKey(keyName, false);
                    var program = Read(key, keyName);
                    if (program != null && !results.ContainsKey(program.DisplayName))
                        results[program.DisplayName] = program;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Could not read uninstall list at {hive}\\{root}", ex);
            }
        }

        return results.Values.OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Returns null for entries that aren't real programs (updates, components, folders).</summary>
    private static InstalledProgram Read(RegistryKey key, string keyName)
    {
        if (key == null) return null;

        string Get(string name) => key.GetValue(name) as string ?? "";
        int GetInt(string name) => key.GetValue(name) is int i ? i : 0;

        var name = Get("DisplayName");
        var uninstall = Get("UninstallString");

        if (string.IsNullOrWhiteSpace(name)) return null;
        if (string.IsNullOrWhiteSpace(uninstall)) return null;
        if (GetInt("SystemComponent") == 1) return null;
        if (GetInt("NoRemove") == 1) return null;
        if (!string.IsNullOrEmpty(Get("ParentKeyName"))) return null;
        if (keyName.StartsWith("KB", StringComparison.OrdinalIgnoreCase)) return null;
        if (Get("ReleaseType").Contains("Update", StringComparison.OrdinalIgnoreCase)) return null;

        return new InstalledProgram
        {
            DisplayName = name.Trim(),
            Publisher = Get("Publisher").Trim(),
            Version = Get("DisplayVersion").Trim(),
            UninstallCommand = uninstall.Trim(),
            RegistryKeyName = keyName
        };
    }

    /// <summary>Runs the program's own uninstaller. Windows and the program handle the removal.</summary>
    public static OperationResult Uninstall(InstalledProgram program)
    {
        try
        {
            var cmd = program.UninstallCommand;
            string file, args;

            if (cmd.StartsWith("\""))
            {
                var end = cmd.IndexOf('"', 1);
                file = end > 0 ? cmd[1..end] : cmd.Trim('"');
                args = end > 0 && end + 1 < cmd.Length ? cmd[(end + 1)..].Trim() : "";
            }
            else
            {
                var space = cmd.IndexOf(' ');
                file = space > 0 ? cmd[..space] : cmd;
                args = space > 0 ? cmd[(space + 1)..] : "";
            }

            using var process = System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(file, args) { UseShellExecute = true });
            process?.WaitForExit();

            LoggingService.Info($"Ran uninstaller for {program.DisplayName}");
            return OperationResult.Ok($"Finished uninstalling {program.DisplayName}.");
        }
        catch (Exception ex)
        {
            LoggingService.Error($"Uninstall failed for {program.DisplayName}", ex);
            return OperationResult.Fail($"Could not start the uninstaller for {program.DisplayName}: {ex.Message}");
        }
    }
}