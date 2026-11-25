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
///     L'éditeur de schéma témoin, en mode contrôlé (l'état vit dans <see cref="SchemaStore" />) ou en MVVM (WPFLOW_MVVM=1,
///     voir <see cref="MvvmSchema" />). Avec WPFLOW_SCENARIOS=chemin.log, il exécute les scénarios à la vraie souris, dans les
///     deux modes, puis quitte avec un code de sortie.
/// </summary>
public partial class MainWindow : Window
{
    private SchemaStore? _store;
    private MvvmSchema? _mvvm;

    /// <summary>Le détenteur du schéma : le service (mode contrôlé) ou les ViewModels.</summary>
    internal ISchemaModel Store => (ISchemaModel?)_mvvm ?? _store!;

    internal FlowView View => Flow;
    internal ListBox Tables => Palette;
    internal string LogText => Log.Text;
    internal string MenuText => Menu.Text;
    internal List<string> SelectionMenus { get; } = [];
    internal List<string> DoubleClicks { get; } = [];

    /// <summary>La suite des évènements reçus, pour en vérifier l'ordre (le même qu'en Blazor).</summary>
    internal List<string> Events { get; } = [];
    internal int TemplateMenuClicks { get; private set; }

    /// <summary>Exceptions non gérées levées pendant les scénarios.</summary>
    internal List<string> Errors { get; } = [];

    public MainWindow() : this(mvvm: Environment.GetEnvironmentVariable("WPFLOW_MVVM") == "1")
    {
        if (Environment.GetEnvironmentVariable("WPFLOW_SCENARIOS") is { Length: > 0 } path)
            ContentRendered += async (_, _) =>
                await SelfTestReport.Run(this, path, TimeSpan.FromSeconds(540), log => Runner.Run(this, log));
        else
            ContentRendered += (_, _) => Open(Schemas.Shop());
    }

