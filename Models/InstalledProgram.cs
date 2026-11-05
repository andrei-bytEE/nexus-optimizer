namespace NexusOptimizer.Models;

public class InstalledProgram
{
    public string DisplayName { get; init; } = "";
    public string Publisher { get; init; } = "";
    public string Version { get; init; } = "";
    public string UninstallCommand { get; init; } = "";
    public string RegistryKeyName { get; init; } = "";
}