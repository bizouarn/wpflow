using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpflow.Core;
using Wpflow.Wpf;

namespace demo;

/// <summary>Petite commande réutilisable pour les boutons des nodes.</summary>
public sealed class RelayCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
}

/// <summary>Données d'un node « tool » : titre et commandes de sa barre d'outils.</summary>
public sealed class ToolNodeData
{
    public string Title { get; init; } = "";
    public ICommand? DeleteCommand { get; set; }
    public ICommand? DuplicateCommand { get; set; }
}

public partial class ExamplesWindow : Window
{
    private int _counter = 100;

    public ExamplesWindow()
    {
        InitializeComponent();
        BuildEdgeTypes();
        BuildGroups();
        BuildTools();
        BuildPalette();
        BuildLayout();
        BuildOptions();

        if (Environment.GetEnvironmentVariable("WPFLOW_SCREENSHOT") is { Length: > 0 } path &&
            Environment.GetEnvironmentVariable("WPFLOW_EXAMPLES") == "selftest")
        {
            ContentRendered += async (_, _) =>
            {
                await Task.Delay(1500);
                await SelfTestReport.Run(this, System.IO.Path.ChangeExtension(path, ".log"), TimeSpan.FromSeconds(240),
                    log => ExamplesSelfTest.Run(this, path, log));
            };
        }
    }

    internal TabControl TabsControl => Tabs;
    internal FlowView Edges => EdgesFlow;
    internal FlowView Groups => GroupsFlow;
    internal FlowView Tools => ToolFlow;
    internal FlowView Palette => PaletteFlow;
    internal FlowView Layout => LayoutFlow;
    internal FlowView Options => OptionsFlow;
    internal FlowView Perf => PerfFlow;
    internal CheckBox Dark => DarkBox;
    internal CheckBox Loose => LooseBox;
    internal CheckBox Extent => ExtentBox;
    internal CheckBox NoSelf => NoSelfBox;
    internal TextBox Json => JsonBox;
    internal TextBlock Status => PerfStatus;

    internal void Capture(string path)
    {
        var content = (FrameworkElement)Content;
        var bmp = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(path);
        encoder.Save(fs);
    }

    // ---------- Types d'edges ----------

    private void BuildEdgeTypes()
    {
        var nodes = new List<Node>();
        var edges = new List<Edge>();
        var types = new[] { "default", "straight", "step", "smoothstep" };
        for (var i = 0; i < types.Length; i++)
        {
            var y = i * 110;
            nodes.Add(new Node($"s{i}", new XYPosition(0, y), "input", types[i]));
            nodes.Add(new Node($"t{i}", new XYPosition(380, y + 20), "output", $"{types[i]} (target)"));
            edges.Add(new Edge($"e{i}", $"s{i}", $"t{i}", Type: types[i], Label: types[i],
                Animated: i == 0, MarkerEnd: i % 2 == 0 ? EdgeMarker.ArrowClosed : EdgeMarker.Arrow));
        }

        // Handles latéraux : un edge qui part du bas pour arriver à gauche.
        nodes.Add(new Node("a", new XYPosition(120, 460), "default", "Bottom to left"));
        nodes.Add(new Node("b", new XYPosition(380, 520), "output", "Target"));
        edges.Add(new Edge("e-ab", "a", "b", Type: "smoothstep"));
        EdgesFlow.Nodes = nodes;
        EdgesFlow.Edges = edges;
    }

    // ---------- Groupes ----------

    private void BuildGroups()
    {
        GroupsFlow.Nodes =
        [
            new Node("g1", new XYPosition(0, 0), "group", "Group A", FixedWidth: 360, FixedHeight: 230),
            new Node("c1", new XYPosition(20, 40), "default", "Child 1", ParentId: "g1", ExtentParent: true),
            new Node("c2", new XYPosition(180, 130), "default", "Child 2", ParentId: "g1", ExtentParent: true),
            new Node("free", new XYPosition(520, 70), "output", "Outside the group")
        ];
        GroupsFlow.Edges =
        [
            new Edge("g-e1", "c1", "c2", Animated: true),
            new Edge("g-e2", "c2", "free")
        ];
    }

