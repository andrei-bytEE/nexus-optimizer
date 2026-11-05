using System.Windows;
using NexusOptimizer.Services;

namespace NexusOptimizer;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LoggingService.Info("NEXUS OPTIMIZER started.");
        if (!Services.PrivacyAcceptance.IsAccepted)
        {
            var privacy = new Views.PrivacyWindow();
            privacy.ShowDialog();
        }
        DispatcherUnhandledException += (_, args) =>
        {
            LoggingService.Error("Unhandled UI exception", args.Exception);
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{args.Exception.Message}\n\nDetails were written to the log.",
                "NEXUS OPTIMIZER", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        var update = await UpdateService.CheckAsync();
        if (update == null) return;

        var answer = MessageBox.Show(
            $"Version {update.Version} is available.\n\n{update.Notes}\n\nUpdate now? The app will restart.",
            "NEXUS OPTIMIZER update", MessageBoxButton.YesNo, MessageBoxImage.Information);

        if (answer != MessageBoxResult.Yes) return;

        var (ok, message) = await UpdateService.InstallAsync(update);
        MessageBox.Show(message, "NEXUS OPTIMIZER update",
            MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Error);

        if (ok)
        {
            UpdateService.Restart();
            Shutdown();
        }
    }
}