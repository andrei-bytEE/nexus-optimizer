using System.IO;

namespace NexusOptimizer.Models;

public class GameProfile
{
    public string Name { get; set; } = "";
    public string ExePath { get; set; } = "";
    public List<string> Tweaks { get; set; } = new();

    /// <summary>The process name Windows reports for this game, without ".exe".</summary>
    public string ProcessName => Path.GetFileNameWithoutExtension(ExePath);
}

public class GameStore
{
    public bool AutoOptimize { get; set; } = true;
    public List<GameProfile> Games { get; set; } = new();
}