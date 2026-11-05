using System.Diagnostics;
using NexusOptimizer.Models;

namespace NexusOptimizer.Services;

/// <summary>
/// Removes preinstalled Store apps for the current user. Only packages on this fixed list
/// can be touched. Windows Store, Windows Security, Xbox Game Bar, and system components are never listed.
/// </summary>
public class DebloatService
{
    public static IReadOnlyList<DebloatItem> Catalog { get; } = new List<DebloatItem>
    {
        new("Microsoft.BingNews", "Microsoft News", "News app."),
        new("Microsoft.BingWeather", "Weather", "Weather app."),
        new("Microsoft.GetHelp", "Get Help", "Support app."),
        new("Microsoft.Getstarted", "Tips", "Windows tips app."),
        new("Microsoft.MicrosoftOfficeHub", "Office promo hub", "Shortcut app that promotes Microsoft 365."),
        new("Microsoft.MicrosoftSolitaireCollection", "Solitaire Collection", "Card games that show ads."),
        new("Microsoft.People", "People", "Contacts app."),
        new("Microsoft.WindowsFeedbackHub", "Feedback Hub", "Sends feedback to Microsoft."),
        new("Microsoft.ZuneMusic", "Media Player", "Music app."),
        new("Microsoft.ZuneVideo", "Movies & TV", "Video app."),
        new("Microsoft.YourPhone", "Phone Link", "Phone sync app."),
        new("Microsoft.MixedReality.Portal", "Mixed Reality Portal", "VR headset app."),
        new("Microsoft.SkypeApp", "Skype", "Skype app."),
        new("Microsoft.3DBuilder", "3D Builder", "3D modelling app."),
        new("Microsoft.Microsoft3DViewer", "3D Viewer", "3D viewer app."),
    };

    /// <summary>Returns the package names currently installed for this user.</summary>
    public HashSet<string> GetInstalledPackageNames()
    {
        var (exit, output, error) = RunPowerShell("Get-AppxPackage | Select-Object -ExpandProperty Name");
        if (exit != 0)
        {
            LoggingService.Error("Could not list installed apps: " + error);
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return output
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Removes one allowlisted app for the current user.</summary>
    public OperationResult Remove(DebloatItem item)
    {
        var (exit, _, error) = RunPowerShell($"Get-AppxPackage -Name '{item.PackageName}' | Remove-AppxPackage");
        if (exit == 0)
        {
            LoggingService.Info($"Removed app: {item.PackageName}");
            return OperationResult.Ok($"Removed {item.DisplayName}.");
        }

        LoggingService.Error($"Failed to remove {item.PackageName}: {error}");
        return OperationResult.Fail($"Could not remove {item.DisplayName}: {error.Trim()}");
    }

    private static (int ExitCode, string Output, string Error) RunPowerShell(string command)
    {
        var psi = new ProcessStartInfo("powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{command}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start PowerShell.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(120000))
        {
            process.Kill();
            return (-1, "", "Timed out.");
        }

        return (process.ExitCode, outputTask.Result, errorTask.Result);
    }
}