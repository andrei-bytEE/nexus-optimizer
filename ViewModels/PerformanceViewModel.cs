using System.Windows.Threading;
using NexusOptimizer.Services;

namespace NexusOptimizer.ViewModels;

public class PerformanceViewModel : ViewModelBase
{
    private readonly PerformanceService _service = new();
    private readonly DispatcherTimer _timer;
    private bool _busy;

    private string _cpuUsage = "Measuring...";
    private string _cpuTemp = "";
    private string _memoryUsage = "";
    private string _memoryDetail = "";
    private string _gpuName = "";
    private string _gpuUsage = "";
    private string _gpuTemp = "";
    private string _lastUpdated = "";

    public PerformanceViewModel()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
        _ = RefreshAsync();
    }

    public string CpuUsage { get => _cpuUsage; private set => Set(ref _cpuUsage, value); }
    public string CpuTemp { get => _cpuTemp; private set => Set(ref _cpuTemp, value); }
    public string MemoryUsage { get => _memoryUsage; private set => Set(ref _memoryUsage, value); }
    public string MemoryDetail { get => _memoryDetail; private set => Set(ref _memoryDetail, value); }
    public string GpuName { get => _gpuName; private set => Set(ref _gpuName, value); }
    public string GpuUsage { get => _gpuUsage; private set => Set(ref _gpuUsage, value); }
    public string GpuTemp { get => _gpuTemp; private set => Set(ref _gpuTemp, value); }
    public string LastUpdated { get => _lastUpdated; private set => Set(ref _lastUpdated, value); }

    private async Task RefreshAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var r = await Task.Run(() => _service.Read());
            Apply(r);
        }
        finally
        {
            _busy = false;
        }
    }

    private void Apply(PerformanceReading r)
    {
        CpuUsage = r.CpuUsagePercent is double c ? $"{c:F0}%" : "Measuring...";
        CpuTemp = r.CpuTempC is double t ? $"{t:F0} °C" : "Not available on this PC";

        var memPct = r.MemoryTotalGb > 0 ? r.MemoryUsedGb / r.MemoryTotalGb * 100 : 0;
        MemoryUsage = $"{memPct:F0}% used";
        MemoryDetail = $"{r.MemoryUsedGb:F1} GB of {r.MemoryTotalGb:F1} GB in use";

        GpuName = string.IsNullOrEmpty(r.GpuName) ? "No NVIDIA GPU detected" : r.GpuName;
        GpuUsage = r.GpuUsagePercent is double g ? $"{g:F0}%" : "Not available";
        GpuTemp = r.GpuTempC is double gt ? $"{gt:F0} °C" : "Not available";

        LastUpdated = "Updated " + DateTime.Now.ToString("HH:mm:ss");
    }
}