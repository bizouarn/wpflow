using DocsSite.Examples;

namespace DocsSite.Components;

/// <param name="Docs">Pages de <c>doc/</c> que l'exemple illustre.</param>
public sealed record Example(string Id, string Title, string Summary, Type Component, params string[] Docs)
{
    /// <summary>Le code du fichier compilé (embarqué par le projet), donc toujours celui qui s'exécute.</summary>
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

/// <summary>Les exemples interactifs : un composant par fichier de <c>Examples/</c>.</summary>
public static class ExampleCatalog
{
    public static readonly IReadOnlyList<Example> All =
    [
        new("premier-graphe", "Premier graphe", "Deux nodes, un lien animé, la minimap et les contrôles.",
            typeof(FirstGraph), "doc/learn/01-quickstart.md", "doc/learn/02-concepts.md"),
        new("etat-controle", "État et changes",
            "Mode contrôlé : chaque interaction produit des changes que l'application applique ou refuse.",
            typeof(ControlledState), "doc/learn/03-building-a-flow.md", "doc/reference/changes.md"),
        new("nodes-personnalises", "Nodes personnalisés",
            "Des modèles Razor avec de vrais champs, le redimensionnement, la rotation et une barre d'outils.",
            typeof(CustomNodes), "doc/learn/04-custom-nodes.md", "doc/reference/components.md"),
        new("edges", "Types d'edges", "Courbes, angles droits, flèches, labels, animation et tracé sur mesure.",
            typeof(EdgeGallery), "doc/learn/05-edges.md", "doc/learn/14-customization.md"),
        new("sous-flows", "Sous-flows et groupes", "Des nodes enfants qui suivent leur groupe et restent dedans.",
            typeof(SubFlows), "doc/learn/06-sub-flows.md"),
        new("disposition", "Disposition automatique", "Mélangez, puis laissez GraphLayout ranger le graphe en couches.",
            typeof(AutoLayout), "doc/learn/07-layouting.md", "doc/reference/utils.md"),
        new("theme", "Thème", "Clair, sombre ou vos couleurs, par les variables CSS ; trois motifs de fond.",
            typeof(Theming), "doc/learn/08-theming.md", "doc/reference/theme-keys.md"),
        new("connexions", "Connexions et validation",
            "Mode strict ou libre, connexion par clics, règle de validation et reconnexion.",
            typeof(Connecting), "doc/learn/09-interactions.md"),
        new("palette", "Glisser-déposer", "Une palette dont les éléments se déposent sur le graphe.",
            typeof(DragAndDrop), "doc/learn/09-interactions.md"),
        new("calcul", "Calculer avec un flow", "Des valeurs circulent le long des liens et sont recalculées à chaque changement.",
            typeof(Computing), "doc/learn/10-computing-flows.md"),
        new("performance", "Grands graphes", "Des milliers de nodes, avec ou sans virtualisation.",
            typeof(LargeGraph), "doc/learn/11-performance-accessibility.md"),
        new("json", "Sauvegarde et JSON", "Exporter le graphe au format React Flow, le modifier, le recharger.",
            typeof(JsonSave), "doc/learn/12-serialization.md", "doc/reference/types.md"),
        new("mvvm", "MVVM", "La vue travaille directement sur vos ViewModels.",
            typeof(MvvmBinding), "doc/learn/13-mvvm.md")
    ];

    public static Example? Find(string id) => All.FirstOrDefault(e => e.Id == id);

    public static IReadOnlyList<Example> ForDoc(string file) => All.Where(e => e.Docs.Contains(file)).ToList();
}
