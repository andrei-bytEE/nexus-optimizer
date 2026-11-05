using System.Collections.ObjectModel;
using System.Windows;
using NexusOptimizer.Helpers;
using NexusOptimizer.Models;
using NexusOptimizer.Services;

namespace NexusOptimizer.ViewModels;

public class UninstallAppsViewModel : ViewModelBase
{
    private const string DefaultStatus = "Tick apps to uninstall, or click Uninstall next to a program.";

    private readonly DebloatService _storeService = new();
    private string _statusMessage = DefaultStatus;
    private string _searchText = "";
    private bool _isBusy;

    private readonly List<InstalledProgram> _allPrograms = new();

    public UninstallAppsViewModel()
    {
        StoreItems = new ObservableCollection<DebloatItem>();
        Programs = new ObservableCollection<InstalledProgram>();
        RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
        UninstallSelectedCommand = new RelayCommand(async _ => await UninstallSelectedAsync());
        UninstallProgramCommand = new RelayCommand(p => UninstallProgram(p as InstalledProgram));
        _ = RefreshAsync();
    }

    public ObservableCollection<DebloatItem> StoreItems { get; }
    public ObservableCollection<InstalledProgram> Programs { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand UninstallSelectedCommand { get; }
    public RelayCommand UninstallProgramCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => Set(ref _statusMessage, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (Set(ref _searchText, value))
                ApplyFilter();
        }
    }

    private async Task RefreshAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        StatusMessage = "Checking installed apps...";
        try
        {
            var installed = await Task.Run(() => _storeService.GetInstalledPackageNames());
            var programs = await Task.Run(InstalledProgramService.GetPrograms);

            StoreItems.Clear();
            foreach (var item in DebloatService.Catalog.Where(i => installed.Contains(i.PackageName)))
            {
                item.IsInstalled = true;
                item.IsSelected = false;
                StoreItems.Add(item);
            }

            _allPrograms.Clear();
            _allPrograms.AddRange(programs);
            ApplyFilter();

            StatusMessage = DefaultStatus;
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void ApplyFilter()
    {
        Programs.Clear();
        var term = SearchText.Trim();
        foreach (var p in _allPrograms.Where(p =>
            term.Length == 0 || p.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            Programs.Add(p);
        }
    }

    private void UninstallProgram(InstalledProgram program)
    {
        if (program == null) return;

        var confirm = MessageBox.Show(
            $"Run the uninstaller for \"{program.DisplayName}\"?\n\nIts own uninstall window will open. " +
            "Undo All cannot restore programs.",
            "Confirm uninstall", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            StatusMessage = "Uninstall cancelled.";
            return;
        }

        var result = InstalledProgramService.Uninstall(program);
        StatusMessage = result.Message;
    }

    private async Task UninstallSelectedAsync()
    {
        if (_isBusy) return;

        var selected = StoreItems.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "No store apps are ticked.";
            return;
        }

        var list = string.Join("\n", selected.Select(i => "• " + i.DisplayName));
        var confirm = MessageBox.Show(
            "These apps will be uninstalled from your Windows account:\n\n" + list +
            "\n\nUndo All cannot restore them. Most can be reinstalled from the Microsoft Store. Continue?",
            "Confirm uninstall", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            StatusMessage = "Uninstall cancelled.";
            return;
        }

        _isBusy = true;
        var messages = new List<string>();
        try
        {
            foreach (var item in selected)
            {
                var result = await Task.Run(() => _storeService.Remove(item));
                messages.Add(result.Message);
            }
            StatusMessage = string.Join(" ", messages);
        }
        finally
        {
            _isBusy = false;
        }

        await RefreshAsync();
    }
}