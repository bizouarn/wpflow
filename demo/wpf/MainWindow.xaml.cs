using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Wpflow.Core;

namespace demo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Tout le comportement est dans HomeViewModel ; la vue ne fait qu'ouvrir la fenêtre d'exemples qu'il demande.
        ((HomeViewModel)DataContext).ExamplesRequested += () => new ExamplesWindow { Owner = this }.Show();

        // Outil de vérification visuelle : WPFLOW_SCREENSHOT=chemin.png capture la fenêtre puis quitte.
        if (Environment.GetEnvironmentVariable("WPFLOW_SCREENSHOT") is { Length: > 0 } path)
        {
            var selfTest = Environment.GetEnvironmentVariable("WPFLOW_SELFTEST") == "1";
            ContentRendered += async (_, _) =>
            {
                await Task.Delay(2000);
                if (int.TryParse(Environment.GetEnvironmentVariable("WPFLOW_PERF"), out var perfCount))
                {
                    await SelfTestReport.Run(this, System.IO.Path.ChangeExtension(path, ".perf.log"), TimeSpan.FromSeconds(180),
                        log => PerfTest.Run(Flow, perfCount, log), () => Capture(path));
                }
                else if (selfTest)
                {
                    await SelfTestReport.Run(this, System.IO.Path.ChangeExtension(path, ".log"), TimeSpan.FromSeconds(120),
                        async log =>
                        {
                            await SelfTest.Run(Flow, log);
                            await Task.Delay(500);
                        }, () => Capture(path));
                }
                else
                {
                    Capture(path);
                    Application.Current.Shutdown();
                }
            };
        }
    }

    private void Capture(string path)
    {
        var content = (FrameworkElement)Content;
        var bmp = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(path);
        encoder.Save(fs);
    }
}
