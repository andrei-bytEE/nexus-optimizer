using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using NexusOptimizer.Models;

namespace NexusOptimizer.Services;

/// <summary>
/// Captures the original value of a registry setting before changing it,
/// persists that backup to disk, and restores it on request.
/// Order of operations: backup is saved first, then the change is written.
/// If the write fails, the pending backup is removed, so history only reflects real changes.
/// </summary>
public class RegistryBackupService
{
    private static readonly string BackupDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexusOptimizer", "backups");

    private static readonly string HistoryFile = Path.Combine(BackupDir, "history.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly object _gate = new();
    private List<BackupEntry> _history = new();

    public RegistryBackupService()
    {
        try { Directory.CreateDirectory(BackupDir); }
        catch (Exception ex) { LoggingService.Error("Cannot create backup directory", ex); }
        Load();
    }

    public IReadOnlyList<BackupEntry> History
    {
        get { lock (_gate) return _history.ToList(); }
    }

    /// <summary>Backs up, then sets a DWORD or String registry value.</summary>
    public OperationResult SetRegistryValue(RegistryHive hive, string subKey, string valueName,
        RegistryValueKind kind, string newValue, string description)
    {
        if (!TryParse(kind, newValue, out var typed, out var parseError))
            return OperationResult.Fail(parseError);

        try
        {
            using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);

            // 1. Capture the original value before anything changes.
            var entry = new BackupEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                Description = description,
                Hive = hive.ToString(),
                SubKey = subKey,
                ValueName = valueName,
                NewKind = kind.ToString(),
                NewValue = newValue
            };

            using (var readKey = root.OpenSubKey(subKey, false))
            {
                var existing = readKey?.GetValue(valueName, null);
                entry.OriginalExisted = existing != null;
                if (existing != null)
                {
                    entry.OriginalKind = readKey.GetValueKind(valueName).ToString();
                    entry.OriginalValue = Convert.ToString(existing, CultureInfo.InvariantCulture);
                }
            }

            // 2. Persist the pending backup before touching the system.
            lock (_gate)
            {
                _history.Add(entry);
                SaveUnlocked();
            }

            // 3. Apply the change. If it fails, roll back the pending backup record.
            try
            {
                using var writeKey = root.CreateSubKey(subKey, true);
                if (writeKey == null)
                    throw new InvalidOperationException($"Could not open or create registry key '{subKey}'.");
                writeKey.SetValue(valueName, typed, kind);
            }
            catch
            {
                lock (_gate)
                {
                    _history.Remove(entry);
                    SaveUnlocked();
                }
                throw;
            }

            LoggingService.Info($"Applied: {description} [{hive}\\{subKey}\\{valueName}] " +
                                $"original={(entry.OriginalExisted ? entry.OriginalValue : "<absent>")}, new={newValue}");
            return OperationResult.Ok($"Applied: {description}");
        }
        catch (UnauthorizedAccessException ex)
        {
            LoggingService.Error($"Access denied while applying: {description}", ex);
            return OperationResult.Fail("Access denied. Run NEXUS OPTIMIZER as administrator to change this setting.");
        }
        catch (Exception ex)
        {
            LoggingService.Error($"Failed to apply: {description}", ex);
            return OperationResult.Fail($"Failed: {ex.Message}");
        }
    }

    /// <summary>Restores the value that existed before the given change.</summary>
    public OperationResult RestoreEntry(Guid id)
    {
        BackupEntry entry;
        lock (_gate) entry = _history.FirstOrDefault(e => e.Id == id);

        if (entry == null) return OperationResult.Fail("Backup entry not found.");
        if (entry.Restored) return OperationResult.Fail("This change was already undone.");
        if (entry.Hive == PowerPlanService.HiveName)
        {
            if (!PowerPlanService.SetActive(entry.OriginalValue))
                return OperationResult.Fail("Could not restore the previous power plan.");

            lock (_gate)
            {
                entry.Restored = true;
                entry.RestoredAt = DateTime.UtcNow;
                SaveUnlocked();
            }
            LoggingService.Info($"Restored power plan {entry.OriginalValue}");
            return OperationResult.Ok("Restored the previous power plan.");
        }
        try
        {
            var hive = Enum.Parse<RegistryHive>(entry.Hive);
            using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);

            if (entry.OriginalExisted)
            {
                var kind = Enum.Parse<RegistryValueKind>(entry.OriginalKind);
                if (!TryParse(kind, entry.OriginalValue, out var typed, out var error))
                    return OperationResult.Fail($"Cannot restore: {error}");

                using var key = root.CreateSubKey(entry.SubKey, true);
                key.SetValue(entry.ValueName, typed, kind);
            }
            else
            {
                // The value did not exist before, so restoring means removing it.
                using var key = root.OpenSubKey(entry.SubKey, true);
                key?.DeleteValue(entry.ValueName, false);
            }

            lock (_gate)
            {
                entry.Restored = true;
                entry.RestoredAt = DateTime.UtcNow;
                SaveUnlocked();
            }

            LoggingService.Info($"Restored: {entry.Description}");
            return OperationResult.Ok($"Restored: {entry.Description}");
        }
        catch (UnauthorizedAccessException ex)
        {
            LoggingService.Error($"Access denied while restoring: {entry.Description}", ex);
            return OperationResult.Fail("Access denied. Run NEXUS OPTIMIZER as administrator to restore this setting.");
        }
        catch (Exception ex)
        {
            LoggingService.Error($"Failed to restore: {entry.Description}", ex);
            return OperationResult.Fail($"Restore failed: {ex.Message}");
        }
    }
    /// <summary>Backs up a string or DWORD value, then deletes it. Restoring the entry puts it back.</summary>
    public OperationResult DeleteRegistryValue(RegistryHive hive, string subKey, string valueName, string description)
    {
        try
        {
            using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var key = root.OpenSubKey(subKey, true);
            if (key == null) return OperationResult.Fail("Registry key not found.");

            var existing = key.GetValue(valueName, null);
            if (existing == null) return OperationResult.Fail("Registry value not found.");

            var entry = new BackupEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                Description = description,
                Hive = hive.ToString(),
                SubKey = subKey,
                ValueName = valueName,
                OriginalExisted = true,
                OriginalKind = key.GetValueKind(valueName).ToString(),
                OriginalValue = Convert.ToString(existing, CultureInfo.InvariantCulture),
                NewKind = "",
                NewValue = "<deleted>"
            };

            lock (_gate)
            {
                _history.Add(entry);
                SaveUnlocked();
            }

            try
            {
                key.DeleteValue(valueName, false);
            }
            catch
            {
                lock (_gate)
                {
                    _history.Remove(entry);
                    SaveUnlocked();
                }
                throw;
            }

            LoggingService.Info($"Deleted: {description} [{hive}\\{subKey}\\{valueName}]");
            return OperationResult.Ok($"Disabled: {description}");
        }
        catch (UnauthorizedAccessException ex)
        {
            LoggingService.Error($"Access denied while deleting: {description}", ex);
            return OperationResult.Fail("Access denied. Run NEXUS OPTIMIZER as administrator to change this entry.");
        }
        catch (Exception ex)
        {
            LoggingService.Error($"Failed to delete: {description}", ex);
            return OperationResult.Fail($"Failed: {ex.Message}");
        }
    }
    /// <summary>Sets a startup entry on or off the same way Windows Settings does, with a backup first.</summary>
    public OperationResult SetStartupApproval(RegistryHive hive, string valueName, bool enabled, string description)
    {
        const string approvedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        try
        {
            using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);

            byte[] original = null;
            using (var readKey = root.OpenSubKey(approvedKey, false))
                original = readKey?.GetValue(valueName) as byte[];

            // Windows uses 0x02 = enabled, 0x03 = disabled, followed by 8 bytes of timestamp.
            var newBytes = new byte[12];
            newBytes[0] = enabled ? (byte)0x02 : (byte)0x03;

            var entry = new BackupEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                Description = description,
                Hive = hive.ToString(),
                SubKey = approvedKey,
                ValueName = valueName,
                OriginalExisted = original != null,
                OriginalKind = original != null ? "Binary" : "",
                OriginalValue = original != null ? Convert.ToHexString(original) : "",
                NewKind = "Binary",
                NewValue = Convert.ToHexString(newBytes)
            };

            lock (_gate)
            {
                _history.Add(entry);
                SaveUnlocked();
            }

            try
            {
                using var writeKey = root.CreateSubKey(approvedKey, true);
                writeKey.SetValue(valueName, newBytes, RegistryValueKind.Binary);
            }
            catch
            {
                lock (_gate)
                {
                    _history.Remove(entry);
                    SaveUnlocked();
                }
                throw;
            }

            LoggingService.Info($"Startup {(enabled ? "enabled" : "disabled")}: {valueName}");
            return OperationResult.Ok($"{(enabled ? "Enabled" : "Disabled")}: {valueName}");
        }
        catch (UnauthorizedAccessException ex)
        {
            LoggingService.Error($"Access denied changing startup entry {valueName}", ex);
            return OperationResult.Fail("Access denied. Run NEXUS OPTIMIZER as administrator to change this entry.");
        }
        catch (Exception ex)
        {
            LoggingService.Error($"Failed to change startup entry {valueName}", ex);
            return OperationResult.Fail($"Failed: {ex.Message}");
        }
    }
    /// <summary>Adds a backup record before a non-registry change is made.</summary>
    public void AddPendingEntry(BackupEntry entry)
    {
        lock (_gate)
        {
            _history.Add(entry);
            SaveUnlocked();
        }
    }

    /// <summary>Removes a pending record when the change it describes failed.</summary>
    public void RemovePendingEntry(BackupEntry entry)
    {
        lock (_gate)
        {
            _history.Remove(entry);
            SaveUnlocked();
        }
    }
    private static bool TryParse(RegistryValueKind kind, string raw, out object typed, out string error)
    {
        typed = null;
        error = null;

        switch (kind)
        {
            case RegistryValueKind.DWord:
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                {
                    typed = i;
                    return true;
                }
                error = $"'{raw}' is not a valid DWORD value.";
                return false;
            case RegistryValueKind.Binary:
                try
                {
                    typed = Convert.FromHexString(raw ?? "");
                    return true;
                }
                catch
                {
                    error = "Stored binary value is damaged.";
                    return false;
                }
            case RegistryValueKind.String:
                typed = raw ?? "";
                return true;

            default:
                error = $"Unsupported registry type: {kind}. Only DWORD and String are handled.";
                return false;
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(HistoryFile)) return;
            var json = File.ReadAllText(HistoryFile);
            _history = JsonSerializer.Deserialize<List<BackupEntry>>(json) ?? new();
        }
        catch (Exception ex)
        {
            // Preserve the corrupt file so backup data is never silently overwritten.
            LoggingService.Error("Backup history is unreadable; preserving it and starting fresh.", ex);
            try
            {
                var corrupt = Path.Combine(BackupDir, $"history.corrupt-{DateTime.Now:yyyyMMddHHmmss}.json");
                File.Move(HistoryFile, corrupt, true);
            }
            catch (Exception moveEx)
            {
                LoggingService.Error("Could not preserve corrupt backup history", moveEx);
            }
            _history = new();
        }
    }

    /// <summary>Caller must hold _gate.</summary>
    private void SaveUnlocked()
    {
        var tmp = HistoryFile + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_history, JsonOptions));
        File.Move(tmp, HistoryFile, true);
    }
}