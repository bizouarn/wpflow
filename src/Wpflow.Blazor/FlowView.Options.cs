using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Wpflow.Blazor;

/// <summary>Options et évènements de <see cref="FlowView" /> (les noms suivent React Flow et la version WPF).</summary>
public partial class FlowView
{
    // ---------- Viewport et vue ----------

    [Parameter] public double MinZoom { get; set; } = 0.5;
    [Parameter] public double MaxZoom { get; set; } = 2;
    [Parameter] public bool FitViewOnInit { get; set; } = true;
    [Parameter] public double FitViewPadding { get; set; } = 0.1;
    [Parameter] public FitViewOptions? FitViewOptions { get; set; }

    /// <summary>Limite la zone atteignable par le viewport (<c>translateExtent</c>), en coordonnées flow.</summary>
    [Parameter] public Rect? TranslateExtent { get; set; }

    /// <summary>Limite la position des nodes (<c>nodeExtent</c>), en coordonnées flow.</summary>
    [Parameter] public Rect? NodeExtent { get; set; }

    /// <summary>Origine des nodes (<c>nodeOrigin</c>) : (0,0) = coin haut gauche, (0.5,0.5) = centre.</summary>
    [Parameter] public XYPosition NodeOrigin { get; set; } = XYPosition.Zero;

    /// <summary>Verrou (le cadenas des contrôles) : plus de glisser, de connexion ni de sélection.</summary>
    [Parameter] public bool IsLocked { get; set; }

    [Parameter] public EventCallback<bool> IsLockedChanged { get; set; }

    // ---------- Interactions ----------

    [Parameter] public bool PanOnDrag { get; set; } = true;

    /// <summary>Boutons de souris qui font défiler la vue : 0 gauche, 1 milieu, 2 droit.</summary>
    [Parameter] public IReadOnlyList<int> PanOnDragButtons { get; set; } = [0, 1];

    [Parameter] public bool PanOnScroll { get; set; }
    [Parameter] public double PanOnScrollSpeed { get; set; } = 0.5;
    [Parameter] public PanOnScrollMode PanOnScrollMode { get; set; } = PanOnScrollMode.Free;
    [Parameter] public bool ZoomOnScroll { get; set; } = true;
    [Parameter] public bool ZoomOnPinch { get; set; } = true;
    [Parameter] public bool ZoomOnDoubleClick { get; set; } = true;

    /// <summary>Faux : la molette est laissée à la page quand elle ne zoome ni ne fait défiler la vue.</summary>
    [Parameter] public bool PreventScrolling { get; set; } = true;

    [Parameter] public bool NodesDraggable { get; set; } = true;
    [Parameter] public bool NodesConnectable { get; set; } = true;
    [Parameter] public bool ElementsSelectable { get; set; } = true;
    [Parameter] public bool EdgesReconnectable { get; set; } = true;
    [Parameter] public bool SelectNodesOnDrag { get; set; } = true;
    [Parameter] public bool SelectionOnDrag { get; set; }
    [Parameter] public FlowSelectionMode SelectionMode { get; set; } = FlowSelectionMode.Partial;

    /// <summary>Touche qui, maintenue, trace un rectangle de sélection : <c>Shift</c>, <c>Control</c>, <c>Meta</c>, <c>Alt</c>, <c>Space</c>.</summary>
    [Parameter] public string SelectionKey { get; set; } = "Shift";

    [Parameter] public string MultiSelectionKey { get; set; } = "Control";
    [Parameter] public string PanActivationKey { get; set; } = "Space";
    [Parameter] public string ZoomActivationKey { get; set; } = "Control";
    [Parameter] public IReadOnlyList<string> DeleteKeys { get; set; } = ["Delete", "Backspace"];
    [Parameter] public bool DeleteKeyEnabled { get; set; } = true;
    [Parameter] public double NodeDragThreshold { get; set; } = 3;
    [Parameter] public double ConnectionDragThreshold { get; set; } = 3;
    [Parameter] public double PaneDragThreshold { get; set; } = 3;
    [Parameter] public bool AutoPanOnNodeDrag { get; set; } = true;
    [Parameter] public bool AutoPanOnConnect { get; set; } = true;
    [Parameter] public bool AutoPanOnSelection { get; set; } = true;
    [Parameter] public double AutoPanSpeed { get; set; } = 15;

    /// <summary>Pas de la grille d'aimantation (<c>snapGrid</c>) ; <c>null</c> = pas d'aimantation.</summary>
    [Parameter] public double? SnapGrid { get; set; }

    // ---------- Connexions ----------

    [Parameter] public ConnectionMode ConnectionMode { get; set; } = ConnectionMode.Strict;
    [Parameter] public Func<Connection, bool>? IsValidConnection { get; set; }

    /// <summary>Rayon (pixels écran) dans lequel un handle accroche le fil (<c>connectionRadius</c>).</summary>
    [Parameter] public double ConnectionRadius { get; set; } = 20;

    [Parameter] public double ReconnectRadius { get; set; } = 10;

