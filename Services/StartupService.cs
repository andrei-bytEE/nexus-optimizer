using Microsoft.Win32;
using NexusOptimizer.Models;

namespace NexusOptimizer.Services;

/// <summary>Lists programs that start with Windows, with their on/off state from StartupApproved.</summary>
public static class StartupService
{
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    public const string DisabledPrefix = "Startup: ";

    public static List<StartupEntry> List()
    {
        var list = new List<StartupEntry>();

        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var runKey = root.OpenSubKey(RunKey, false);
                if (runKey == null) continue;
                using var approvedKey = root.OpenSubKey(ApprovedKey, false);

                foreach (var name in runKey.GetValueNames())
                {
                    // Bit 0 set means Windows has this program turned off. No value means on.
                    var state = approvedKey?.GetValue(name) as byte[];
                    var enabled = state == null || state.Length == 0 || (state[0] & 1) == 0;

                    list.Add(new StartupEntry
                    {
                        Name = name,
                        Command = runKey.GetValue(name) as string ?? "",
                        Hive = hive,
                        IsEnabled = enabled
                    });
                }
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Could not read startup list for {hive}", ex);
            }
        }

        return list.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}