using System.Collections.ObjectModel;
using Microsoft.Win32;
using NexusOptimizer.Helpers;
using NexusOptimizer.Models;
using NexusOptimizer.Services;

namespace NexusOptimizer.ViewModels;

public class ProfilesViewModel : ViewModelBase
{
    /// <summary>
    /// Gaming-only registry settings. Not part of the Windows Optimization catalog.
    /// Source: Microsoft Learn, MMCSS registry settings, Task values.
    /// </summary>
    private static readonly List<OptimizationItem> GamingItems = new()
    {
        new OptimizationItem
        {
            Name = "Set game scheduling category to High (requires administrator)",
            Description = "Gives game threads the High scheduling category in MMCSS, as documented by Microsoft. " +
                          "Background apps may get less CPU priority while a game runs.",
            Risk = "Medium",
            Hive = RegistryHive.LocalMachine,
            SubKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
            ValueName = "Scheduling Category",
            Kind = RegistryValueKind.String,
            AppliedValue = "High",
            OffValue = "Medium"
        }
    };

    private readonly OptimizationService _optimizer;
    private readonly RegistryBackupService _backups;
    private string _statusMessage = "Apply turns on the gaming settings and the High performance power plan. Revert undoes both.";
    private string _powerStatus = "Checking...";

    public ProfilesViewModel(OptimizationService optimizer, RegistryBackupService backups)
    {
        _optimizer = optimizer;
        _backups = backups;
        Items = new ObservableCollection<OptimizationItem>();
        ApplyGamingCommand = new RelayCommand(_ => ApplyGaming());
        RevertGamingCommand = new RelayCommand(_ => RevertGaming());
        Refresh();
    }

    public ObservableCollection<OptimizationItem> Items { get; }
    public RelayCommand ApplyGamingCommand { get; }
    public RelayCommand RevertGamingCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => Set(ref _statusMessage, value);
    }

    public string PowerStatus
    {
        get => _powerStatus;
        private set => Set(ref _powerStatus, value);
    }

    public void Refresh()
    {
        Items.Clear();
        foreach (var item in GamingItems)
        {
            item.IsApplied = _optimizer.IsApplied(item);
            Items.Add(item);
        }

        var active = PowerPlanService.GetActiveGuid();
        var high = PowerPlanService.FindGuidByName(PowerPlanService.HighPerformanceName);
        PowerStatus = active == null
            ? "Could not read the active power plan."
            : high != null && string.Equals(active, high, StringComparison.OrdinalIgnoreCase)
                ? "Active power plan: High performance"
                : "Active power plan: a different plan";
    }

    private void ApplyGaming()
    {
        var messages = new List<string>();
        int failed = 0;

        foreach (var item in GamingItems)
        {
            if (_optimizer.IsApplied(item)) continue;
            var result = _optimizer.Apply(item);
            messages.Add(result.Message);
            if (!result.Success) failed++;
        }

        var power = PowerPlanService.ActivateHighPerformance(_backups);
        messages.Add(power.Message);
        if (!power.Success) failed++;

        StatusMessage = (failed == 0 ? "Gaming profile applied. " : $"Finished with {failed} failure(s). ")
                        + string.Join(" ", messages);
        Refresh();
    }

    private void RevertGaming()
    {
        var messages = new List<string>();
        int failed = 0;

        foreach (var item in GamingItems)
        {
            if (!_optimizer.IsApplied(item)) continue;
            var result = _optimizer.Revert(item);
            messages.Add(result.Message);
            if (!result.Success) failed++;
        }

        var pendingPower = _backups.History
            .Where(b => !b.Restored && b.Hive == PowerPlanService.HiveName)
            .OrderByDescending(b => b.Timestamp)
            .FirstOrDefault();

        if (pendingPower != null)
        {
            var power = _backups.RestoreEntry(pendingPower.Id);
            messages.Add(power.Message);
            if (!power.Success) failed++;
        }

        StatusMessage = messages.Count == 0
            ? "Nothing to revert. The gaming profile is not applied."
            : (failed == 0 ? "Gaming profile reverted. " : $"Finished with {failed} failure(s). ")
              + string.Join(" ", messages);
        Refresh();
    }
}