    /// <summary>Un clic sur un handle puis un clic sur un autre handle connecte (<c>connectOnClick</c>).</summary>
    [Parameter] public bool ConnectOnClick { get; set; } = true;

    [Parameter] public ConnectionLineType ConnectionLineType { get; set; } = ConnectionLineType.Bezier;
    [Parameter] public RenderFragment<ConnectionState>? ConnectionLineTemplate { get; set; }

    /// <summary>Vrai : une connexion aboutie ajoute un edge.</summary>
    [Parameter] public bool AutoConnect { get; set; } = true;

    [Parameter] public string DefaultEdgeType { get; set; } = "default";

    /// <summary>Modèle des edges créés par l'interface (<c>defaultEdgeOptions</c>) : type, animation, marqueurs, style…</summary>
    [Parameter] public Func<Edge, Edge>? DefaultEdgeOptions { get; set; }

    // ---------- Rendu ----------

    [Parameter] public bool ShowBackground { get; set; } = true;
    [Parameter] public BackgroundVariant BackgroundVariant { get; set; } = BackgroundVariant.Dots;
    [Parameter] public double BackgroundGap { get; set; } = 20;
    [Parameter] public double? BackgroundSize { get; set; }
    [Parameter] public double BackgroundLineWidth { get; set; } = 1;
    [Parameter] public string? BackgroundColor { get; set; }
    [Parameter] public XYPosition BackgroundOffset { get; set; } = XYPosition.Zero;

    /// <summary>Motifs superposés au fond principal (plusieurs <c>&lt;Background id="…"&gt;</c>).</summary>
    [Parameter] public IReadOnlyList<BackgroundLayer>? AdditionalBackgrounds { get; set; }

    [Parameter] public bool Dark { get; set; }
    [Parameter] public bool OnlyRenderVisibleElements { get; set; }
    [Parameter] public bool ElevateNodesOnSelect { get; set; } = true;
    [Parameter] public bool ElevateEdgesOnSelect { get; set; }
    [Parameter] public FlowZIndexMode ZIndexMode { get; set; } = FlowZIndexMode.Basic;

    /// <summary>Couleur des marqueurs sans couleur propre (<c>defaultMarkerColor</c>).</summary>
    [Parameter] public string? DefaultMarkerColor { get; set; }

    /// <summary>Contenu d'un node ; sans modèle (ou pour les types intégrés) utilisez <see cref="FlowDefaultNode" />.</summary>
    [Parameter] public RenderFragment<Node>? NodeTemplate { get; set; }

    /// <summary>Un modèle par <see cref="Node.Type" /> (<c>nodeTypes</c>), prioritaire sur <see cref="NodeTemplate" />.</summary>
    [Parameter] public IReadOnlyDictionary<string, RenderFragment<Node>>? NodeTemplates { get; set; }

    /// <summary>Tracés personnalisés par <see cref="Edge.Type" /> (<c>edgeTypes</c>) ; prioritaires sur les types intégrés.</summary>
    [Parameter] public IReadOnlyDictionary<string, IEdgePathProvider>? EdgeTypes { get; set; }

    /// <summary>Dessin complet d'un type d'edge : remplace le tracé par défaut (le fil de sélection reste fourni).</summary>
    [Parameter] public IReadOnlyDictionary<string, RenderFragment<EdgeRenderContext>>? EdgeTemplates { get; set; }

    /// <summary>Label des edges (<c>EdgeLabelRenderer</c>) ; sans modèle, <see cref="Edge.Label" /> est affiché en texte.</summary>
    [Parameter] public RenderFragment<EdgeRenderContext>? EdgeLabelTemplate { get; set; }

    /// <summary>Marqueurs personnalisés : clé (<see cref="Edge.MarkerEndKey" />) vers un dessin SVG pointant +x à l'origine.</summary>
    [Parameter] public IReadOnlyDictionary<string, RenderFragment<string>>? MarkerTemplates { get; set; }

    /// <summary>Calque par-dessus le graphe : <see cref="FlowControls" />, <see cref="FlowMiniMap" />…</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter] public string? Class { get; set; }
    [Parameter] public string? Style { get; set; }

    // ---------- Accessibilité ----------

    [Parameter] public bool NodesFocusable { get; set; } = true;
    [Parameter] public bool EdgesFocusable { get; set; } = true;
    [Parameter] public bool DisableKeyboardA11y { get; set; }
    [Parameter] public bool AnnounceSelection { get; set; } = true;
    [Parameter] public Func<Node, string?>? NodeAriaLabelProvider { get; set; }
    [Parameter] public Func<Edge, string?>? EdgeAriaLabelProvider { get; set; }

    // ---------- Historique ----------

    /// <summary>Conserve un historique annuler/rétablir (Ctrl+Z, Ctrl+Y).</summary>
    [Parameter] public bool HistoryEnabled { get; set; } = true;

    [Parameter] public int HistoryLimit { get; set; } = 100;

    /// <summary>Affiche dans la console les changes appliqués (<c>debug</c>).</summary>
    [Parameter] public bool Debug { get; set; }

