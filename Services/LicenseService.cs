using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace NexusOptimizer.Services;

/// <summary>Offline license check. A key is "payload.signature". The payload is "name|machineId", signed with a secret only the key generator knows.</summary>
public static class LicenseService
{
    // Must match the secret in tools\make-license.ps1. Changing it invalidates existing keys.
    private const string Secret = "YOUR-SECRET-HERE";

    private static readonly string LicenseFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexusOptimizer", "license.txt");

    public static bool IsPremium { get; private set; }
    public static string Licensee { get; private set; } = "";

    /// <summary>This PC's Windows machine ID. The customer sends it to you, and you put it in the key.</summary>
    public static string MachineId { get; } = ReadMachineId();

    private static string ReadMachineId()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            return key?.GetValue("MachineGuid") as string ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    public static void Load()
    {
        try
        {
            if (!File.Exists(LicenseFile)) return;
            IsPremium = TryValidate(File.ReadAllText(LicenseFile).Trim(), out var name);
            Licensee = name;
        }
        catch (Exception ex)
        {
            LoggingService.Error("Could not read license file", ex);
        }
    }

    public static bool TryActivate(string key, out string message)
    {
        if (!TryValidate((key ?? "").Trim(), out var name))
        {
            message = "That key is not valid for this PC. Check that you copied the whole key, and that it was made for this machine ID.";
            return false;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LicenseFile)!);
            File.WriteAllText(LicenseFile, key.Trim());
        }
        catch (Exception ex)
        {
            LoggingService.Error("Could not save license", ex);
            message = "The key is valid, but it could not be saved: " + ex.Message;
            return false;
        }

        IsPremium = true;
        Licensee = name;
        LoggingService.Info("License activated for " + name);
        message = $"Premium unlocked for {name}.";
        return true;
    }

    private static bool TryValidate(string key, out string name)
    {
        name = "";
        var parts = key.Split('.');
        if (parts.Length != 2) return false;

        try
        {
            var payload = FromB64U(parts[0]);
            var signature = FromB64U(parts[1]);

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
            var expected = hmac.ComputeHash(payload);
            if (!CryptographicOperations.FixedTimeEquals(expected, signature)) return false;

            var fields = Encoding.UTF8.GetString(payload).Split('|');
            if (fields.Length != 2) return false;
            if (!string.Equals(fields[1], MachineId, StringComparison.OrdinalIgnoreCase)) return false;

            name = fields[0];
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] FromB64U(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}