using NexusOptimizer.Models;

namespace NexusOptimizer.Services;

public record ProfileRow(string Name, string Description);

/// <summary>Built-in profiles and tweak metadata. Profiles only select tweaks that already exist in the catalog.</summary>
public static class ProfileService
{
    private static readonly string[] Names =
        { "Safe", "Gaming", "Low-End PC", "Maximum Performance" };

    private static readonly Dictionary<string, string> Descriptions = new()
    {
        ["Safe"] = "Privacy and clutter settings only. Nothing here changes performance much. No administrator rights needed.",
        ["Gaming"] = "Game Mode, game recording and background apps, plus GPU scheduling (restart needed).",
        ["Low-End PC"] = "Turns off animations and transparency, background apps, and Edge preloading. Built for older or weaker PCs.",
        ["Maximum Performance"] = "Every tweak in the library. Some need administrator rights and a restart. Preview it first.",
    };

    private static readonly Dictionary<string, string[]> Members = new()
    {
        ["Safe"] = new[]
        {
            "Disable advertising ID",
            "Turn off Start menu suggestions",
            "Turn off Windows tips and suggestions",
            "Turn off silently installed app suggestions",
            "Turn off Bing results in Start menu search",
            "Turn off Start menu recommendations",
            "Turn off Windows Spotlight tips on lock screen",
        },
        ["Gaming"] = new[]
        {
            "Turn on Windows Game Mode",
            "Turn off Game DVR background recording",
            "Turn off Game Bar capture",
            "Stop background apps from running",
            "Enable hardware-accelerated GPU scheduling (requires administrator and restart)",
        },
        ["Low-End PC"] = new[]
        {
            "Turn on best-performance visual effects",
            "Turn off transparency effects",
            "Stop background apps from running",
            "Turn off Edge startup boost (requires administrator)",
            "Turn off Edge background running (requires administrator)",
            "Turn off Windows Spotlight tips on lock screen",
            "Turn off Start menu suggestions",
            "Turn off Start menu recommendations",
            "Turn off Game DVR background recording",
        },
    };

    // Category, restart flag, and evidence for each catalog item.
    private static readonly Dictionary<string, (string Category, bool Restart, string Evidence)> Meta = new()
    {
        ["Disable advertising ID"] = ("Privacy", false, "Per-user setting. Affects ad personalization only."),
        ["Turn off transparency effects"] = ("Visuals", false, "Reduces compositing work. Mainly helps weaker GPUs."),
        ["Turn off Start menu suggestions"] = ("Privacy", false, "Hides promoted apps. No performance effect expected."),
        ["Turn off Windows tips and suggestions"] = ("Privacy", false, "Reduces content delivery. Small effect."),
        ["Turn off silently installed app suggestions"] = ("Privacy", false, "Stops automatic promoted-app installs."),
        ["Turn on Windows Game Mode"] = ("Gaming", false, "Windows prioritizes the foreground game. Gains are not guaranteed."),
        ["Turn off Game DVR background recording"] = ("Gaming", false, "Stops background recording. Can reduce load while gaming."),
        ["Turn off Game Bar capture"] = ("Gaming", false, "Disables capture shortcuts. Does not change game speed by itself."),
        ["Stop background apps from running"] = ("Startup & Background", false, "Store apps stop when closed. Frees some RAM and CPU time."),
        ["Limit telemetry to Basic (requires administrator)"] = ("Policy (admin)", false, "Policy setting. Security updates are not affected."),
        ["Enable hardware-accelerated GPU scheduling (requires administrator and restart)"] = ("Gaming", true, "Needs a supported GPU and driver. Check the result after restart."),
        ["Turn off Windows Copilot"] = ("Search & Start", false, "Policy value. May have no effect on some Windows builds."),
        ["Turn off Bing results in Start menu search"] = ("Search & Start", false, "Keeps Start searches local. Not a speed change."),
        ["Turn off Start menu recommendations"] = ("Search & Start", false, "Hides recommendations. No performance effect expected."),
        ["Disable consumer app promotions (requires administrator)"] = ("Policy (admin)", false, "Policy setting. Stops promoted app installs."),
        ["Turn on best-performance visual effects"] = ("Visuals", false, "Turns off window animations. Mainly helps weaker PCs."),
        ["Turn off Windows Spotlight tips on lock screen"] = ("Privacy", false, "Stops lock-screen promotions. No performance effect expected."),
        ["Turn off Edge startup boost (requires administrator)"] = ("Edge", false, "Stops Edge preloading at sign-in. Frees memory while idle."),
        ["Turn off Edge background running (requires administrator)"] = ("Edge", false, "Stops Edge running after its window closes. Frees memory."),
        ["Turn off power throttling (requires administrator)"] = ("Power", false, "Background work keeps full speed. Uses more battery on laptops."),
        ["Turn off Cortana in search (requires administrator)"] = ("Search & Start", false, "Policy setting. Normal file search still works."),
        ["Turn off Windows Error Reporting (requires administrator)"] = ("Policy (admin)", false, "Stops crash report uploads. Crash dialogs still appear."),
    };

    /// <summary>Copies category, restart, and evidence onto the catalog items. Safe to call more than once.</summary>
    public static void ApplyMetadata()
    {
        foreach (var item in OptimizationService.Catalog)
        {
            if (!Meta.TryGetValue(item.Name, out var m)) continue;
            item.Category = m.Category;
            item.RequiresRestart = m.Restart;
            item.Evidence = m.Evidence;
        }
    }

    public static List<ProfileRow> Rows() =>
        Names.Select(n => new ProfileRow(n, Descriptions[n])).ToList();

    public static List<OptimizationItem> ItemsFor(string profile)
    {
        if (profile == "Maximum Performance")
            return OptimizationService.Catalog.ToList();

        var names = Members[profile];
        return OptimizationService.Catalog.Where(i => names.Contains(i.Name)).ToList();
    }

    /// <summary>Lists exactly what a profile would do, without changing anything.</summary>
    public static List<string> Preview(OptimizationService service, string profile)
    {
        var lines = new List<string>
        {
            $"{profile}: {ItemsFor(profile).Count} setting(s) in this profile.",
            ""
        };

        foreach (var item in ItemsFor(profile))
        {
            var state = service.IsApplied(item) ? "Already on" : "Will turn on";
            var tags = new List<string>();
            if (item.Name.Contains("requires administrator")) tags.Add("admin");
            if (item.RequiresRestart) tags.Add("restart");
            var tagText = tags.Count > 0 ? $" [{string.Join(", ", tags)}]" : "";
            lines.Add($"- {state}: {item.Name}{tagText}");
        }

        lines.Add("");
        lines.Add("Nothing changes until you click Apply.");
        return lines;
    }
}