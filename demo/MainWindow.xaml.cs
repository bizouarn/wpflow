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

        var state = new DemoState();
        Flow.Nodes =
        [
            new Node("color", new XYPosition(0, 0), "color", state),
            new Node("shape", new XYPosition(-35, 85), "shape", state),
            new Node("zoom", new XYPosition(-10, 190), "zoom", state),
            new Node("output", new XYPosition(215, 25), "output", state, FixedWidth: 300, FixedHeight: 250)
        ];
        Flow.Edges =
        [
            new Edge("e-color", "color", "output", TargetHandle: "color", Animated: true),
            new Edge("e-shape", "shape", "output", TargetHandle: "shape", Animated: true),
            new Edge("e-zoom", "zoom", "output", TargetHandle: "zoom", Animated: true)
        ];
        Flow.Viewport = new Viewport(560, 150, 1.1);

        // « Relier » : le node output n'utilise couleur, forme et zoom que si le node correspondant lui est connecté.
        void SyncConnections()
        {
            bool Linked(string handle) => Flow.Edges.Any(e => e.Target == "output" && e.TargetHandle == handle);
            state.UseColor = Linked("color");
            state.UseShape = Linked("shape");
            state.UseZoom = Linked("zoom");
        }

        System.ComponentModel.DependencyPropertyDescriptor
            .FromProperty(Wpflow.Wpf.FlowView.EdgesProperty, typeof(Wpflow.Wpf.FlowView))
            .AddValueChanged(Flow, (_, _) => SyncConnections());
        SyncConnections();

        // Outil de vérification visuelle : WPFLOW_SCREENSHOT=chemin.png capture la fenêtre puis quitte.
        if (Environment.GetEnvironmentVariable("WPFLOW_SCREENSHOT") is { Length: > 0 } path)
        {
            var selfTest = Environment.GetEnvironmentVariable("WPFLOW_SELFTEST") == "1";
            ContentRendered += async (_, _) =>
            {
                await Task.Delay(2000);
                if (int.TryParse(Environment.GetEnvironmentVariable("WPFLOW_PERF"), out var perfCount))
                {
                    var perfLines = new List<string>();
                    try
                    {
                        await PerfTest.Run(Flow, perfCount, perfLines.Add);
                    }
                    catch (Exception ex)
                    {
                        perfLines.Add("ERROR " + ex);
                    }

                    File.WriteAllLines(System.IO.Path.ChangeExtension(path, ".perf.log"), perfLines);
                }
                else if (selfTest)
                {
                    var logPath = System.IO.Path.ChangeExtension(path, ".log");
                    var lines = new List<string>();
                    try
                    {
                        await SelfTest.Run(Flow, lines.Add);
                    }
                    catch (Exception ex)
                    {
                        lines.Add("ERROR " + ex);
                    }

                    File.WriteAllLines(logPath, lines);
                    await Task.Delay(500);
                }

                Capture(path);
                Application.Current.Shutdown();
            };
        }
    }

    private void OnOpenExamples(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        new ExamplesWindow { Owner = this }.Show();
        e.Handled = true;
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
