using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using NexusOptimizer.Helpers;
using NexusOptimizer.Models;

namespace NexusOptimizer.Services;

public static class HardwareDetectionService
{
    private const double GiB = 1024.0 * 1024.0 * 1024.0;

    public static Task<HardwareInfo> DetectAsync() => Task.Run(Detect);

    private static HardwareInfo Detect()
    {
        var info = new HardwareInfo
        {
            WindowsName = ReadWindowsName(),
            Architecture = RuntimeInformation.OSArchitecture.ToString(),
            CpuName = ReadCpuName(),
            LogicalProcessors = Environment.ProcessorCount,
            GpuNames = ReadGpuNames(),
            IsAdministrator = AdminHelper.IsAdministrator(),
            PowerPlan = ReadPowerPlan()
        };
        ReadMemory(info);
        ReadStorage(info);
        return info;
    }

    private static string ReadWindowsName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var product = key?.GetValue("ProductName") as string ?? "Windows";
            var display = key?.GetValue("DisplayVersion") as string ?? "";
            var buildText = key?.GetValue("CurrentBuildNumber") as string ?? "0";
            int.TryParse(buildText, out var build);

            // ProductName still reads "Windows 10" on many Windows 11 installs. Build 22000+ is Windows 11.
            if (build >= 22000)
                product = product.Replace("Windows 10", "Windows 11");

            return string.IsNullOrEmpty(display)
                ? $"{product} (build {build})"
                : $"{product} {display} (build {build})";
        }
        catch (Exception ex)
        {
            LoggingService.Error("Could not read Windows version", ex);
            return "Unknown";
        }
    }

    private static string ReadCpuName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return (key?.GetValue("ProcessorNameString") as string)?.Trim() ?? "Unknown";
        }
        catch (Exception ex)
        {
            LoggingService.Error("Could not read CPU name", ex);
            return "Unknown";
        }
    }

    private static List<string> ReadGpuNames()
    {
        var names = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"] as string;
                if (!string.IsNullOrWhiteSpace(name)) names.Add(name.Trim());
            }
        }
        catch (Exception ex)
        {
            LoggingService.Error("GPU detection failed", ex);
        }
        return names;
    }

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

    private static void ReadMemory(HardwareInfo info)
    {
        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (GlobalMemoryStatusEx(ref mem))
        {
            info.TotalRamGb = mem.ullTotalPhys / GiB;
            info.AvailableRamGb = mem.ullAvailPhys / GiB;
        }
        else
        {
            LoggingService.Error("GlobalMemoryStatusEx failed",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }
    }

    private static void ReadStorage(HardwareInfo info)
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory);
            var drive = new DriveInfo(root);
            info.SystemDrive = root;
            info.SystemDriveFreeGb = drive.AvailableFreeSpace / GiB;
            info.SystemDriveTotalGb = drive.TotalSize / GiB;
        }
        catch (Exception ex)
        {
            LoggingService.Error("Could not read system drive", ex);
        }
    }

    private static string ReadPowerPlan()
    {
        try
        {
            var (exit, output) = RunProcess("powercfg.exe", "/getactivescheme");
            if (exit != 0) return "Unknown";

            // Matches "GUID: <guid> (Plan name)". Parentheses are used because the
            // "Power Scheme" label is localized on some Windows installs.
            var match = Regex.Match(output, @"GUID:\s*[0-9a-fA-F-]{36}\s*\((.+?)\)");
            return match.Success ? match.Groups[1].Value.Trim() : "Unknown";
        }
        catch (Exception ex)
        {
            LoggingService.Error("Could not read power plan", ex);
            return "Unknown";
        }
    }

    private static (int ExitCode, string Output) RunProcess(string file, string args)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {file}.");
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(10000);
        return (process.ExitCode, output);
    }
}