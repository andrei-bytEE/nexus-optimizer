using System.Collections.ObjectModel;
using System.Windows;
using NexusOptimizer.Helpers;
using NexusOptimizer.Models;
using NexusOptimizer.Services;

namespace NexusOptimizer.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private readonly RegistryBackupService _backups;
    private readonly OptimizationService _optimizer;
    private bool _isScanning;
    private string _scanStatus = "Not scanned yet";
    private bool _showElevateButton;
    private bool _isOptimizing;

    public DashboardViewModel(RegistryBackupService backups)
    {
        _backups = backups;
        _optimizer = new OptimizationService(backups);

        SystemRows = new ObservableCollection<InfoRow>();
        OptimizationRows = new ObservableCollection<InfoRow>();

        SystemScanCommand = new RelayCommand(async _ => await ScanAsync(), _ => !IsScanning);
        ElevateCommand = new RelayCommand(_ => Elevate());
        OptimizeNowCommand = new RelayCommand(_ => OptimizeNow(lowRiskOnly: false), _ => CanOptimize);
        QuickOptimizeCommand = new RelayCommand(_ => OptimizeNow(lowRiskOnly: true), _ => CanOptimize);
        UninstallCommand = new RelayCommand(_ => Uninstall());

        RebuildOptimizationRows();
        _ = ScanAsync();
    }

    public ObservableCollection<InfoRow> SystemRows { get; }
    public ObservableCollection<InfoRow> OptimizationRows { get; }
    public RelayCommand SystemScanCommand { get; }
    public RelayCommand ElevateCommand { get; }
    public RelayCommand OptimizeNowCommand { get; }
    public RelayCommand QuickOptimizeCommand { get; }
    public RelayCommand UninstallCommand { get; }

    /// <summary>Optimize buttons work only with a premium license and when no run is in progress.</summary>
    public bool CanOptimize => LicenseService.IsPremium && !_isOptimizing;

    /// <summary>Shown on the buttons and status area so the free plan is clear.</summary>
    public string OptimizeHint => LicenseService.IsPremium
        ? ""
        : "Optimize buttons need a premium license. Open the License page to enter your key.";

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (Set(ref _isScanning, value))
                SystemScanCommand?.RaiseCanExecuteChanged();
        }
    }

    public string ScanStatus
    {
        get => _scanStatus;
        private set => Set(ref _scanStatus, value);
    }

    public bool ShowElevateButton
    {
        get => _showElevateButton;
        private set => Set(ref _showElevateButton, value);
    }

    private void RaiseOptimizeState()
    {
        OptimizeNowCommand.RaiseCanExecuteChanged();
        QuickOptimizeCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanOptimize));
    }

    private async Task ScanAsync()
    {
        IsScanning = true;
        ScanStatus = "Scanning system...";
        try
        {
            var hw = await HardwareDetectionService.DetectAsync();
            ApplyHardware(hw);
            ScanStatus = $"Scan completed at {DateTime.Now:HH:mm:ss}";
            LoggingService.Info("System scan completed.");
        }
        catch (Exception ex)
        {
            ScanStatus = "Scan failed: " + ex.Message;
            LoggingService.Error("System scan failed", ex);
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void ApplyHardware(HardwareInfo hw)
    {
        SystemRows.Clear();
        SystemRows.Add(new InfoRow("Windows", hw.WindowsName));
        SystemRows.Add(new InfoRow("CPU", hw.CpuName));
        SystemRows.Add(new InfoRow("Logical processors", hw.LogicalProcessors.ToString()));
        SystemRows.Add(new InfoRow("GPU", hw.GpuNames.Count > 0 ? string.Join(", ", hw.GpuNames) : "Not detected"));
        SystemRows.Add(new InfoRow("Installed RAM", $"{hw.TotalRamGb:F1} GB ({hw.AvailableRamGb:F1} GB available)"));
        SystemRows.Add(new InfoRow("Architecture", hw.Architecture));
        SystemRows.Add(new InfoRow("System drive",
            $"{hw.SystemDrive} {hw.SystemDriveFreeGb:F1} GB free of {hw.SystemDriveTotalGb:F1} GB"));
        SystemRows.Add(new InfoRow("Power plan", hw.PowerPlan));
        SystemRows.Add(new InfoRow("Administrator",
            hw.IsAdministrator ? "Yes" : "No (some changes need elevation)"));

        ShowElevateButton = !hw.IsAdministrator;
        RebuildOptimizationRows();
    }

    private void RebuildOptimizationRows()
    {
        var history = _backups.History;
        var active = history.Where(e => !e.Restored).ToList();
        var last = history.OrderByDescending(e => e.Timestamp).FirstOrDefault();

        OptimizationRows.Clear();
        OptimizationRows.Add(new InfoRow("Status",
            active.Count > 0 ? "Optimizations applied" : "No optimizations applied"));
        OptimizationRows.Add(new InfoRow("Applied changes", active.Count.ToString()));
        OptimizationRows.Add(new InfoRow("Last change",
            last == null ? "Never" : last.Timestamp.ToLocalTime().ToString("g")));
    }

    private async void OptimizeNow(bool lowRiskOnly)
    {
        if (!CanOptimize) return;

        _isOptimizing = true;
        RaiseOptimizeState();
        ScanStatus = lowRiskOnly ? "Running Quick Optimization..." : "Running Optimize Now...";

        try
        {
            var (applied, skipped, failed, errors) =
                await Task.Run(() => _optimizer.ApplyMany(lowRiskOnly));

            var summary = $"Applied {applied}, already applied or skipped {skipped}, failed {failed}.";
            if (failed > 0 && !AdminHelper.IsAdministrator())
                summary += " Some changes need administrator rights. Click Restart as Administrator and try again.";
            ScanStatus = summary;
            LoggingService.Info("Bulk optimization: " + summary);
            if (errors.Count > 0)
                LoggingService.Error("Bulk optimization errors: " + string.Join(" | ", errors));

            RebuildOptimizationRows();
        }
        finally
        {
            _isOptimizing = false;
            RaiseOptimizeState();
        }
    }

    /// <summary>Call after a license is activated so the buttons unlock without restarting.</summary>
    public void RefreshLicenseState()
    {
        OnPropertyChanged(nameof(OptimizeHint));
        RaiseOptimizeState();
    }

    private void Uninstall()
    {
        var pending = UninstallService.PendingChangeCount(_backups);
        var confirm = MessageBox.Show(
            $"This will:\n\n" +
            $"• Revert {pending} applied change(s) to their original values\n" +
            "• Delete NEXUS OPTIMIZER's logs and backup history\n" +
            "• Close the app\n\n" +
            "Your installed programs and apps are not affected. Continue?",
            "Uninstall NEXUS OPTIMIZER", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            ScanStatus = "Uninstall cancelled.";
            return;
        }

        var revert = UninstallService.RevertAll(_backups);
        if (!revert.Success)
        {
            MessageBox.Show(revert.Message, "Uninstall stopped", MessageBoxButton.OK, MessageBoxImage.Error);
            ScanStatus = "Uninstall stopped. Nothing was deleted.";
            return;
        }

        var data = UninstallService.DeleteAppData();
        if (!data.Success)
        {
            MessageBox.Show(data.Message, "Uninstall incomplete", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        var deleteExe = MessageBox.Show(
            "Also delete the NEXUS-OPTIMIZER.exe file?",
            "Uninstall NEXUS OPTIMIZER", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (deleteExe == MessageBoxResult.Yes)
            UninstallService.ScheduleExeDeletion();

        Application.Current.Shutdown();
    }

    private void Elevate()
    {
        if (AdminHelper.RelaunchElevated())
            Application.Current.Shutdown();
        else
            ScanStatus = "Elevation was cancelled. Non-administrator features remain available.";
    }
}