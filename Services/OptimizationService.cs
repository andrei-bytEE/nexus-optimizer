using Microsoft.Win32;
using NexusOptimizer.Models;

namespace NexusOptimizer.Services;

/// <summary>Reads and applies individual optimizations through the backup service.</summary>
public class OptimizationService
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string ContentKey = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
    private const string GameBarKey = @"Software\Microsoft\GameBar";
    private const string GameDvrKey = @"Software\Microsoft\Windows\CurrentVersion\GameDVR";
    private const string GameConfigKey = @"System\GameConfigStore";
    private const string BackgroundAppsKey = @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications";
    private const string TelemetryPolicyKey = @"SOFTWARE\Policies\Microsoft\Windows\DataCollection";
    private const string GraphicsKey = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";

    private readonly RegistryBackupService _backups;

    public OptimizationService(RegistryBackupService backups)
    {
        _backups = backups;
    }

    /// <summary>The optimizations offered in this build. OffValue is the Windows default.</summary>
    public static IReadOnlyList<OptimizationItem> Catalog { get; } = new List<OptimizationItem>
    {
        Dw("Disable advertising ID",
           "Stops apps from using your advertising ID for personalized ads.",
           "Low", RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", "0", "1"),

        Dw("Turn off transparency effects",
           "Removes the translucent blur on taskbar and menus. Reduces GPU work on busy screens.",
           "Low", RegistryHive.CurrentUser, PersonalizeKey, "EnableTransparency", "0", "1"),

        Dw("Turn off Start menu suggestions",
           "Stops promoted and suggested apps from appearing in the Start menu.",
           "Low", RegistryHive.CurrentUser, ContentKey, "SubscribedContent-338388Enabled", "0", "1"),

        Dw("Turn off Windows tips and suggestions",
           "Stops tips, tricks, and suggested content from appearing in Windows.",
           "Low", RegistryHive.CurrentUser, ContentKey, "SubscribedContent-338389Enabled", "0", "1"),

        Dw("Turn off silently installed app suggestions",
           "Stops Windows from quietly installing promoted apps for your account.",
           "Low", RegistryHive.CurrentUser, ContentKey, "SilentInstalledAppsEnabled", "0", "1"),

        Dw("Turn on Windows Game Mode",
           "Tells Windows to prioritize the active game for CPU and GPU time. Does not guarantee FPS gains.",
           "Low", RegistryHive.CurrentUser, GameBarKey, "AutoGameModeEnabled", "1", "0"),

        Dw("Turn off Game DVR background recording",
           "Stops Windows from recording gameplay in the background. The Game Bar overlay still works.",
           "Low", RegistryHive.CurrentUser, GameConfigKey, "GameDVR_Enabled", "0", "1"),

        Dw("Turn off Game Bar capture",
           "Disables the Game Bar capture feature so it does not record in the background.",
           "Low", RegistryHive.CurrentUser, GameDvrKey, "AppCaptureEnabled", "0", "1"),

        Dw("Stop background apps from running",
           "Stops Microsoft Store apps from running in the background when they are closed. Open apps are not affected.",
           "Low", RegistryHive.CurrentUser, BackgroundAppsKey, "GlobalUserDisabled", "1", "0"),

        Dw("Limit telemetry to Basic (requires administrator)",
           "Sets Windows diagnostic data to the Basic level. Security updates are not affected. " +
           "Some Home editions may ignore this policy.",
           "Medium", RegistryHive.LocalMachine, TelemetryPolicyKey, "AllowTelemetry", "1", "3"),

        Dw("Enable hardware-accelerated GPU scheduling (requires administrator and restart)",
           "Lets the GPU manage its own memory scheduling. Only works on supported GPUs and drivers. " +
           "Check the Applied status after a restart.",
           "Medium", RegistryHive.LocalMachine, GraphicsKey, "HwSchMode", "2", "1"),

        Dw("Turn off Windows Copilot",
           "Disables the Copilot sidebar and its background activity via Windows policy.",
           "Low", RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", "1", "0"),

        Dw("Turn off Bing results in Start menu search",
           "Stops Start menu searches from sending queries to Bing, so searches stay local.",
           "Low", RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", "0", "1"),

        Dw("Turn off Start menu recommendations",
           "Hides recommended files and apps in the Start menu.",
           "Low", RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_IrisRecommendations", "0", "1"),

        Dw("Disable consumer app promotions (requires administrator)",
           "Stops Windows from automatically installing promoted apps and showing sponsored content.",
           "Medium", RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", "1", "0"),

        Dw("Turn on best-performance visual effects",
           "Turns off window animations and shadows in Windows. Looks plainer, but uses less GPU and CPU work when switching windows.",
           "Low", RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", "2", "0"),

        Dw("Turn off Windows Spotlight tips on lock screen",
           "Stops rotating ads, tips, and promotions from appearing on the lock screen.",
           "Low", RegistryHive.CurrentUser, ContentKey, "RotatingLockScreenEnabled", "0", "1"),

        Dw("Turn off Edge startup boost (requires administrator)",
           "Stops Microsoft Edge from loading in the background when Windows starts, which frees memory and startup time.",
           "Medium", RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Edge", "StartupBoostEnabled", "0", "1"),

        Dw("Turn off Edge background running (requires administrator)",
           "Stops Edge from keeping processes running after you close its window.",
           "Medium", RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Edge", "BackgroundModeEnabled", "0", "1"),

        Dw("Turn off power throttling (requires administrator)",
           "Stops Windows from slowing background processes to save power. Helps on desktops, but uses more power on laptops.",
           "Medium", RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", "1", "0"),

        Dw("Turn off Cortana in search (requires administrator)",
           "Disables the Cortana assistant in Windows Search. Normal file search still works.",
           "Medium", RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", "0", "1"),

        Dw("Turn off Windows Error Reporting (requires administrator)",
           "Stops Windows from sending crash reports to Microsoft in the background. Crash dialogs still appear.",
           "Medium", RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", "1", "0"),

    };

    private static OptimizationItem Dw(string name, string description, string risk,
                                       RegistryHive hive, string subKey, string valueName,
                                       string appliedValue, string offValue) =>
        new()
        {
            Name = name,
            Description = description,
            Risk = risk,
            Hive = hive,
            SubKey = subKey,
            ValueName = valueName,
            Kind = RegistryValueKind.DWord,
            AppliedValue = appliedValue,
            OffValue = offValue
        };

    /// <summary>Reads the live registry value and reports whether the optimization is applied.</summary>
    public bool IsApplied(OptimizationItem item)
    {
        try
        {
            using var root = RegistryKey.OpenBaseKey(item.Hive, RegistryView.Registry64);
            using var key = root.OpenSubKey(item.SubKey, false);
            var value = key?.GetValue(item.ValueName);
            return value != null && value.ToString() == item.AppliedValue;
        }
        catch (Exception ex)
        {
            LoggingService.Error($"Could not read state for {item.Name}", ex);
            return false;
        }
    }

        public OperationResult Apply(OptimizationItem item)
    {
        var result = _backups.SetRegistryValue(item.Hive, item.SubKey, item.ValueName,
                                               item.Kind, item.AppliedValue, item.Name);
        if (!result.Success) return result;

        // Read the value back to confirm Windows stored what we intended.
        if (!IsApplied(item))
            return OperationResult.Fail($"{item.Name}: the value was written, but reading it back did not match.");

        return result;
    }

    /// <summary>
    /// Turns an applied optimization off. If the app has a backup, the original value is restored.
    /// If the setting was applied outside the app, it is set to the Windows default instead.
    /// Either way the change goes through the backup service, so it stays undoable.
    /// </summary>
    public OperationResult Revert(OptimizationItem item)
    {
        var entry = _backups.History
            .Where(e => !e.Restored
                     && e.Hive == item.Hive.ToString()
                     && e.SubKey == item.SubKey
                     && e.ValueName == item.ValueName)
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefault();

        if (entry != null)
            return _backups.RestoreEntry(entry.Id);

        var result = _backups.SetRegistryValue(item.Hive, item.SubKey, item.ValueName,
                                               item.Kind, item.OffValue, item.Name + " (set to Windows default)");
        return result.Success
            ? OperationResult.Ok($"Turned off '{item.Name}' by setting it to the Windows default.")
            : result;
    }
    /// <summary>Reverts every optimization that is currently applied. Returns a summary message.</summary>
    public (int Reverted, int Failed, List<string> Errors) RevertAll()
    {
        int reverted = 0, failed = 0;
        var errors = new List<string>();

        foreach (var item in Catalog)
        {
            if (!IsApplied(item)) continue;

            var result = Revert(item);
            if (result.Success) reverted++;
            else
            {
                failed++;
                errors.Add($"{item.Name}: {result.Message}");
            }
        }
        return (reverted, failed, errors);
    }
    /// <summary>Applies every optimization that is not yet applied. Low-risk only when lowRiskOnly is true.</summary>
    public (int Applied, int Skipped, int Failed, List<string> Errors) ApplyMany(bool lowRiskOnly)
    {
        int applied = 0, skipped = 0, failed = 0;
        var errors = new List<string>();

        foreach (var item in Catalog)
        {
            if (lowRiskOnly && item.Risk != "Low") { skipped++; continue; }
            if (IsApplied(item)) { skipped++; continue; }

            var result = Apply(item);
            if (result.Success) applied++;
            else
            {
                failed++;
                errors.Add($"{item.Name}: {result.Message}");
            }
        }
        return (applied, skipped, failed, errors);
    }
    public OperationResult Undo(Guid backupId) => _backups.RestoreEntry(backupId);
}