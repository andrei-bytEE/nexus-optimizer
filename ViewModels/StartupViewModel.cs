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

    public void Refresh()
    {
        Items.Clear();
        foreach (var entry in StartupService.List())
            Items.Add(entry);

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
            // Prefer restoring the original value, so Undo history stays accurate.
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