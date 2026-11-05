using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NexusOptimizer.Services;

public class UpdateInfo
{
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("signature")] public string Signature { get; set; } = "";
    [JsonPropertyName("notes")] public string Notes { get; set; } = "";
}

/// <summary>Checks GitHub Releases for a newer version, verifies its signature, and replaces the running exe.</summary>
public static class UpdateService
{
        public static string CurrentVersion =>
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version is { } v
            ? $"{v.Major}.{v.Minor}.{v.Build}"
            : "0.0.0";

    // Public key only. Safe to ship. Its matching private key never leaves your PC.
    private const string PublicKeyXml = "<RSAKeyValue><Modulus>m7X2v0yWeu/ApB1mgmgsz5aEU3oNiI2kFciTr1Z45OXpYPd0ZcS80zsbwQ7WzQhYKSYcFI6na9GCbB0lGFlDTSsYwX6ZJyBTr+PuMxoDaIxHLwEX0IF5WF1JCSc00rQQRqMXzVZ1SXz3OWPlORaxC5eS0pJMbOP5tq1sXTJ8p8Epu635zE4pKFYd6XyQxnjKCnfMuEJ2dHjN9v1gZVAqLl1lTToy2mrhIvwXVCiCY2jBakrtlaGrvcAJ2zQn9o0uG3ZhW+fAXddYWXOkIKmJfqvfuVDQlM93ejBDArsBG3fL4n2xBAeO9B5ddIci1+TjGbRkWXSAtj6JIaUcKyXBKQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

    private const string VersionUrl =
        "https://github.com/andrei-bytEE/nexus-optimizer/releases/latest/download/version.json";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    public static async Task<UpdateInfo> CheckAsync()
    {
        try
        {
            var json = await Http.GetStringAsync(VersionUrl);
            var info = JsonSerializer.Deserialize<UpdateInfo>(json);
            if (info == null || string.IsNullOrEmpty(info.Version))
            {
                LoggingService.Info("Update check: release had no version.");
                return null;
            }

            var newer = IsNewer(info.Version, CurrentVersion);
            LoggingService.Info($"Update check: latest={info.Version} current={CurrentVersion} newer={newer}");
            return newer ? info : null;
        }
        catch (Exception ex)
        {
            LoggingService.Error("Update check failed", ex);
            return null;
        }
    }

    public static async Task<(bool Ok, string Message)> InstallAsync(UpdateInfo info)
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return (false, "Could not find the app file.");

        var dir = Path.GetDirectoryName(exe)!;
        var newFile = Path.Combine(dir, "NEXUS-OPTIMIZER.new");
        var oldFile = Path.Combine(dir, "NEXUS-OPTIMIZER.old");

        try
        {
            var bytes = await Http.GetByteArrayAsync(info.Url);

            using var rsa = RSA.Create();
            rsa.FromXmlString(PublicKeyXml);
            var sig = Convert.FromBase64String(info.Signature);
            if (!rsa.VerifyData(bytes, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                return (false, "The update failed its signature check and was not installed.");

            await File.WriteAllBytesAsync(newFile, bytes);
            if (File.Exists(oldFile)) File.Delete(oldFile);
            File.Move(exe, oldFile);
            File.Move(newFile, exe);

            LoggingService.Info($"Updated to {info.Version}");
            return (true, "Update installed. The app will now restart.");
        }
        catch (Exception ex)
        {
            LoggingService.Error("Update install failed", ex);
            if (!File.Exists(exe) && File.Exists(oldFile)) File.Move(oldFile, exe);
            return (false, "The update could not be installed: " + ex.Message);
        }
    }

    public static void Restart()
    {
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exe))
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
    }

    private static bool IsNewer(string latest, string current)
    {
        return Version.TryParse(latest, out var l) && Version.TryParse(current, out var c) && l > c;
    }
}