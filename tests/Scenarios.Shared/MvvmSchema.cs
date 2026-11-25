using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Wpflow.Core;

namespace Scenarios;

public abstract class Notifying : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Raise(name);
    }
}

/// <summary>Le même schéma en MVVM : le ViewModel sert de <c>Node.Data</c>, avec les mêmes noms que <see cref="Table" />.</summary>
public sealed class TableViewModel : Notifying
{
    private readonly Action<string>? _refused;
    private double _x, _y, _width = double.NaN, _height = double.NaN;
    private int _rows;
    private bool _collapsed, _selected;

    public TableViewModel(string id, double x, double y, IEnumerable<Column> columns, bool locked = false, Action<string>? refused = null)
    {
        Id = id;
        _x = x;
        _y = y;
        Locked = locked;
        _refused = refused;
        Columns = new ObservableCollection<Column>(columns);
        // WPF suit la collection par ses bindings ; Blazor a besoin que le ViewModel le signale.
        Columns.CollectionChanged += (_, _) => Raise(nameof(Columns));
    }

    public string Id { get; }
    public string Name => Id;
    public string Type => Schemas.TableType;
    public bool Locked { get; }
    public ObservableCollection<Column> Columns { get; }
    public double X { get => _x; set => Move(ref _x, value); }
    public double Y { get => _y; set => Move(ref _y, value); }
    public double Width { get => _width; set => Set(ref _width, value); }
    public double Height { get => _height; set => Set(ref _height, value); }
    public int Rows { get => _rows; set => Set(ref _rows, value); }
    public bool Collapsed { get => _collapsed; set => Set(ref _collapsed, value); }
    public bool IsSelected { get => _selected; set => Set(ref _selected, value); }

    public Table ToTable() => new(Name, Columns.ToList(), Collapsed, Locked, Rows);

    // Une table verrouillée refuse de bouger et le signale : la vue doit alors reprendre la position du ViewModel.
    private void Move(ref double field, double value, [CallerMemberName] string? name = null)
    {
        if (!Locked)
        {
            Set(ref field, value, name);
            return;
        }

        _refused?.Invoke("déplacement de " + Id);
        Raise(name);
    }
}

public sealed class RelationViewModel(string id, string source, string sourceColumn, string target, string targetColumn,
    string cardinality = "1-n") : Notifying
{
    private string _sourceColumn = sourceColumn, _targetColumn = targetColumn, _cardinality = cardinality;
    private bool _selected;

    public string Id { get; } = id;
    public string Source { get; } = source;
    public string SourceColumn { get => _sourceColumn; set => Set(ref _sourceColumn, value); }
    public string Target { get; } = target;
    public string TargetColumn { get => _targetColumn; set => Set(ref _targetColumn, value); }
    public string Type => Schemas.RelationType;
    public bool IsSelected { get => _selected; set => Set(ref _selected, value); }

    public string Cardinality
    {
        get => _cardinality;
        set
        {
            Set(ref _cardinality, value);
            foreach (var name in new[] { nameof(Label), nameof(Data), nameof(MarkerStart), nameof(MarkerEnd) }) Raise(name);
        }
    }

    // Ce que la vue lit pour dessiner la relation : EdgeMapping.LabelPath, DataPath, MarkerStartPath, MarkerEndPath.
    public string Label => Cardinality;
    public Relation Data => new(Cardinality);
    public EdgeMarker MarkerStart => ToEdge().MarkerStart;
    public EdgeMarker MarkerEnd => ToEdge().MarkerEnd;

    public Edge ToEdge() =>
        Schemas.RelationEdge(Id, Source, SourceColumn, Target, TargetColumn, Cardinality) with { Selected = IsSelected };
}

/// <summary>Le schéma tenu par des ViewModels : la vue y écrit, et lit <see cref="Nodes" /> pour les vérifications.</summary>
public sealed class MvvmSchema(Action<IReadOnlyList<NodeChange>> apply) : ISchemaModel
{
    private readonly List<string> _refused = [];

    public ObservableCollection<TableViewModel> Tables { get; } = [];
    public ObservableCollection<RelationViewModel> Relations { get; } = [];

    /// <summary>Quelque chose a changé dans le schéma : l'application Blazor redessine sa barre d'état.</summary>
    public event Action? Changed;

    private bool _observed;

    /// <summary>Surveille les collections et les ViewModels (une fois), pour <see cref="Changed" />.</summary>
    public void Observe()
    {
        if (_observed) return;
        _observed = true;

        void Watch(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            foreach (var added in e.NewItems?.OfType<INotifyPropertyChanged>() ?? [])
                added.PropertyChanged += (_, _) => Changed?.Invoke();
            Changed?.Invoke();
        }

        Tables.CollectionChanged += (_, e) => Watch(e);
        Relations.CollectionChanged += (_, e) => Watch(e);
    }

    /// <summary>Pour une application qui refuse en bloc : une table verrouillée ne part pas, ni ses relations.</summary>
    public bool MayDelete(IReadOnlyList<Node> nodes, IReadOnlyList<Edge> edges)
    {
        var locked = nodes.FirstOrDefault(n => IsLocked(n.Id));
        if (locked != null) Refuse("suppression de " + locked.Id);
        else if (edges.Any(e => IsLocked(e.Source) || IsLocked(e.Target))) Refuse("suppression d'une relation verrouillée");
        else return true;
        return false;
    }

