using System.Collections.ObjectModel;
using NexusOptimizer.Models;
using NexusOptimizer.Services;
using NexusOptimizer.Helpers;

namespace NexusOptimizer.ViewModels;

public class OptimizationViewModel : ViewModelBase
{
    private readonly OptimizationService _service;
    private string _statusMessage = "Select an optimization to apply.";

    public OptimizationViewModel(OptimizationService service)
    {
        _service = service;
        Items = new ObservableCollection<OptimizationItem>();
        Refresh();
    }

    public ObservableCollection<OptimizationItem> Items { get; }
   public RelayCommand UndoAllCommand => new(_ => UndoAll());
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
}