using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using NexusOptimizer.Helpers;
using NexusOptimizer.Models;
using NexusOptimizer.Services;

namespace NexusOptimizer.ViewModels;

public class GameRow : ViewModelBase
{
    public GameRow(GameProfile profile) => Profile = profile;

    public GameProfile Profile { get; }
    public string Name => Profile.Name;
    public string ExePath => Profile.ExePath;
    public string SummaryText => $"{Profile.Tweaks.Count} tweak(s) selected · watches for {Profile.ProcessName}.exe";

    public void Touch() => OnPropertyChanged(nameof(SummaryText));
}

public class TweakChoice : ViewModelBase
{
    private bool _isChecked;
    private readonly Action _changed;

    public TweakChoice(OptimizationItem item, bool isChecked, Action changed)
    {
        Item = item;
        _isChecked = isChecked;
        _changed = changed;
    }

    public OptimizationItem Item { get; }
    public string Name => Item.Name;
    public string Description => $"{Item.Category} · {Item.Risk} risk · {Item.Evidence}";

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (Set(ref _isChecked, value)) _changed();
        }
    }
}

public class GamesViewModel : ViewModelBase
{
    private readonly GameSessionService _sessions;
    private GameRow _selected;
    private string _status = "Add a game, then choose the tweaks it should use while it runs.";
    private bool _autoOptimize;

    public GamesViewModel(GameSessionService sessions)
    {
        _sessions = sessions;
        _autoOptimize = GameStorage.Load().AutoOptimize;

        AddGameCommand = new RelayCommand(_ => AddGame());
        RemoveGameCommand = new RelayCommand(_ => RemoveSelected(), _ => Selected != null);
        SelectCommand = new RelayCommand(p => Select(p as GameRow));

        _sessions.StatusChanged += OnSessionStatusChanged;
        Refresh();
    }

    public ObservableCollection<GameRow> Games { get; } = new();
    public ObservableCollection<TweakChoice> Tweaks { get; } = new();
    public RelayCommand AddGameCommand { get; }
    public RelayCommand RemoveGameCommand { get; }
    public RelayCommand SelectCommand { get; }

    public GameRow Selected
    {
        get => _selected;
        private set
        {
            if (Set(ref _selected, value))
            {
                RemoveGameCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(SelectedName));
            }
        }
    }

    public string SelectedName => Selected?.Name ?? "No game selected";
    public string SessionText => _sessions.ActiveText;

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public bool AutoOptimize
    {
        get => _autoOptimize;
        set
        {
            if (!Set(ref _autoOptimize, value)) return;
            var store = GameStorage.Load();
            store.AutoOptimize = value;
            GameStorage.Save(store);
            Status = value
                ? "Automatic game optimization is on."
                : "Automatic game optimization is off. Nothing changes when games start.";
        }
    }

    public void Refresh()
    {
        var previousPath = Selected?.ExePath;
        var store = GameStorage.Load();

        Games.Clear();
        foreach (var g in store.Games) Games.Add(new GameRow(g));

        Select(Games.FirstOrDefault(g => g.ExePath == previousPath));
        OnPropertyChanged(nameof(SessionText));
    }

    private void OnSessionStatusChanged()
    {
        Status = _sessions.LastStatus;
        OnPropertyChanged(nameof(SessionText));
    }

    private void Select(GameRow row)
    {
        Selected = row;
        Tweaks.Clear();
        if (row == null) return;

        var chosen = new HashSet<string>(row.Profile.Tweaks);
        foreach (var item in OptimizationService.Catalog)
            Tweaks.Add(new TweakChoice(item, chosen.Contains(item.Name), SaveSelectedTweaks));
    }

    private void SaveSelectedTweaks()
    {
        if (Selected == null) return;

        var names = Tweaks.Where(t => t.IsChecked).Select(t => t.Name).ToList();

        var store = GameStorage.Load();
        var game = store.Games.FirstOrDefault(g => g.ExePath == Selected.ExePath);
        if (game == null) return;

        game.Tweaks = names;
        GameStorage.Save(store);

        Selected.Profile.Tweaks = names;
        Selected.Touch();
        Status = $"Saved {names.Count} tweak(s) for {game.Name}. They apply only while it runs.";
    }

    private void AddGame()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Programs (*.exe)|*.exe",
            Title = "Choose the game's .exe file"
        };
        if (dialog.ShowDialog() != true) return;

        var store = GameStorage.Load();
        if (store.Games.Any(g => string.Equals(g.ExePath, dialog.FileName, StringComparison.OrdinalIgnoreCase)))
        {
            Status = "That game is already in the list.";
            return;
        }

        store.Games.Add(new GameProfile
        {
            Name = Path.GetFileNameWithoutExtension(dialog.FileName),
            ExePath = dialog.FileName
        });
        GameStorage.Save(store);

        Refresh();
        Select(Games.FirstOrDefault(g => g.ExePath == dialog.FileName));
        Status = $"Added {Path.GetFileNameWithoutExtension(dialog.FileName)}. Choose its tweaks on the right.";
    }

    private void RemoveSelected()
    {
        if (Selected == null) return;

        var answer = MessageBox.Show(
            $"Remove {Selected.Name} from the list? Its tweak choices are deleted. Nothing on your PC changes.",
            "Remove game", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        var store = GameStorage.Load();
        store.Games.RemoveAll(g => g.ExePath == Selected.ExePath);
        GameStorage.Save(store);

        Status = "Game removed.";
        Refresh();
    }
}