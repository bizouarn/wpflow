using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using demo;
using Wpflow.Core;
using Wpflow.Wpf;

namespace Scenarios.Wpf;

/// <summary>
///     L'éditeur de schéma témoin : mode contrôlé (l'état vit dans <see cref="SchemaStore" />). Avec WPFLOW_SCENARIOS=chemin.log,
///     il exécute les scénarios à la vraie souris puis quitte avec un code de sortie.
/// </summary>
public partial class MainWindow : Window
{
    internal SchemaStore Store { get; } = new();
    internal FlowView View => Flow;
    internal ListBox Tables => Palette;
    internal string LogText => Log.Text;
    internal string MenuText => Menu.Text;
    internal List<string> SelectionMenus { get; } = [];
    internal List<string> DoubleClicks { get; } = [];
    internal int TemplateMenuClicks { get; private set; }

    /// <summary>Exceptions non gérées levées pendant les scénarios.</summary>
    internal List<string> Errors { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        Dispatcher.UnhandledException += (_, e) =>
        {
            Errors.Add(e.Exception.ToString());
            e.Handled = true;
        };
        Wire();

        if (Environment.GetEnvironmentVariable("WPFLOW_SCENARIOS") is { Length: > 0 } path)
            ContentRendered += async (_, _) =>
                await SelfTestReport.Run(this, path, TimeSpan.FromSeconds(300), log => Runner.Run(this, log));
        else
            ContentRendered += (_, _) => Open(Schemas.Shop());
    }

    private void Wire()
    {
        Flow.AutoApplyChanges = false;
        Flow.NodeChangesRequested += (_, e) => Store.Apply(e.Changes);
        Flow.EdgeChangesRequested += (_, e) => Store.Apply(e.Changes);
        Flow.ConnectRequested += (_, e) => Store.Connect(e.Connection);
        Flow.IsValidConnection = Store.CanConnect;
        Store.Changed += () =>
        {
            Flow.Nodes = Store.Nodes;
            Flow.Edges = Store.Edges;
        };

        Flow.NodeClick += (_, e) => Log.Text = "clic " + Describe(e);
        Flow.NodeDoubleClick += (_, e) =>
        {
            DoubleClicks.Add(Describe(e));
            if (e.TargetData is string && e.OriginalSource != null) StartRename(e.OriginalSource);
        };
        Flow.EdgeClick += (_, e) => Log.Text = "clic relation " + e.Id;
        Flow.PaneClick += (_, _) => Log.Text = "clic sur le fond";
        Flow.NodeContextMenuRequested += (_, e) => Menu.Text = (e.TargetData is string ? "colonne " : "table ") + Describe(e);
        Flow.EdgeContextMenuRequested += (_, e) => Menu.Text = "relation " + e.Id;
        Flow.PaneContextMenuRequested += (_, _) => Menu.Text = "fond";
        Flow.SelectionContextMenuRequested += (_, _) => SelectionMenus.Add("sélection de " + Store.Nodes.Count(n => n.Selected));
        Flow.DropRequested += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.StringFormat) is not string name || Store.Nodes.Any(n => n.Id == name)) return;
            Store.Apply([new NodeAddChange(Schemas.TableNode(name, e.Position.X, e.Position.Y,
                new Table(name, [new Column("id", "int", true)])))]);
        };
    }

    private static string Describe(ElementEventArgs e) => e.TargetData is string column ? $"{e.Id}.{column}" : e.Id ?? "";

    /// <summary>Un autre document : en mode contrôlé, la vue ne tient pas d'historique ; la sélection vit dans les nodes.</summary>
    internal void Open(Schema schema)
    {
        Store.Load(schema);
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () => Flow.FitView());
    }

    private void StartRename(DependencyObject source)
    {
        for (var d = source; d != null; d = VisualTreeHelper.GetParent(d))
        {
            if (d is not Grid { Tag: string name } row) continue;
            var box = row.Children.OfType<TextBox>().First();
            box.Text = name;
            box.Visibility = Visibility.Visible;
            // Après la fin du clic : la vue prend le focus au moment de la pression.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                box.Focus();
                box.SelectAll();
            });
            return;
        }
    }

    private void OnRenameKey(object sender, KeyEventArgs e)
    {
        var box = (TextBox)sender;
        if (e.Key is not (Key.Enter or Key.Escape)) return;
        e.Handled = true;
        box.Visibility = Visibility.Collapsed;
        if (e.Key == Key.Enter && box.Parent is Grid { Tag: string old } && NodeOf(box) is { } node)
            Store.RenameColumn(node.Id, old, box.Text);
        Flow.Focus();
    }

    private static Node? NodeOf(DependencyObject element)
    {
        for (var d = element; d != null; d = VisualTreeHelper.GetParent(d))
            if (d is NodeHost host) return host.Node;
        return null;
    }

    private void OnLabelClick(object sender, RoutedEventArgs e) => Log.Text = "label " + ((Button)sender).Tag;

    private void OnTemplateMenu(object sender, RoutedEventArgs e) => TemplateMenuClicks++;

    private void OnWidgetButton(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        button.Content = "clics " + (int.Parse(((string)button.Content).Split(' ')[1]) + 1);
    }

    // L'élément PRESSÉ : la sélection de la liste suit la souris pendant que le bouton reste enfoncé.
    private string? _dragged;

    private void OnPaletteDown(object sender, MouseButtonEventArgs e) =>
        _dragged = (ItemsControl.ContainerFromElement(Palette, (DependencyObject)e.OriginalSource) as ListBoxItem)?.Content as string;

    private void OnPaletteMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragged is not { } name) return;
        _dragged = null;
        DragDrop.DoDragDrop(Palette, name, DragDropEffects.Copy);
    }
}
