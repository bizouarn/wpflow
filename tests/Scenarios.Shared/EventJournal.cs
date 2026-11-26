using Wpflow.Core;

namespace Scenarios;

/// <summary>
///     La suite des évènements reçus par une application témoin (ROADMAP-TESTS, lot 3). Une entrée est « Nom » ou
///     « Nom sujet » ; les noms sont ceux de <see cref="EventCatalog" />, communs aux deux vues.
/// </summary>
public sealed class EventJournal
{
    private readonly List<string> _entries = [];
    private readonly object _gate = new();

    public event Action? Changed;

    public void Add(string name, string? subject = null)
    {
        lock (_gate) _entries.Add(string.IsNullOrEmpty(subject) ? name : name + " " + subject);
        Changed?.Invoke();
    }

    public void Clear()
    {
        lock (_gate) _entries.Clear();
        Changed?.Invoke();
    }

    public IReadOnlyList<string> Entries
    {
        get
        {
            lock (_gate) return _entries.ToList();
        }
    }

    // ---------------- les sujets, écrits de la même façon des deux côtés ----------------

    public static string Ids(IEnumerable<string> ids) => string.Join(",", ids.Order(StringComparer.Ordinal));

    /// <summary>« nodes|edges ».</summary>
    public static string Selection(IEnumerable<Node> nodes, IEnumerable<Edge> edges) =>
        Ids(nodes.Select(n => n.Id)) + "|" + Ids(edges.Select(e => e.Id));

    public static string Handle(string node, string? handle) => $"{node}.{handle}";

    public static string Link(Connection c) => $"{Handle(c.Source, c.SourceHandle)}>{Handle(c.Target, c.TargetHandle)}";

    /// <summary>La fin d'un fil : son départ, puis la connexion faite ou « - ».</summary>
    public static string End(string node, string? handle, Connection? connection) =>
        Handle(node, handle) + " " + (connection == null ? "-" : Link(connection));
}
