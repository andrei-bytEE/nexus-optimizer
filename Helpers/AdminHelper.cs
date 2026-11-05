using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using NexusOptimizer.Services;

namespace NexusOptimizer.Helpers;

public static class AdminHelper
{
    public static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex)
        {
            LoggingService.Error("Admin check failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Starts a new elevated instance via the standard UAC prompt.
    /// Returns false if the user declines. The current instance should then keep running.
    /// </summary>
    public static bool RelaunchElevated()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return false;

            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas" });
            LoggingService.Info("Relaunched with elevation.");
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED: UAC declined
        {
            LoggingService.Info("Elevation declined by user.");
            return false;
        }
        catch (Exception ex)
        {
            LoggingService.Error("Elevation failed", ex);
            return false;
        }
    }
}