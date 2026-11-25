namespace Wpflow.Wpf;

/// <summary>
///     Correspondance entre les propriétés d'un ViewModel de node et un <see cref="Core.Node" />
///     (voir <see cref="FlowView.NodesSource" />). Chaque valeur est un <b>nom de propriété</b> du ViewModel ;
///     <c>null</c> = non mappé (la valeur courante du node est conservée).
/// </summary>
/// <remarks>Les valeurs par défaut suivent la convention <c>Id</c>, <c>X</c>, <c>Y</c> : un ViewModel qui les expose n'a rien à configurer.</remarks>
public sealed class NodeMapping
{
    internal static readonly NodeMapping Default = new();

    public string IdPath { get; set; } = "Id";
    public string? XPath { get; set; } = "X";
    public string? YPath { get; set; } = "Y";
    public string? TypePath { get; set; }

    /// <summary>Taille explicite du node (<c>FixedWidth</c>) ; écrite par le redimensionnement. <c>NaN</c> ou <c>null</c> = taille automatique.</summary>
    public string? WidthPath { get; set; }

    public string? HeightPath { get; set; }
    public string? SelectedPath { get; set; }
    public string? DraggablePath { get; set; }
    public string? SelectablePath { get; set; }
    public string? ParentIdPath { get; set; }
    public string? HiddenPath { get; set; }
    public string? ConnectablePath { get; set; }
    public string? DeletablePath { get; set; }

    /// <summary>Propriété fournie à <c>Node.Data</c> (DataContext des templates) ; <c>null</c> = le ViewModel lui-même.</summary>
    public string? DataPath { get; set; }

    internal HashSet<string> Paths() => Collect(IdPath, XPath, YPath, TypePath, WidthPath, HeightPath, SelectedPath,
        DraggablePath, SelectablePath, ParentIdPath, DataPath, HiddenPath, ConnectablePath, DeletablePath);

    internal static HashSet<string> Collect(params string?[] paths) => paths.OfType<string>().ToHashSet();
}

/// <summary>Correspondance entre les propriétés d'un ViewModel d'edge et un <see cref="Core.Edge" /> (voir <see cref="FlowView.EdgesSource" />).</summary>
public sealed class EdgeMapping
{
    internal static readonly EdgeMapping Default = new();

    public string IdPath { get; set; } = "Id";
    public string SourcePath { get; set; } = "Source";
    public string TargetPath { get; set; } = "Target";
    public string? SourceHandlePath { get; set; }
    public string? TargetHandlePath { get; set; }
    public string? TypePath { get; set; }
    public string? LabelPath { get; set; }
    public string? AnimatedPath { get; set; }
    public string? HiddenPath { get; set; }
    public string? SelectablePath { get; set; }
    public string? DeletablePath { get; set; }
    public string? SelectedPath { get; set; }

    /// <summary>
    ///     Propriété fournie à <c>Edge.Data</c> (lue par les fournisseurs de tracé et les modèles d'edge). Contrairement à
    ///     <see cref="NodeMapping.DataPath" />, <c>null</c> ne met <b>pas</b> le ViewModel dans <c>Data</c> : il n'est pas mappé.
    /// </summary>
    public string? DataPath { get; set; }

    /// <summary>Propriété de type <see cref="Core.EdgeMarker" /> pour le marqueur de départ.</summary>
    public string? MarkerStartPath { get; set; }

    /// <summary>Propriété de type <see cref="Core.EdgeMarker" /> pour le marqueur d'arrivée.</summary>
    public string? MarkerEndPath { get; set; }

    internal HashSet<string> Paths() => NodeMapping.Collect(IdPath, SourcePath, TargetPath, SourceHandlePath,
        TargetHandlePath, TypePath, LabelPath, AnimatedPath, SelectedPath, HiddenPath, SelectablePath, DeletablePath,
        DataPath, MarkerStartPath, MarkerEndPath);
}
