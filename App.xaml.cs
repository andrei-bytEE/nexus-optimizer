using System.Windows;
using NexusOptimizer.Services;

namespace NexusOptimizer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LoggingService.Info("NEXUS OPTIMIZER started.");

        DispatcherUnhandledException += (_, args) =>
        {
            LoggingService.Error("Unhandled UI exception", args.Exception);
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{args.Exception.Message}\n\nDetails were written to the log.",
                "NEXUS OPTIMIZER", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}