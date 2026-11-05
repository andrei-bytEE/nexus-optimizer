using System.IO;

namespace NexusOptimizer.Services;

/// <summary>Local-only file log. Nothing is sent anywhere.</summary>
public static class LoggingService
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexusOptimizer", "logs");

    private static readonly object Gate = new();

    static LoggingService()
    {
        try { Directory.CreateDirectory(LogDir); } catch { /* logging must never crash the app */ }
    }

    public static string LogFile => Path.Combine(LogDir, "nexus-optimizer.log");

    public static void Info(string message) => Write("INFO", message, null);

    public static void Error(string message, Exception ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception ex)
    {
        try
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}"
                     + (ex != null ? $" :: {ex}" : "")
                     + Environment.NewLine;
            lock (Gate) File.AppendAllText(LogFile, line);
        }
        catch { /* swallow: logging failures must not cascade */ }
    }
}