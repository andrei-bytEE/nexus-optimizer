using System.Collections.ObjectModel;
using System.Windows;
using NexusOptimizer.Helpers;
using NexusOptimizer.Models;
using NexusOptimizer.Services;

namespace NexusOptimizer.ViewModels;

public class OptimizationViewModel : ViewModelBase
{
    private readonly OptimizationService _service;
    private string _statusMessage = "Preview a profile to see what it changes. Nothing changes until you click Apply.";

    public OptimizationViewModel(OptimizationService service)
    {
        _service = service;
        ProfileService.ApplyMetadata();

        Items = new ObservableCollection<OptimizationItem>();
        Profiles = new ObservableCollection<ProfileRow>(ProfileService.Rows());
        UndoAllCommand = new RelayCommand(_ => UndoAll());
        PreviewCommand = new RelayCommand(p => Preview(p as ProfileRow));
        ApplyProfileCommand = new RelayCommand(p => ApplyProfile(p as ProfileRow));
        Refresh();
    }

    public ObservableCollection<OptimizationItem> Items { get; }
    public ObservableCollection<ProfileRow> Profiles { get; }
    public RelayCommand UndoAllCommand { get; }
    public RelayCommand PreviewCommand { get; }
    public RelayCommand ApplyProfileCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => Set(ref _statusMessage, value);
    }

    /// <summary>Re-reads every item from the live registry.</summary>
    public void Refresh()
    {
        Items.Clear();
        foreach (var item in OptimizationService.Catalog)
        {
            item.IsApplied = _service.IsApplied(item);
            Items.Add(item);
        }
    }

    /// <summary>Applies an item if it is off, or reverts it if it is on. Then re-reads the state.</summary>
    public void Toggle(OptimizationItem item)
    {
        var result = item.IsApplied ? _service.Revert(item) : _service.Apply(item);
        StatusMessage = result.Message;
        Refresh();
    }

    /// <summary>Reverts every applied optimization in one step.</summary>
    public void UndoAll()
    {
        var (reverted, failed, errors) = _service.RevertAll();
        StatusMessage = failed == 0
            ? $"Reverted {reverted} optimization(s)."
            : $"Reverted {reverted}, failed {failed}: {string.Join(" | ", errors)}";
        Refresh();
    }

    private void Preview(ProfileRow row)
    {
        if (row == null) return;
        MessageBox.Show(string.Join("\n", ProfileService.Preview(_service, row.Name)),
                        "Preview: " + row.Name, MessageBoxButton.OK, MessageBoxImage.Information);
        StatusMessage = $"Preview shown for {row.Name}. Nothing was changed.";
    }

    private void ApplyProfile(ProfileRow row)
    {
        if (row == null) return;

        var pending = ProfileService.ItemsFor(row.Name).Where(i => !_service.IsApplied(i)).ToList();
        if (pending.Count == 0)
        {
            StatusMessage = $"{row.Name} is already fully applied.";
            Refresh();
            return;
        }

        var needsAdmin = pending.Any(i => i.Name.Contains("requires administrator")) && !AdminHelper.IsAdministrator();
        var needsRestart = pending.Any(i => i.RequiresRestart);

        var text = $"Apply the {row.Name} profile? {pending.Count} setting(s) will change." +
                   (needsAdmin ? "\n\nSome settings need administrator rights. Those will fail unless you restart as administrator." : "") +
                   (needsRestart ? "\n\nSome settings need a restart to take full effect." : "") +
                   "\n\nEvery change is backed up and can be undone.";

        if (MessageBox.Show(text, "Confirm profile", MessageBoxButton.YesNo, MessageBoxImage.Question)
            != MessageBoxResult.Yes)
        {
            StatusMessage = "Cancelled. Nothing was changed.";
            return;
        }

        int applied = 0, failed = 0;
        var errors = new List<string>();
        foreach (var item in pending)
        {
            var result = _service.Apply(item);
            if (result.Success) applied++;
            else { failed++; errors.Add(result.Message); }
        }

        LoggingService.Info($"Profile {row.Name}: applied {applied}, failed {failed}.");
        StatusMessage = failed == 0
            ? $"{row.Name}: applied {applied} setting(s)." + (needsRestart ? " Restart to finish." : "")
            : $"{row.Name}: applied {applied}, failed {failed}. " + string.Join(" | ", errors);
        Refresh();
    }
}