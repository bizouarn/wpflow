using DocsSite.Examples;

namespace DocsSite.Components;

/// <param name="Docs">Pages of <c>doc/</c> that the example illustrates.</param>
public sealed record Example(string Id, string Title, string Summary, Type Component, params string[] Docs)
{
    /// <summary>The code of the compiled file (embedded by the project), so always the code that runs.</summary>
    public string Source
    {
        get
        {
            using var reader = new StreamReader(
                typeof(Example).Assembly.GetManifestResourceStream($"source/{Component.Name}.razor")!);
            return reader.ReadToEnd().ReplaceLineEndings("\n").TrimEnd();
        }
    }
}

/// <summary>The interactive examples: one component per file in <c>Examples/</c>.</summary>
public static class ExampleCatalog
{
    public static readonly IReadOnlyList<Example> All =
    [
        new("premier-graphe", "First graph", "Two nodes, an animated link, the minimap and the controls.",
            typeof(FirstGraph), "doc/learn/01-quickstart.md", "doc/learn/02-concepts.md"),
        new("etat-controle", "State and changes",
            "Controlled mode: each interaction produces changes that the application applies or refuses.",
            typeof(ControlledState), "doc/learn/03-building-a-flow.md", "doc/reference/changes.md"),
        new("nodes-personnalises", "Custom nodes",
            "Razor templates with real fields, resizing, rotation and a toolbar.",
            typeof(CustomNodes), "doc/learn/04-custom-nodes.md", "doc/reference/components.md"),
        new("edges", "Edge types", "Curves, right angles, arrows, labels, animation and a custom path.",
            typeof(EdgeGallery), "doc/learn/05-edges.md", "doc/learn/14-customization.md"),
        new("sous-flows", "Sub-flows and groups", "Child nodes that follow their group and stay inside it.",
            typeof(SubFlows), "doc/learn/06-sub-flows.md"),
        new("disposition", "Automatic layout", "Shuffle, then let GraphLayout arrange the graph in layers.",
            typeof(AutoLayout), "doc/learn/07-layouting.md", "doc/reference/utils.md"),
        new("theme", "Theme", "Light, dark or your own colours, through CSS variables; three background patterns.",
            typeof(Theming), "doc/learn/08-theming.md", "doc/reference/theme-keys.md"),
        new("connexions", "Connections and validation",
            "Strict or loose mode, two-click connection, a validation rule and reconnection.",
            typeof(Connecting), "doc/learn/09-interactions.md"),
        new("palette", "Drag and drop", "A palette whose items are dropped onto the graph.",
            typeof(DragAndDrop), "doc/learn/09-interactions.md"),
        new("calcul", "Computing with a flow", "Values flow along the links and are recomputed at each change.",
            typeof(Computing), "doc/learn/10-computing-flows.md"),
        new("performance", "Large graphs", "Thousands of nodes, with or without virtualisation.",
            typeof(LargeGraph), "doc/learn/11-performance-accessibility.md"),
        new("json", "Saving and JSON", "Export the graph in React Flow format, edit it, reload it.",
            typeof(JsonSave), "doc/learn/12-serialization.md", "doc/reference/types.md"),
        new("mvvm", "MVVM", "The view works directly on your ViewModels.",
            typeof(MvvmBinding), "doc/learn/13-mvvm.md")
    ];

    public static Example? Find(string id) => All.FirstOrDefault(e => e.Id == id);

    public static IReadOnlyList<Example> ForDoc(string file) => All.Where(e => e.Docs.Contains(file)).ToList();
}