    // ---------- Toolbar / resizer ----------

    private void BuildTools()
    {
        ToolFlow.Nodes = [MakeTool("t1", 0, 0, "Node with tools"), MakeTool("t2", 330, 60, "Another node")];
        ToolFlow.Edges = [new Edge("t-e", "t1", "t2", MarkerEnd: EdgeMarker.ArrowClosed)];
    }

    private Node MakeTool(string id, double x, double y, string title)
    {
        var data = new ToolNodeData { Title = title };
        data.DeleteCommand = new RelayCommand(() =>
        {
            ToolFlow.Nodes = ToolFlow.Nodes.Where(n => n.Id != id).ToList();
            ToolFlow.Edges = ToolFlow.Edges.Where(e => e.Source != id && e.Target != id).ToList();
        });
        data.DuplicateCommand = new RelayCommand(() =>
        {
            var source = ToolFlow.Nodes.First(n => n.Id == id);
            var copy = MakeTool($"t{++_counter}", source.Position.X + 30, source.Position.Y + 110, title + " (copy)");
            ToolFlow.Nodes = ToolFlow.Nodes.Append(copy with { FixedWidth = source.FixedWidth, FixedHeight = source.FixedHeight }).ToList();
        });
        return new Node(id, new XYPosition(x, y), "tool", data, FixedWidth: 200, FixedHeight: 90);
    }

    // ---------- Palette (glisser-déposer) ----------

    private void BuildPalette()
    {
        PaletteFlow.Nodes = [new Node("p0", new XYPosition(120, 120), "input", "Already here")];
        PaletteFlow.Viewport = new Viewport(100, 80, 1);
        PaletteFlow.DropRequested += (_, e) =>
        {
            if (e.Data.GetData("wpflow-node-type") is not string type) return;
            var node = new Node($"p{++_counter}", e.Position - new XYPosition(75, 20), type, $"{type} node");
            PaletteFlow.Nodes = PaletteFlow.Nodes.Append(node).ToList();
        };
    }

