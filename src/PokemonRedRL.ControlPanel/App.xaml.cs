using System.Windows;
using PokemonRedRL.ControlPanel.Services;
using PokemonRedRL.ControlPanel.ViewModels;

namespace PokemonRedRL.ControlPanel;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Surface unhandled exceptions (e.g. a failed process launch) as a message box instead of a
        // silent crash, so the operator can see what went wrong with a run.
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.ToString(), "PokemonRedRL Control Panel — unhandled error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        // Manual composition root (no generic host needed for a single-window WPF app).
        var manifestService = new RunManifestService();
        var agentProcessManager = new AgentProcessManager();
        var pingProbe = new EmulatorPingProbe();
        var previewCapture = new PreviewCaptureService();
        var windowEnumeration = new WindowEnumerationService();

        var viewModel = new MainViewModel(manifestService, agentProcessManager, pingProbe, previewCapture, windowEnumeration);
        var mainWindow = new MainWindow(viewModel);
        mainWindow.Closed += (_, _) => viewModel.Dispose();
        mainWindow.Show();
    }
}