    // ---------- Évènements ----------

    /// <summary>Levé avant l'application des changes (<c>onNodesChange</c>).</summary>
    [Parameter] public EventCallback<IReadOnlyList<NodeChange>> OnNodesChange { get; set; }

    [Parameter] public EventCallback<IReadOnlyList<EdgeChange>> OnEdgesChange { get; set; }
    [Parameter] public EventCallback<Connection> OnConnect { get; set; }
    [Parameter] public EventCallback<ConnectStartEventArgs> OnConnectStart { get; set; }
    [Parameter] public EventCallback<ConnectEndEventArgs> OnConnectEnd { get; set; }
    [Parameter] public EventCallback<ConnectStartEventArgs> OnClickConnectStart { get; set; }
    [Parameter] public EventCallback<ConnectEndEventArgs> OnClickConnectEnd { get; set; }
    [Parameter] public EventCallback<Edge> OnReconnectStart { get; set; }
    [Parameter] public EventCallback<ReconnectEventArgs> OnReconnect { get; set; }
    [Parameter] public EventCallback<ReconnectEndEventArgs> OnReconnectEnd { get; set; }

    [Parameter] public EventCallback<NodeEventArgs> OnNodeClick { get; set; }
    [Parameter] public EventCallback<NodeEventArgs> OnNodeDoubleClick { get; set; }
    [Parameter] public EventCallback<NodeEventArgs> OnNodeContextMenu { get; set; }
    [Parameter] public EventCallback<NodeEventArgs> OnNodeMouseEnter { get; set; }
    [Parameter] public EventCallback<NodeEventArgs> OnNodeMouseLeave { get; set; }
    [Parameter] public EventCallback<EdgeEventArgs> OnEdgeClick { get; set; }
    [Parameter] public EventCallback<EdgeEventArgs> OnEdgeDoubleClick { get; set; }
    [Parameter] public EventCallback<EdgeEventArgs> OnEdgeContextMenu { get; set; }
    [Parameter] public EventCallback<EdgeEventArgs> OnEdgeMouseEnter { get; set; }
    [Parameter] public EventCallback<EdgeEventArgs> OnEdgeMouseLeave { get; set; }
    [Parameter] public EventCallback<PaneEventArgs> OnPaneClick { get; set; }
    [Parameter] public EventCallback<PaneEventArgs> OnPaneContextMenu { get; set; }
    [Parameter] public EventCallback<PaneEventArgs> OnPaneMouseEnter { get; set; }
    [Parameter] public EventCallback<PaneEventArgs> OnPaneMouseLeave { get; set; }
    [Parameter] public EventCallback<PaneEventArgs> OnPaneMouseMove { get; set; }
    [Parameter] public EventCallback<PaneEventArgs> OnSelectionContextMenu { get; set; }

    [Parameter] public EventCallback<NodeDragEventArgs> OnNodeDragStart { get; set; }
    [Parameter] public EventCallback<NodeDragEventArgs> OnNodeDrag { get; set; }
    [Parameter] public EventCallback<NodeDragEventArgs> OnNodeDragStop { get; set; }
    [Parameter] public EventCallback<NodeDragEventArgs> OnSelectionDragStart { get; set; }
    [Parameter] public EventCallback<NodeDragEventArgs> OnSelectionDrag { get; set; }
    [Parameter] public EventCallback<NodeDragEventArgs> OnSelectionDragStop { get; set; }
    [Parameter] public EventCallback OnSelectionStart { get; set; }
    [Parameter] public EventCallback OnSelectionEnd { get; set; }

    /// <summary>La sélection (nodes et edges) a changé (<c>onSelectionChange</c>).</summary>
    [Parameter] public EventCallback<SelectionChangeArgs> OnSelectionChange { get; set; }

    [Parameter] public EventCallback<Viewport> OnMoveStart { get; set; }
    [Parameter] public EventCallback<Viewport> OnMove { get; set; }
    [Parameter] public EventCallback<Viewport> OnMoveEnd { get; set; }

    /// <summary>Levé une fois, quand le pont JS est prêt et que le conteneur est mesuré (<c>onInit</c>).</summary>
    [Parameter] public EventCallback OnInit { get; set; }

    /// <summary>Levé une fois, quand tous les nodes ont été mesurés (<c>nodesInitialized</c>).</summary>
    [Parameter] public EventCallback OnNodesInitialized { get; set; }

    [Parameter] public EventCallback<FlowError> OnError { get; set; }

    /// <summary>Un élément a été déposé sur la vue (glisser-déposer depuis une palette).</summary>
    [Parameter] public EventCallback<FlowDropEventArgs> OnDrop { get; set; }

    /// <summary>
    ///     Appelé avant toute suppression ; renvoyer faux l'annule (<c>onBeforeDelete</c>).
    /// </summary>
    [Parameter] public Func<DeleteRequest, Task<bool>>? OnBeforeDelete { get; set; }

    [Parameter] public EventCallback<DeleteRequest> OnDelete { get; set; }
}
