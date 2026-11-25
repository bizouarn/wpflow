namespace Wpflow.Core;

/// <summary>
///     Correspondance entre les propriétés d'un ViewModel de node et un <see cref="Node" /> (voir <c>FlowView.NodesSource</c>).
///     Chaque valeur est un <b>nom de propriété</b> du ViewModel ; <c>null</c> = non mappé (la valeur courante du node est
///     conservée). Les vues exposent leur classe dérivée, <c>NodeMapping</c>.
/// </summary>
/// <remarks>Les valeurs par défaut suivent la convention <c>Id</c>, <c>X</c>, <c>Y</c> : un ViewModel qui les expose n'a rien à configurer.</remarks>
public class NodeMappingBase
{
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

    public string? ExtentParentPath { get; set; }

    /// <summary>Propriété de type <see cref="Rect" /> (nullable).</summary>
    public string? ExtentPath { get; set; }

    public string? ExpandParentPath { get; set; }

    /// <summary>Propriété de type <see cref="Position" /> (nullable).</summary>
    public string? SourcePositionPath { get; set; }

    /// <summary>Propriété de type <see cref="Position" /> (nullable).</summary>
    public string? TargetPositionPath { get; set; }

    public string? DragHandlePath { get; set; }
    public string? ZIndexPath { get; set; }
    public string? AriaLabelPath { get; set; }

    /// <summary>Propriété de type <see cref="XYPosition" /> (nullable).</summary>
    public string? OriginPath { get; set; }

    /// <summary>Rotation en degrés ; écrite par <c>NodeRotator</c>.</summary>
    public string? RotationPath { get; set; }

    public string? InitialWidthPath { get; set; }
    public string? InitialHeightPath { get; set; }

    /// <summary>Propriété de type <c>IReadOnlyList&lt;HandleInfo&gt;</c>.</summary>
    public string? HandlesPath { get; set; }
}

/// <summary>Correspondance entre les propriétés d'un ViewModel d'edge et un <see cref="Edge" /> (voir <c>FlowView.EdgesSource</c>).</summary>
public class EdgeMappingBase
{
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
    ///     <see cref="NodeMappingBase.DataPath" />, <c>null</c> ne met <b>pas</b> le ViewModel dans <c>Data</c> : il n'est pas mappé.
    /// </summary>
    public string? DataPath { get; set; }

    /// <summary>Propriété de type <see cref="EdgeMarker" /> pour le marqueur de départ.</summary>
    public string? MarkerStartPath { get; set; }

    /// <summary>Propriété de type <see cref="EdgeMarker" /> pour le marqueur d'arrivée.</summary>
    public string? MarkerEndPath { get; set; }

    public string? MarkerStartKeyPath { get; set; }
    public string? MarkerEndKeyPath { get; set; }
    public string? ReconnectablePath { get; set; }
    public string? InteractionWidthPath { get; set; }
    public string? ZIndexPath { get; set; }
    public string? AriaLabelPath { get; set; }

    /// <summary>Propriété de type <see cref="EdgeStyleOptions" />.</summary>
    public string? StylePath { get; set; }

    /// <summary>Propriété de type <see cref="EdgeLabelOptions" />.</summary>
    public string? LabelStylePath { get; set; }

    /// <summary>Propriété de type <see cref="EdgePathOptions" />.</summary>
    public string? PathOptionsPath { get; set; }

    /// <summary>Propriété de type <see cref="EdgeMarkerOptions" />.</summary>
    public string? MarkerStartOptionsPath { get; set; }

    /// <summary>Propriété de type <see cref="EdgeMarkerOptions" />.</summary>
    public string? MarkerEndOptionsPath { get; set; }
}
