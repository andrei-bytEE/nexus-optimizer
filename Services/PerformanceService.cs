using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;

namespace NexusOptimizer.Services;

public class PerformanceReading
{
    public double? CpuUsagePercent { get; init; }
    public double? CpuTempC { get; init; }
    public double MemoryUsedGb { get; init; }
    public double MemoryTotalGb { get; init; }
    public string GpuName { get; init; } = "";
    public double? GpuUsagePercent { get; init; }
    public double? GpuTempC { get; init; }
}

/// <summary>Read-only sensor readings. Changes nothing on the PC.</summary>
public class PerformanceService
{
    private const double GiB = 1024.0 * 1024.0 * 1024.0;

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint Low;
        public uint High;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

    private ulong _prevIdle, _prevKernel, _prevUser;
    private bool _hasPrevious;

    public PerformanceReading Read()
    {
        var (gpuName, gpuUsage, gpuTemp) = ReadNvidiaGpu();
        var (used, total) = ReadMemory();

        return new PerformanceReading
        {
            CpuUsagePercent = ReadCpuUsage(),
            CpuTempC = ReadCpuTemp(),
            MemoryUsedGb = used,
            MemoryTotalGb = total,
            GpuName = gpuName,
            GpuUsagePercent = gpuUsage,
            GpuTempC = gpuTemp
        };
    }

    /// <summary>CPU usage is calculated from the change since the previous reading, so the first call returns null.</summary>
    private double? ReadCpuUsage()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return null;

        var i = ToU64(idle);
        var k = ToU64(kernel);
        var u = ToU64(user);

        double? result = null;
        if (_hasPrevious)
        {
            var idleDelta = i - _prevIdle;
            var kernelDelta = k - _prevKernel;
            var userDelta = u - _prevUser;
            var total = kernelDelta + userDelta;   // kernel time already includes idle time
            if (total > 0)
                result = (total - idleDelta) * 100.0 / total;
        }

        _prevIdle = i;
        _prevKernel = k;
        _prevUser = u;
        _hasPrevious = true;
        return result;
    }

    private static (double Used, double Total) ReadMemory()
    {
        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref mem)) return (0, 0);

        var total = mem.ullTotalPhys / GiB;
        var used = (mem.ullTotalPhys - mem.ullAvailPhys) / GiB;
        return (used, total);
    }

    /// <summary>Reads CPU temperature from the motherboard's ACPI thermal zone. Often unavailable.</summary>
    private static double? ReadCpuTemp()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");

            double? hottest = null;
            foreach (ManagementObject zone in searcher.Get())
            {
                // Reported in tenths of Kelvin.
                var celsius = Convert.ToDouble(zone["CurrentTemperature"], CultureInfo.InvariantCulture) / 10.0 - 273.15;
                if (celsius > 0 && celsius < 150)
                    hottest = hottest.HasValue ? Math.Max(hottest.Value, celsius) : celsius;
            }
            return hottest;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Reads GPU name, usage, and temperature from nvidia-smi. Returns nulls if it isn't available.</summary>
    private static (string Name, double? Usage, double? Temp) ReadNvidiaGpu()
    {
        var output = RunProcess("nvidia-smi",
            "--query-gpu=name,utilization.gpu,temperature.gpu --format=csv,noheader,nounits");
        if (string.IsNullOrWhiteSpace(output)) return ("", null, null);

        var parts = output.Trim().Split(',');
        if (parts.Length < 3) return ("", null, null);

        double? usage = double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var u) ? u : null;
        double? temp = double.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var t) ? t : null;
        return (parts[0].Trim(), usage, temp);
    }

    private static string RunProcess(string file, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(file, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process == null) return null;

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(3000)) { process.Kill(); return null; }
            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;   // tool not installed or not an NVIDIA system
        }
    }

    private static ulong ToU64(FILETIME t) => ((ulong)t.High << 32) | t.Low;
}