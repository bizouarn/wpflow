using System.Text.Json;
using Wpflow.Core;

namespace Scenarios;

public sealed record Column(string Name, string Type, bool IsKey = false);

/// <summary>Une table : <c>Node.Data</c>. <paramref name="Rows" /> est mis à jour par une tâche de fond.</summary>
public sealed record Table(string Name, IReadOnlyList<Column> Columns, bool Collapsed = false, bool Locked = false, int Rows = 0);

/// <summary>Une relation : <c>Edge.Data</c>. Le handle d'une extrémité porte le nom de la colonne.</summary>
public sealed record Relation(string Cardinality);

public sealed record Schema(IReadOnlyList<Node> Nodes, IReadOnlyList<Edge> Edges);

public static class Schemas
{
    public const string TableType = "table";
    public const string RelationType = "relation";

    public static Node TableNode(string id, double x, double y, Table table) =>
        new(id, new XYPosition(x, y), TableType, table);

    public static Edge RelationEdge(string id, string from, string fromColumn, string to, string toColumn, string cardinality) =>
        new(id, from, to, fromColumn, toColumn, RelationType, Label: cardinality, Data: new Relation(cardinality),
            MarkerEnd: cardinality == "1-n" ? EdgeMarker.ArrowClosed : EdgeMarker.None,
            MarkerStart: cardinality == "n-n" ? EdgeMarker.Arrow : EdgeMarker.None);

    public static Column? ColumnOf(IReadOnlyList<Node> nodes, string table, string? column) =>
        nodes.FirstOrDefault(n => n.Id == table)?.Data is Table t ? t.Columns.FirstOrDefault(c => c.Name == column) : null;

    /// <summary>Deux tables différentes, deux colonnes de même type, pas de doublon.</summary>
    public static bool CanConnect(IReadOnlyList<Node> nodes, IReadOnlyList<Edge> edges, Connection c) =>
        c.Source != c.Target
        && ColumnOf(nodes, c.Source, c.SourceHandle) is { } from && ColumnOf(nodes, c.Target, c.TargetHandle) is { } to
        && from.Type == to.Type
        && !edges.Any(e => e.Source == c.Source && e.Target == c.Target && e.SourceHandle == c.SourceHandle && e.TargetHandle == c.TargetHandle);

    /// <summary>Boutique : trois tables et deux relations ; <c>audit</c> est verrouillée (le service refuse de la déplacer ou la supprimer).</summary>
    public static Schema Shop() => new(
    [
        TableNode("customers", 0, 0, new Table("customers", [new("id", "int", true), new("name", "text"), new("email", "text")])),
        TableNode("orders", 380, 60, new Table("orders", [new("id", "int", true), new("customer_id", "int"), new("note", "text")])),
        TableNode("lines", 760, 0, new Table("lines", [new("id", "int", true), new("order_id", "int"), new("label", "text")])),
        TableNode("audit", 380, 300, new Table("audit", [new("id", "int", true), new("author", "text")], Locked: true))
    ],
    [
        RelationEdge("r-orders", "customers", "id", "orders", "customer_id", "1-n"),
        RelationEdge("r-lines", "orders", "id", "lines", "order_id", "1-1")
    ]);

    public static Schema Library() => new(
    [
        TableNode("books", 1500, 900, new Table("books", [new("id", "int", true), new("title", "text")])),
        TableNode("authors", 1900, 1100, new Table("authors", [new("id", "int", true), new("book_id", "int")]))
    ],
    [RelationEdge("r-authors", "books", "id", "authors", "book_id", "1-n")]);

    /// <summary>Une table de 150 colonnes : sa seule mesure dépasse la taille maximale d'un message SignalR (32 Ko).</summary>
    public static Schema Wide(int columns = 150) => new(
    [
        TableNode("wide", 0, 0, new Table("wide", Enumerable.Range(0, columns).Select(i => new Column($"column_{i}", "int", i == 0)).ToList())),
        TableNode("other", 380, 0, new Table("other", [new("id", "int", true)]))
    ],
    [RelationEdge("r-wide", "wide", $"column_{columns - 1}", "other", "id", "1-n")]);

    /// <summary>Un grand schéma : tables en grille, chacune reliée aux deux précédentes.</summary>
    public static Schema Large(int tables = 200, int relations = 400)
    {
        var columns = (int)Math.Ceiling(Math.Sqrt(tables));
        var nodes = Enumerable.Range(0, tables).Select(i => TableNode($"t{i}", i % columns * 320, i / columns * 220,
            new Table($"t{i}", [new("id", "int", true), new("a", "int"), new("b", "int"), new("label", "text")]))).ToList();
        var edges = new List<Edge>(relations);
        for (var k = 1; edges.Count < relations; k++)
        for (var i = k; i < tables && edges.Count < relations; i++)
            edges.Add(RelationEdge($"r{edges.Count}", $"t{i - k}", "id", $"t{i}", k % 2 == 1 ? "a" : "b", "1-n"));
        return new Schema(nodes, edges);
    }

    public static string ToJson(IReadOnlyList<Node> nodes, IReadOnlyList<Edge> edges, Viewport viewport) =>
        GraphSerializer.ToJson(nodes, edges, viewport);

    /// <summary>Après <c>FromJson</c>, <c>Data</c> est un <see cref="JsonElement" /> : l'application le retype.</summary>
    public static Schema FromJson(string json)
    {
        var graph = GraphSerializer.FromJson(json);
        return new Schema(graph.Nodes.Select(n => n with { Data = Typed<Table>(n.Data) }).ToList(),
            graph.Edges.Select(e => e with { Data = Typed<Relation>(e.Data) }).ToList());
    }

    private static T Typed<T>(object? data) =>
        data is JsonElement element ? element.Deserialize<T>()! : throw new InvalidOperationException($"Data inattendu : {data}");
}

/// <summary>Le tracé d'une relation dépend de ses données : un seul type d'edge pour toutes les cardinalités.</summary>
public sealed class RelationPath : IEdgePathProvider
{
    public EdgePath GetPath(EdgeGeometry g) => (g.Edge?.Data as Relation)?.Cardinality switch
    {
        "1-1" => EdgePaths.SmoothStep(g.Source, g.SourcePosition, g.Target, g.TargetPosition),
        "n-n" => EdgePaths.Straight(g.Source, g.Target),
        _ => EdgePaths.Bezier(g.Source, g.SourcePosition, g.Target, g.TargetPosition)
    };
}