    internal MainWindow(bool mvvm)
    {
        InitializeComponent();
        Dispatcher.UnhandledException += OnUnhandled;
        Closed += (_, _) => Dispatcher.UnhandledException -= OnUnhandled;
        if (mvvm) WireMvvm();
        else WireControlled();
    }

    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Errors.Add(e.Exception.ToString());
        e.Handled = true;
    }

    private void WireControlled()
    {
        var store = _store = new SchemaStore();
        Flow.AutoApplyChanges = false;
        Flow.NodeChangesRequested += (_, e) => store.Apply(e.Changes);
        Flow.EdgeChangesRequested += (_, e) => store.Apply(e.Changes);
        Flow.ConnectRequested += (_, e) => store.Connect(e.Connection);
        Flow.IsValidConnection = store.CanConnect;
        store.Changed += () =>
        {
            Flow.Nodes = store.Nodes;
            Flow.Edges = store.Edges;
        };

        Flow.NodeClick += (_, e) => OnNodeClicked(e);
        Flow.NodeDoubleClick += (_, e) => OnNodeDoubleClicked(e);
        Flow.EdgeClick += (_, e) => OnEdgeClicked(e);
        Flow.PaneClick += (_, _) => OnPaneClicked();
        Flow.NodeContextMenuRequested += (_, e) => OnNodeMenu(e);
        Flow.EdgeContextMenuRequested += (_, e) => OnEdgeMenu(e);
        Flow.PaneContextMenuRequested += (_, _) => OnPaneMenu();
        Flow.SelectionContextMenuRequested += (_, _) => OnSelectionMenu();
        Flow.DropRequested += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.StringFormat) is not string name || store.Nodes.Any(n => n.Id == name)) return;
            store.Apply([new NodeAddChange(Schemas.TableNode(name, e.Position.X, e.Position.Y,
                new Table(name, [new Column("id", "int", true)])))]);
        };
    }

    // Tout passe par les sources, des commandes et les ViewModels : aucun évènement, aucune écriture des records.
    private void WireMvvm()
    {
        var schema = _mvvm = new MvvmSchema(Flow.ApplyNodeChanges);
        Flow.NodeMapping = new NodeMapping { SelectedPath = "IsSelected", TypePath = "Type", WidthPath = "Width", HeightPath = "Height" };
        Flow.EdgeMapping = new EdgeMapping
        {
            SourceHandlePath = "SourceColumn", TargetHandlePath = "TargetColumn", TypePath = "Type", SelectedPath = "IsSelected",
            LabelPath = "Label", DataPath = "Data", MarkerStartPath = "MarkerStart", MarkerEndPath = "MarkerEnd"
        };
        Flow.NodesSource = schema.Tables;
        Flow.EdgesSource = schema.Relations;
        Flow.NodeFactory = schema.NodeFactory;
        Flow.EdgeFactory = schema.EdgeFactory;

        Flow.CommandsReceiveEventArgs = true; // pour savoir quelle colonne est visée
        Flow.NodeClickCommand = new Command<ElementEventArgs>(OnNodeClicked);
        Flow.NodeDoubleClickCommand = new Command<ElementEventArgs>(OnNodeDoubleClicked);
        Flow.EdgeClickCommand = new Command<ElementEventArgs>(OnEdgeClicked);
        Flow.PaneClickCommand = new Command<ElementEventArgs>(_ => OnPaneClicked());
        Flow.NodeContextMenuCommand = new Command<ElementEventArgs>(OnNodeMenu);
        Flow.EdgeContextMenuCommand = new Command<ElementEventArgs>(OnEdgeMenu);
        Flow.PaneContextMenuCommand = new Command<ElementEventArgs>(_ => OnPaneMenu());
        Flow.SelectionContextMenuCommand = new Command<ElementEventArgs>(_ => OnSelectionMenu());

        Flow.ConnectCommand = new Command<Connection>(schema.Connect, schema.CanConnect);
        Flow.NodesDeleteCommand = new Command<IReadOnlyList<object>>(items => schema.DeleteTables(items.OfType<TableViewModel>().ToList()));
        Flow.EdgesDeleteCommand = new Command<IReadOnlyList<object>>(items => schema.DeleteRelations(items.OfType<RelationViewModel>().ToList()));
        Flow.DropCommand = new Command<FlowDropEventArgs>(e =>
        {
            if (e.Data.GetData(DataFormats.StringFormat) is string name) schema.Add(name, e.Position);
        });
    }

    private void OnNodeClicked(ElementEventArgs e) => Events.Add(Log.Text = "clic " + Describe(e));

    private void OnNodeDoubleClicked(ElementEventArgs e)
    {
        DoubleClicks.Add(Describe(e));
        Events.Add("double-clic " + Describe(e));
        if (e.TargetData is string && e.OriginalSource != null) StartRename(e.OriginalSource);
    }

    private void OnEdgeClicked(ElementEventArgs e) => Log.Text = "clic relation " + e.Id;
    private void OnPaneClicked() => Log.Text = "clic sur le fond";
    private void OnNodeMenu(ElementEventArgs e) => Events.Add(Menu.Text = (e.TargetData is string ? "colonne " : "table ") + Describe(e));
    private void OnEdgeMenu(ElementEventArgs e) => Menu.Text = "relation " + e.Id;
    private void OnPaneMenu() => Menu.Text = "fond";
    private void OnSelectionMenu()
    {
        var menu = "sélection de " + Store.Nodes.Count(n => n.Selected);
        SelectionMenus.Add(menu);
        Events.Add(menu);
    }

    private static string Describe(ElementEventArgs e) => e.TargetData is string column ? $"{e.Id}.{column}" : e.Id ?? "";

    /// <summary>
    ///     Un autre document. En mode contrôlé, la vue ne tient pas d'historique ; en MVVM si, et il ne doit pas ramener le
    ///     document précédent. La sélection vit dans le schéma.
    /// </summary>
    internal void Open(Schema schema)
    {
        Store.Load(schema);
        Flow.ClearHistory();
        FitLater();
    }

    internal string SaveDocument() => _mvvm == null
        ? Schemas.ToJson(Store.Nodes, Store.Edges, Flow.Viewport)
        : Flow.ToJson(n => ((TableViewModel)n.Data!).ToTable());

    internal void RestoreDocument(string json)
    {
        if (_mvvm == null)
        {
            Open(Schemas.FromJson(json));
            return;
        }

        Flow.LoadJson(json); // les fabriques recréent les ViewModels
        FitLater();
    }

    private void FitLater() => Dispatcher.BeginInvoke(DispatcherPriority.Background, () => Flow.FitView());

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
