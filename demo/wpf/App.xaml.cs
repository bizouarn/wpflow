using System.Windows;

namespace demo;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // WPFLOW_EXAMPLES=... ouvre directement la fenêtre d'exemples (auto-test inclus).
        Window window = Environment.GetEnvironmentVariable("WPFLOW_EXAMPLES") is { Length: > 0 }
            ? new ExamplesWindow()
            : new MainWindow();
        window.Show();
    }
}
