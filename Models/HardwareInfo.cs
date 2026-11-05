namespace NexusOptimizer.Models;

public class HardwareInfo
{
    public string WindowsName { get; set; } = "Unknown";
    public string Architecture { get; set; } = "Unknown";
    public string CpuName { get; set; } = "Unknown";
    public int LogicalProcessors { get; set; }
    public List<string> GpuNames { get; set; } = new();
    public double TotalRamGb { get; set; }
    public double AvailableRamGb { get; set; }
    public string SystemDrive { get; set; } = "";
    public double SystemDriveFreeGb { get; set; }
    public double SystemDriveTotalGb { get; set; }
    public string PowerPlan { get; set; } = "Unknown";
    public bool IsAdministrator { get; set; }
}