    // Construits depuis les ViewModels : les vérifications portent sur eux, pas sur ce que la vue croit.
    public IReadOnlyList<Node> Nodes => Tables
        .Select(t => Schemas.TableNode(t.Id, t.X, t.Y, t.ToTable()) with { Selected = t.IsSelected }).ToList();

    public IReadOnlyList<Edge> Edges => Relations.Select(r => r.ToEdge()).ToList();
    public IReadOnlyList<string> Refused => _refused;

    private TableViewModel Of(string id) => Tables.First(t => t.Id == id);
    public Table Table(string id) => Of(id).ToTable();

    private void Refuse(string what)
    {
        if (_refused.Count == 0 || _refused[^1] != what) _refused.Add(what);
        Changed?.Invoke();
    }

    private bool IsLocked(string id) => Tables.FirstOrDefault(t => t.Id == id)?.Locked == true;

    private TableViewModel Create(string id, XYPosition at, Table table) =>
        new(id, at.X, at.Y, table.Columns, table.Locked, Refuse) { Rows = table.Rows, Collapsed = table.Collapsed };

    private static RelationViewModel Create(Edge e, Relation relation) =>
        new(e.Id, e.Source, e.SourceHandle!, e.Target, e.TargetHandle!, relation.Cardinality);

    public void Load(Schema schema)
    {
        _refused.Clear();
        Relations.Clear();
        Tables.Clear();
        foreach (var n in schema.Nodes) Tables.Add(Create(n.Id, n.Position, (Table)n.Data!));
        foreach (var e in schema.Edges) Relations.Add(Create(e, (Relation)e.Data!));
    }

    /// <summary>Les fabriques de <see cref="FlowView.LoadJson" /> : <c>Data</c> revient en <see cref="JsonElement" />.</summary>
    public object NodeFactory(Node n) =>
        Create(n.Id, n.Position, n.Data is JsonElement data ? data.Deserialize<Table>()! : (Table)n.Data!);

    public object EdgeFactory(Edge e) => Create(e, e.Data is JsonElement data ? data.Deserialize<Relation>()! : (Relation)e.Data!);

    public void Apply(IReadOnlyList<NodeChange> changes) => apply(changes);

    public bool CanConnect(Connection c) => Schemas.CanConnect(Nodes, Edges, c);

    public void Connect(Connection c) => Relations.Add(new RelationViewModel(
        $"r-{c.Source}.{c.SourceHandle}-{c.Target}.{c.TargetHandle}", c.Source, c.SourceHandle!, c.Target, c.TargetHandle!));

    public void Add(string name, XYPosition at)
    {
        if (Tables.All(t => t.Id != name)) Tables.Add(Create(name, at, new Table(name, [new Column("id", "int", true)])));
    }

    /// <summary>La commande de suppression remplace celle de la vue : les tables verrouillées restent.</summary>
    public void DeleteTables(IReadOnlyList<TableViewModel> tables)
    {
        foreach (var table in tables)
        {
            if (table.Locked)
            {
                Refuse("suppression de " + table.Id);
                continue;
            }

            foreach (var r in Relations.Where(r => r.Source == table.Id || r.Target == table.Id).ToList()) Relations.Remove(r);
            Tables.Remove(table);
        }
    }

    public void DeleteRelations(IReadOnlyList<RelationViewModel> relations)
    {
        foreach (var relation in relations)
            if (IsLocked(relation.Source) || IsLocked(relation.Target)) Refuse("suppression d'une relation verrouillée");
            else Relations.Remove(relation);
    }

    public void AddColumn(string table, Column column, int index) =>
        Of(table).Columns.Insert(Math.Clamp(index, 0, Of(table).Columns.Count), column);

    public void RemoveColumn(string table, string column)
    {
        foreach (var r in Relations.Where(r => r.Source == table && r.SourceColumn == column ||
                                               r.Target == table && r.TargetColumn == column).ToList())
            Relations.Remove(r);
        var columns = Of(table).Columns;
        columns.Remove(columns.First(c => c.Name == column));
    }

    public void MoveColumn(string table, string column, int delta)
    {
        var columns = Of(table).Columns;
        var from = columns.IndexOf(columns.First(c => c.Name == column));
        columns.Move(from, Math.Clamp(from + delta, 0, columns.Count - 1));
    }

    public void ToggleCollapsed(string table) => Of(table).Collapsed = !Of(table).Collapsed;

    public void RenameColumn(string table, string oldName, string newName)
    {
        var columns = Of(table).Columns;
        if (string.IsNullOrWhiteSpace(newName) || oldName == newName || columns.Any(c => c.Name == newName)) return;
        var index = columns.IndexOf(columns.First(c => c.Name == oldName));
        columns[index] = columns[index] with { Name = newName };
        foreach (var r in Relations)
        {
            if (r.Source == table && r.SourceColumn == oldName) r.SourceColumn = newName;
            if (r.Target == table && r.TargetColumn == oldName) r.TargetColumn = newName;
        }
    }

    public void SetCardinality(string relation, string cardinality) =>
        Relations.First(r => r.Id == relation).Cardinality = cardinality;

    public void Tick(string table) => Of(table).Rows++;
}