    private void OnPaletteMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not Border { Tag: string type } b) return;
        DragDrop.DoDragDrop(b, new DataObject("wpflow-node-type", type), DragDropEffects.Copy);
    }

    // ---------- Disposition automatique ----------

    private void BuildLayout()
    {
        var rng = new Random(5);
        string[] ids = ["a", "b", "c", "d", "e", "f", "g"];
        LayoutFlow.Nodes = ids.Select(id => new Node(id, new XYPosition(rng.Next(0, 600), rng.Next(0, 400)),
            id == "a" ? "input" : "default", $"Node {id.ToUpper()}")).ToList();
        LayoutFlow.Edges =
        [
            new Edge("l1", "a", "b", Type: "smoothstep"), new Edge("l2", "a", "c", Type: "smoothstep"),
            new Edge("l3", "b", "d", Type: "smoothstep"), new Edge("l4", "c", "d", Type: "smoothstep"),
            new Edge("l5", "c", "e", Type: "smoothstep"), new Edge("l6", "d", "f", Type: "smoothstep"),
            new Edge("l7", "e", "g", Type: "smoothstep")
        ];
        LayoutFlow.FitViewOnInit = true;
    }

    internal void OnAutoLayout(object? sender, RoutedEventArgs? e)
    {
        var positions = GraphLayout.LayeredLeftToRight(LayoutFlow.Nodes, LayoutFlow.Edges, 90, 30, 150, 40);
        // Les edges partent du bas vers le haut : en disposition horizontale on passe par des "step" latéraux.
        LayoutFlow.Nodes = LayoutFlow.Nodes.Select(n => n with { Position = positions[n.Id] }).ToList();
        Dispatcher.BeginInvoke(() => LayoutFlow.FitView(), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnShuffle(object sender, RoutedEventArgs e)
    {
        var rng = new Random();
        LayoutFlow.Nodes = LayoutFlow.Nodes.Select(n => n with { Position = new XYPosition(rng.Next(0, 600), rng.Next(0, 400)) })
            .ToList();
        Dispatcher.BeginInvoke(() => LayoutFlow.FitView(), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    // ---------- Options et JSON ----------

    private void BuildOptions()
    {
        string[] names = ["A", "B", "C", "D", "E"];
        OptionsFlow.Nodes = names.Select((n, i) => new Node($"o{n}", new XYPosition(i % 3 * 220, i / 3 * 150),
            i == 0 ? "input" : "default", $"Node {n}")).ToList();
        OptionsFlow.Edges = [new Edge("o-1", "oA", "oB", MarkerEnd: EdgeMarker.ArrowClosed, MarkerStart: EdgeMarker.Arrow), new Edge("o-2", "oB", "oC")];
    }

    internal void OnOptionsChanged(object? sender, RoutedEventArgs? e)
    {
        var f = OptionsFlow;
        f.ColorMode = DarkBox.IsChecked == true ? ColorMode.Dark : ColorMode.Light;
        f.ConnectionMode = LooseBox.IsChecked == true ? ConnectionMode.Loose : ConnectionMode.Strict;
        f.SnapToGrid = SnapBox.IsChecked == true;
        f.SnapGrid = 20;
        f.IsLocked = LockBox.IsChecked == true;
        f.TranslateExtent = ExtentBox.IsChecked == true ? new Wpflow.Core.Rect(-100, -100, 800, 500) : null;
        f.IsValidConnection = NoSelfBox.IsChecked == true ? c => c.Target != "oD" && c.Source != "oD" : null;
    }

    internal void OnExport(object? sender, RoutedEventArgs? e) =>
        JsonBox.Text = GraphSerializer.ToJson(OptionsFlow.Nodes.Select(n => n with { Data = n.Data?.ToString() }),
            OptionsFlow.Edges, OptionsFlow.Viewport);

    internal void OnImport(object? sender, RoutedEventArgs? e)
    {
        try
        {
            var graph = GraphSerializer.FromJson(JsonBox.Text);
            // Data revient sous forme de JsonElement : on la remet en texte pour le node par défaut.
            OptionsFlow.Nodes = graph.Nodes
                .Select(n => n with { Data = n.Data is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } j ? j.GetString() : n.Data })
                .ToList();
            OptionsFlow.Edges = graph.Edges;
            OptionsFlow.Viewport = graph.Viewport;
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or ArgumentException)
        {
            MessageBox.Show(this, ex.Message, "Invalid JSON");
        }
    }

    // ---------- Performance ----------

    internal void OnLoadMany(object? sender, RoutedEventArgs? e)
    {
        const int count = 3000;
        var cols = (int)Math.Ceiling(Math.Sqrt(count));
        PerfFlow.Edges = Enumerable.Range(1, count - 1).Select(i => new Edge($"pe{i}", $"pn{i - 1}", $"pn{i}")).ToList();
        PerfFlow.Nodes = Enumerable.Range(0, count)
            .Select(i => new Node($"pn{i}", new XYPosition(i % cols * 200, i / cols * 80), "default", $"Node {i}")).ToList();
        PerfFlow.Viewport = new Viewport(20, 20, 0.6);
        Dispatcher.BeginInvoke(UpdatePerfStatus, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void OnVirtChanged(object sender, RoutedEventArgs e)
    {
        PerfFlow.OnlyRenderVisibleElements = VirtBox.IsChecked == true;
        Dispatcher.BeginInvoke(UpdatePerfStatus, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    internal int CountVisibleNodes()
    {
        var count = 0;
        void Walk(DependencyObject d)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
            {
                var c = VisualTreeHelper.GetChild(d, i);
                if (c is NodeHost h && h.Visibility == Visibility.Visible) count++;
                else Walk(c);
            }
        }

        Walk(PerfFlow);
        return count;
    }

    internal void UpdatePerfStatus() =>
        PerfStatus.Text = $"{PerfFlow.Nodes.Count} nodes, {CountVisibleNodes()} rendered";
}
