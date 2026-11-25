// Sonde ajoutée à chaque application témoin WPF : elle vérifie le FlowView de la fenêtre puis ferme l'application.
// Résultat dans le fichier WPFLOW_PROBE ; code de sortie 0 seulement si tout passe.
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Wpflow.Core;
using Wpflow.Wpf;

namespace Consumer;

internal static class Probe
{
    private static readonly List<string> BindingErrors = [];

    private sealed class Listener : TraceListener
    {
        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
            if (message != null) BindingErrors.Add(message);
        }
    }

    /// <summary>À appeler dans le constructeur de la fenêtre, avant InitializeComponent.</summary>
    public static void Watch()
    {
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(new Listener());
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
    }

    public static void Attach(Window window, Func<FlowView> flow, Action<FlowView, Action<string, bool>>? extra = null)
    {
        var path = Environment.GetEnvironmentVariable("WPFLOW_PROBE");
        if (string.IsNullOrEmpty(path)) return;
        window.ContentRendered += async (_, _) =>
        {
            var lines = new List<string>();
            var failed = false;
            void Check(string name, bool ok)
            {
                failed |= !ok;
                lines.Add((ok ? "OK    " : "ECHEC ") + name);
            }

            try
            {
                await Task.Delay(1500);
                var view = flow();
                lines.Add($"INFO  runtime {Environment.Version}");
                Check("style par défaut chargé sans rien déclarer", view.Template != null && Find<EdgeLayer>(view).Any());
                Check($"nodes mesurés ({view.Nodes.Count})", view.Nodes.Count > 0 && view.Nodes.All(n => n.Width > 0 && n.Height > 0));
                Check("un NodeHost par node", Find<NodeHost>(view).Count() == view.Nodes.Count);
                Check($"edges présents ({view.Edges.Count})", view.Edges.Count > 0);
                Check("handles affichés et dimensionnés", Find<NodeHost>(view).All(h => Find<Handle>(h).Any(x => x.ActualWidth > 0)));

                var png = Path.ChangeExtension(path, ".png");
                Check("export d'image : fichier écrit", view.SaveImage(png) && new FileInfo(png).Length > 0);
                // Chaque node doit laisser une trace à sa place dans l'image (marge de 20 px, échelle 1) : ni vide, ni décalée.
                var bitmap = view.RenderToBitmap(20)!;
                var left = view.Nodes.Min(n => n.Position.X) - 20;
                var top = view.Nodes.Min(n => n.Position.Y) - 20;
                Check("export d'image : chaque node est dessiné à sa place", view.Nodes.All(n => Drawn(bitmap,
                    (int)(n.Position.X - left), (int)(n.Position.Y - top), (int)n.Width!.Value, (int)n.Height!.Value)));

                var light = Color(view, "Flow.Background");
                view.ColorMode = ColorMode.Dark;
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                var dark = Color(view, "Flow.Background");
                Check($"thèmes clair et sombre ({light} / {dark})", light != null && dark != null && light != dark);
                view.ColorMode = ColorMode.Light;
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

                extra?.Invoke(view, Check);
                Check("aucune erreur de liaison", BindingErrors.Count == 0);
                lines.AddRange(BindingErrors.Select(e => "INFO  " + e));
            }
            catch (Exception ex)
            {
                failed = true;
                lines.Add("ERROR " + ex);
            }

            File.WriteAllLines(path, lines);
            Environment.ExitCode = failed ? 1 : 0;
            Application.Current.Shutdown(Environment.ExitCode);
        };
    }

    // Vrai si la zone est dans l'image et contient au moins trois couleurs (fond du node, bordure ou texte).
    private static bool Drawn(System.Windows.Media.Imaging.BitmapSource bitmap, int x, int y, int width, int height)
    {
        width = Math.Min(width, bitmap.PixelWidth - x);
        height = Math.Min(height, bitmap.PixelHeight - y);
        if (x < 0 || y < 0 || width <= 0 || height <= 0) return false;
        var pixels = new int[width * height];
        bitmap.CopyPixels(new Int32Rect(x, y, width, height), pixels, width * 4, 0);
        return pixels.Distinct().Take(3).Count() == 3;
    }

    private static string? Color(FlowView view, string key) => (view.ThemeBrush(key) as SolidColorBrush)?.Color.ToString();

    public static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var d in Find<T>(child)) yield return d;
        }
    }
}
