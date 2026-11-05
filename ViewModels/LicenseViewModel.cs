using NexusOptimizer.Helpers;
using NexusOptimizer.Services;

namespace NexusOptimizer.ViewModels;

public class LicenseViewModel : ViewModelBase
{
    private string _keyText = "";
    private string _status = "Paste the key you received, then click Activate.";

    public LicenseViewModel()
    {
        ActivateCommand = new RelayCommand(_ => Activate());
        Refresh();
    }

    public RelayCommand ActivateCommand { get; }

    public string KeyText
    {
        get => _keyText;
        set => Set(ref _keyText, value);
    }

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public string PlanText => LicenseService.IsPremium
        ? $"Premium (licensed to {LicenseService.Licensee})"
        : "Free trial: Dashboard, Performance, Startup Apps, Uninstall Apps";

    public string MachineIdText => "Your machine ID: " + LicenseService.MachineId;

    public void Refresh()
    {
        OnPropertyChanged(nameof(PlanText));
        OnPropertyChanged(nameof(MachineIdText));
    }

    private void Activate()
    {
        var ok = LicenseService.TryActivate(KeyText, out var message);
        Status = message;
        if (ok) KeyText = "";
        Refresh();
    }
}