using System.Collections.ObjectModel;
using NexusOptimizer.Helpers;
using NexusOptimizer.Models;
using NexusOptimizer.Services;

namespace NexusOptimizer.ViewModels;

public class StartupViewModel : ViewModelBase
{
    private readonly RegistryBackupService _backups;
    private string _statusMessage = "Turning a program off here matches the switch in Windows Settings > Apps > Startup.";

    public StartupViewModel(RegistryBackupService backups)
    {
        _backups = backups;
        Items = new ObservableCollection<StartupEntry>();
        DisabledItems = new ObservableCollection<BackupEntry>();
        DisableCommand = new RelayCommand(p => Toggle(p as StartupEntry));
        RestoreCommand = new RelayCommand(p => Restore(p as BackupEntry));
        Refresh();
    }

    public ObservableCollection<StartupEntry> Items { get; }
    public ObservableCollection<BackupEntry> DisabledItems { get; }
    public RelayCommand DisableCommand { get; }
    public RelayCommand RestoreCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => Set(ref _statusMessage, value);
    }

    /// <summary>Updates rows in place, so only the rows that changed redraw.</summary>
    public void Refresh()
    {
        var latest = StartupService.List();

        // Remove rows that no longer exist.
        for (int i = Items.Count - 1; i >= 0; i--)
        {
            if (!latest.Any(l => l.Name == Items[i].Name && l.Hive == Items[i].Hive))
                Items.RemoveAt(i);
        }

        // Update existing rows, or add new ones.
        foreach (var entry in latest)
        {
            var existing = Items.FirstOrDefault(i => i.Name == entry.Name && i.Hive == entry.Hive);
            if (existing == null)
            {
                Items.Add(entry);
            }
            else if (existing.IsEnabled != entry.IsEnabled)
            {
                existing.IsEnabled = entry.IsEnabled;
            }
        }

        DisabledItems.Clear();
        foreach (var backup in _backups.History
                     .Where(b => !b.Restored && b.Description.StartsWith(StartupService.DisabledPrefix))
                     .OrderByDescending(b => b.Timestamp))
        {
            DisabledItems.Add(backup);
        }
    }

    private void Toggle(StartupEntry entry)
    {
        if (entry == null) return;

        OperationResult result;
        if (entry.IsEnabled)
        {
            result = _backups.SetStartupApproval(entry.Hive, entry.Name, false,
                                                 StartupService.DisabledPrefix + entry.Name);
        }
        else
        {
            var backup = _backups.History.FirstOrDefault(b =>
                !b.Restored &&
                b.SubKey == StartupService.ApprovedKey &&
                b.ValueName == entry.Name &&
                b.Hive == entry.Hive.ToString());

            result = backup != null
                ? _backups.RestoreEntry(backup.Id)
                : _backups.SetStartupApproval(entry.Hive, entry.Name, true, "Enable: " + entry.Name);
        }

        StatusMessage = result.Message;
        Refresh();
    }

    private void Restore(BackupEntry backup)
    {
        if (backup == null) return;
        var result = _backups.RestoreEntry(backup.Id);
        StatusMessage = result.Message;
        Refresh();
    }
}