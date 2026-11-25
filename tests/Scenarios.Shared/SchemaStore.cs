using Wpflow.Core;

namespace Scenarios;

/// <summary>
///     Le service qui détient l'état (mode contrôlé) : la vue propose des changes, lui les applique ou les refuse.
///     <see cref="Changed" /> signale toute modification, y compris celles venues d'ailleurs que la vue.
/// </summary>
public sealed class SchemaStore
{
    private readonly List<string> _refused = [];

    public IReadOnlyList<Node> Nodes { get; private set; } = [];
    public IReadOnlyList<Edge> Edges { get; private set; } = [];

    /// <summary>Les refus, du plus ancien au plus récent.</summary>
    public IReadOnlyList<string> Refused => _refused;

    public event Action? Changed;

    public void Load(Schema schema)
    {
        Nodes = schema.Nodes;
        Edges = schema.Edges;
        _refused.Clear();
        Changed?.Invoke();
    }

    public Table Table(string id) => (Table)Nodes.First(n => n.Id == id).Data!;

    private bool IsLocked(string id) => Nodes.FirstOrDefault(n => n.Id == id)?.Data is Table { Locked: true };

    public void Apply(IReadOnlyList<NodeChange> changes)
    {
        var accepted = new List<NodeChange>(changes.Count);
        foreach (var change in changes)
            switch (change)
            {
                case NodePositionChange p when IsLocked(p.Id):
                    Refuse($"déplacement de {p.Id}");
                    break;
                case NodeRemoveChange r when IsLocked(r.Id):
                    Refuse($"suppression de {r.Id}");
                    break;
                default:
                    accepted.Add(change);
                    break;
            }

        Nodes = Changes.Apply(accepted, Nodes);
        Changed?.Invoke();
    }

    public void Apply(IReadOnlyList<EdgeChange> changes)
    {
        // Les relations d'une table verrouillée ne partent pas avec une suppression refusée.
        var accepted = changes.Where(c => c is not EdgeRemoveChange r ||
                                          Edges.FirstOrDefault(e => e.Id == r.Id) is not { } e || !(IsLocked(e.Source) || IsLocked(e.Target))).ToList();
        if (accepted.Count != changes.Count) Refuse("suppression d'une relation verrouillée");
        Edges = Changes.Apply(accepted, Edges);
        Changed?.Invoke();
    }

    private void Refuse(string what)
    {
        if (_refused.Count == 0 || _refused[^1] != what) _refused.Add(what);
    }

    private Column? Column(string table, string? column) =>
        Nodes.FirstOrDefault(n => n.Id == table)?.Data is Table t ? t.Columns.FirstOrDefault(c => c.Name == column) : null;

    /// <summary>Deux tables différentes, deux colonnes de même type, pas de doublon.</summary>
    public bool CanConnect(Connection c) =>
        c.Source != c.Target
        && Column(c.Source, c.SourceHandle) is { } from && Column(c.Target, c.TargetHandle) is { } to && from.Type == to.Type
        && !Edges.Any(e => e.Source == c.Source && e.Target == c.Target && e.SourceHandle == c.SourceHandle && e.TargetHandle == c.TargetHandle);

    public void Connect(Connection c)
    {
        if (!CanConnect(c)) return;
        var id = $"r-{c.Source}.{c.SourceHandle}-{c.Target}.{c.TargetHandle}";
        Edges = [..Edges, Schemas.RelationEdge(id, c.Source, c.SourceHandle!, c.Target, c.TargetHandle!, "1-n")];
        Changed?.Invoke();
    }

    private void Update(string id, Func<Table, Table> update)
    {
        Nodes = Nodes.Select(n => n.Id == id ? n with { Data = update((Table)n.Data!) } : n).ToList();
        Changed?.Invoke();
    }

    public void AddColumn(string table, Column column, int index) => Update(table, t =>
    {
        var columns = t.Columns.ToList();
        columns.Insert(Math.Clamp(index, 0, columns.Count), column);
        return t with { Columns = columns };
    });

    public void RemoveColumn(string table, string column)
    {
        Edges = Edges.Where(e => !(e.Source == table && e.SourceHandle == column) && !(e.Target == table && e.TargetHandle == column)).ToList();
        Update(table, t => t with { Columns = t.Columns.Where(c => c.Name != column).ToList() });
    }

    public void MoveColumn(string table, string column, int delta) => Update(table, t =>
    {
        var columns = t.Columns.ToList();
        var from = columns.FindIndex(c => c.Name == column);
        if (from < 0) return t;
        var item = columns[from];
        columns.RemoveAt(from);
        columns.Insert(Math.Clamp(from + delta, 0, columns.Count), item);
        return t with { Columns = columns };
    });

    public void ToggleCollapsed(string table) => Update(table, t => t with { Collapsed = !t.Collapsed });

    public void RenameColumn(string table, string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || oldName == newName || Column(table, newName) != null) return;
        Edges = Edges.Select(e =>
        {
            if (e.Source == table && e.SourceHandle == oldName) e = e with { SourceHandle = newName };
            if (e.Target == table && e.TargetHandle == oldName) e = e with { TargetHandle = newName };
            return e;
        }).ToList();
        Update(table, t => t with { Columns = t.Columns.Select(c => c.Name == oldName ? c with { Name = newName } : c).ToList() });
    }

    /// <summary>Mise à jour venue d'une minuterie : le nombre de lignes d'une table change.</summary>
    public void Tick(string table) => Update(table, t => t with { Rows = t.Rows + 1 });
}
