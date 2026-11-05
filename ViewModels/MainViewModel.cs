using System.Windows;
using NexusOptimizer.Helpers;
using NexusOptimizer.Services;

namespace NexusOptimizer.ViewModels;

public class MainViewModel : ViewModelBase
{
    private object _currentPage;

    public MainViewModel()
    {
        LicenseService.Load();

        var backups = new RegistryBackupService();
        var optimizer = new OptimizationService(backups);

        Dashboard = new DashboardViewModel(backups);
        Optimization = new OptimizationViewModel(optimizer);
        Profiles = new ProfilesViewModel(optimizer, backups);
        Startup = new StartupViewModel(backups);
        UninstallApps = new UninstallAppsViewModel();
        Performance = new PerformanceViewModel();
        License = new LicenseViewModel();
        _currentPage = Dashboard;
    }

    public DashboardViewModel Dashboard { get; }
    public OptimizationViewModel Optimization { get; }
    public ProfilesViewModel Profiles { get; }
    public StartupViewModel Startup { get; }
    public UninstallAppsViewModel UninstallApps { get; }
    public PerformanceViewModel Performance { get; }
    public LicenseViewModel License { get; }

    /// <summary>Shown in the sidebar footer, read from the running build.</summary>
    public string VersionText => "Version " + UpdateService.CurrentVersion;

    /// <summary>The page currently shown in the content area.</summary>
    public object CurrentPage
    {
        get => _currentPage;
        set => Set(ref _currentPage, value);
    }

    private void Navigate(object page, bool premium, Action refresh = null)
    {
        if (premium && !LicenseService.IsPremium)
        {
            MessageBox.Show(
                "This feature needs a premium license. Open the License page and enter your key.",
                "Premium feature", MessageBoxButton.OK, MessageBoxImage.Information);
            CurrentPage = License;
            return;
        }

        refresh?.Invoke();
        CurrentPage = page;
    }

    public RelayCommand ShowDashboardCommand => new(_ =>
    {
        Dashboard.RefreshLicenseState();
        Navigate(Dashboard, false);
    });

    public RelayCommand ShowPerformanceCommand => new(_ => Navigate(Performance, false));
    public RelayCommand ShowStartupCommand => new(_ => Navigate(Startup, false, Startup.Refresh));
    public RelayCommand ShowUninstallAppsCommand => new(_ => Navigate(UninstallApps, false));
    public RelayCommand ShowLicenseCommand => new(_ => Navigate(License, false, License.Refresh));

    public RelayCommand ShowOptimizationCommand => new(_ => Navigate(Optimization, true, Optimization.Refresh));
    public RelayCommand ShowProfilesCommand => new(_ => Navigate(Profiles, true, Profiles.Refresh));
}