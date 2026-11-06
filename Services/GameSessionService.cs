using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using NexusOptimizer.Models;

namespace NexusOptimizer.Services;

/// <summary>Loads and saves the game list and the automatic-optimization switch.</summary>
public static class GameStorage
{
    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexusOptimizer");
    private static readonly string GamesFile = Path.Combine(DataDir, "games.json");

    public static GameStore Load()
    {
        try
        {
            if (!File.Exists(GamesFile)) return new GameStore();
            return JsonSerializer.Deserialize<GameStore>(File.ReadAllText(GamesFile)) ?? new GameStore();
        }
        catch (Exception ex)
        {
            LoggingService.Error("Could not read games.json", ex);
            return new GameStore();
        }
    }

    public static void Save(GameStore store)
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(GamesFile + ".tmp", JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(GamesFile + ".tmp", GamesFile, true);
        }
        catch (Exception ex)
        {
            LoggingService.Error("Could not save games.json", ex);
        }
    }
}

/// <summary>What was applied for the current session, saved to disk so a crash can be cleaned up.</summary>
public class SessionRecord
{
    public string Game { get; set; } = "";
    public List<string> Applied { get; set; } = new();
}

/// <summary>Applies approved tweaks while a selected game runs, and reverts them when it closes.</summary>
public class GameSessionService
{
    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexusOptimizer");
    private static readonly string SessionFile = Path.Combine(DataDir, "game-session.json");

    private readonly OptimizationService _optimizer;
    private readonly DispatcherTimer _timer;
    private ActiveSession _active;
    private bool _busy;

    public event Action StatusChanged;
    public string LastStatus { get; private set; } = "";

    public string ActiveText => _active == null
        ? "No game session is running."
        : $"Active now: {_active.Game} ({_active.Applied.Count} temporary tweak(s))";

    public GameSessionService(OptimizationService optimizer)
    {
        _optimizer = optimizer;
        RecoverLeftovers();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += (_, _) => Poll();
        _timer.Start();

        if (Application.Current != null)
        {
            Application.Current.Exit += (_, _) =>
            {
                _timer.Stop();
                EndSession();
            };
        }
    }

    private void Poll()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var store = GameStorage.Load();

            if (!store.AutoOptimize)
            {
                if (_active != null) EndSession();
                return;
            }

            if (_active != null)
            {
                if (!IsRunning(_active.ProcessName)) EndSession();
                return;
            }

            foreach (var game in store.Games)
            {
                if (!string.IsNullOrWhiteSpace(game.ProcessName) && IsRunning(game.ProcessName))
                {
                    StartSession(game);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            LoggingService.Error("Game session check failed", ex);
        }
        finally
        {
            _busy = false;
        }
    }

    private void StartSession(GameProfile game)
    {
        // Only apply tweaks that are currently off, so permanent changes are never reverted later.
        var candidates = OptimizationService.Catalog
            .Where(i => game.Tweaks.Contains(i.Name) && !_optimizer.IsApplied(i))
            .ToList();

        // Record the intended changes before touching anything, so a crash mid-way can be recovered.
        WriteSession(new SessionRecord { Game = game.Name, Applied = candidates.Select(c => c.Name).ToList() });

        var confirmed = new List<string>();
        var failed = new List<string>();
        foreach (var item in candidates)
        {
            var result = _optimizer.Apply(item);
            if (result.Success) confirmed.Add(item.Name);
            else failed.Add($"{item.Name}: {result.Message}");
        }

        WriteSession(new SessionRecord { Game = game.Name, Applied = confirmed });
        _active = new ActiveSession { Game = game.Name, ProcessName = game.ProcessName, Applied = confirmed };

        var text = $"{game.Name} detected. Applied {confirmed.Count} temporary tweak(s); they revert when it closes.";
        if (failed.Count > 0)
            text += " Could not apply: " + string.Join(" | ", failed);

        LoggingService.Info(text);
        SetStatus(text);
    }

    private void EndSession()
    {
        var session = _active;
        _active = null;
        if (session == null) return;

        int reverted = 0;
        var failed = new List<string>();
        foreach (var name in session.Applied)
        {
            var item = OptimizationService.Catalog.FirstOrDefault(i => i.Name == name);
            if (item == null) continue;

            if (!_optimizer.IsApplied(item))
            {
                reverted++;   // already back to its original state
                continue;
            }

            var result = _optimizer.Revert(item);
            if (result.Success) reverted++;
            else failed.Add($"{name}: {result.Message}");
        }

        var text = $"{session.Game} closed. Restored {reverted} temporary tweak(s).";
        if (failed.Count == 0)
        {
            DeleteSession();
        }
        else
        {
            // Keep the session file so the next launch retries the failed reverts.
            text += " Some could not be restored and will be retried on next launch: " + string.Join(" | ", failed);
        }

        LoggingService.Info(text);
        SetStatus(text);
    }

    private void RecoverLeftovers()
    {
        try
        {
            if (!File.Exists(SessionFile)) return;

            var record = JsonSerializer.Deserialize<SessionRecord>(File.ReadAllText(SessionFile));
            int reverted = 0;
            var failed = new List<string>();

            foreach (var name in record?.Applied ?? new List<string>())
            {
                var item = OptimizationService.Catalog.FirstOrDefault(i => i.Name == name);
                if (item == null || !_optimizer.IsApplied(item)) continue;

                var result = _optimizer.Revert(item);
                if (result.Success) reverted++;
                else failed.Add($"{name}: {result.Message}");
            }

            if (failed.Count == 0) DeleteSession();

            LastStatus = failed.Count == 0
                ? $"Recovered from an interrupted game session. Reverted {reverted} temporary tweak(s)."
                : "An interrupted game session could not be fully reverted: " + string.Join(" | ", failed);
            LoggingService.Info(LastStatus);
        }
        catch (Exception ex)
        {
            LoggingService.Error("Game session recovery failed", ex);
        }
    }

    private void SetStatus(string text)
    {
        LastStatus = text;
        StatusChanged?.Invoke();
    }

    private static void WriteSession(SessionRecord record)
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(SessionFile, JsonSerializer.Serialize(record));
        }
        catch (Exception ex)
        {
            LoggingService.Error("Could not write game session file", ex);
        }
    }

    private static void DeleteSession()
    {
        try { File.Delete(SessionFile); }
        catch (Exception ex) { LoggingService.Error("Could not delete game session file", ex); }
    }

    private static bool IsRunning(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var p in processes) p.Dispose();
        }
    }

    private sealed class ActiveSession
    {
        public string Game { get; init; } = "";
        public string ProcessName { get; init; } = "";
        public List<string> Applied { get; init; } = new();
    }
}