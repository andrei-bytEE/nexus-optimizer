using NexusOptimizer.ViewModels;

namespace NexusOptimizer.Models;

public class DebloatItem : ViewModelBase
{
    private bool _isInstalled;
    private bool _isSelected;

    public DebloatItem(string packageName, string displayName, string description)
    {
        PackageName = packageName;
        DisplayName = displayName;
        Description = description;
    }

    public string PackageName { get; }
    public string DisplayName { get; }
    public string Description { get; }

    public bool IsInstalled
    {
        get => _isInstalled;
        set
        {
            if (Set(ref _isInstalled, value))
                OnPropertyChanged(nameof(StatusText));
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    public string StatusText => IsInstalled ? "Installed" : "Not installed";
}