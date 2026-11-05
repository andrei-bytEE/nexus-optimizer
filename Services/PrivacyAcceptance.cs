using System.IO;

namespace NexusOptimizer.Services;

/// <summary>Remembers that the user accepted the privacy notice, so it shows only once.</summary>
public static class PrivacyAcceptance
{
    private static readonly string FlagFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexusOptimizer", "privacy-accepted.txt");

    public static bool IsAccepted => File.Exists(FlagFile);

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FlagFile)!);
            File.WriteAllText(FlagFile, DateTime.UtcNow.ToString("O"));
        }
        catch (Exception ex)
        {
            LoggingService.Error("Could not save privacy acceptance", ex);
        }
    }
}