# Wpflow

Éditeur de graphes node/edge interactif pour **WPF**, porté de [xyflow](https://github.com/xyflow/xyflow) (React Flow) : surface zoomable, nodes déplaçables dont le contenu est n'importe quel contrôle WPF, handles, connexions à la souris, edges (bézier, droits, à angles droits), minimap, contrôles, groupes, redimensionnement, thèmes clair/sombre, sérialisation JSON compatible React Flow.

![Démo](work-doc/screenshots/demo.png)

```bash
dotnet run --project demo        # page d'accueil façon reactflow.dev ; le bouton « Exemples » ouvre les autres cas
dotnet test tests/Wpflow.Core.Tests
```

## Structure

| Projet | Rôle |
|---|---|
| `src/Wpflow.Core` (net8.0) | Logique **sans UI**, portée de `@xyflow/system` : modèle immuable, viewport, chemins d'edges, connexions, nodes imbriqués, JSON, disposition automatique. Testée (`tests/`). |
| `src/Wpflow.Wpf` (net8.0-windows) | Contrôles WPF : `FlowView`, `Handle`, `NodeResizer`, `NodeToolbar`, `MiniMap`, `FlowControls`, `FlowPanel`. |
| `demo` | Page d'accueil (réplique de reactflow.dev) et fenêtre d'exemples. |

## Démarrage rapide

```xml
<Window xmlns:flow="clr-namespace:Wpflow.Wpf;assembly=Wpflow.Wpf" …>
    <flow:FlowView x:Name="Flow" FitViewOnInit="True">
        <Grid>                       <!-- le contenu de FlowView est un overlay au-dessus du graphe -->
            <flow:MiniMap />
            <flow:FlowControls />
        </Grid>
    </flow:FlowView>
</Window>
```

```csharp
using Wpflow.Core;

Flow.Nodes =
[
    new Node("1", new XYPosition(0, 0),   "input",   "Début"),
    new Node("2", new XYPosition(250, 80), "default", "Étape"),
    new Node("3", new XYPosition(500, 0),  "output",  "Fin"),
];
Flow.Edges =
[
    new Edge("e1-2", "1", "2", Animated: true),
    new Edge("e2-3", "2", "3", Type: "smoothstep", Label: "ok", MarkerEnd: EdgeMarker.ArrowClosed),
];
```

Glisser les nodes, relier les handles, molette pour zoomer, glisser le fond (ou clic milieu, ou **Espace** + glisser) pour se déplacer — tout fonctionne sans autre code.

## Principes

**L'état passe par des « changes », comme dans React Flow.** `Nodes` et `Edges` sont des listes de `record` immuables. L'interface ne les modifie jamais : elle produit des `NodeChange` / `EdgeChange` (position, dimensions, sélection, ajout, suppression, redimensionnement…) et, par défaut (`AutoApplyChanges="True"`), les applique avec `Changes.Apply`. Pour contrôler vous-même l'état (store, validation, undo personnalisé) :

```csharp
Flow.AutoApplyChanges = false;
Flow.NodeChangesRequested += (_, e) => Flow.Nodes = Changes.Apply(e.Changes, Flow.Nodes);
Flow.ConnectRequested     += (_, e) => /* e.Connection : Source, Target, SourceHandle, TargetHandle */;
```

### Nodes personnalisés

Un node de type `"montype"` est rendu avec le `DataTemplate` de clé `wpflow-node-montype`, défini n'importe où dans les ressources (fenêtre, application…). Le `DataContext` est le `Node` ; mettez votre ViewModel dans `Node.Data` — les contrôles WPF (Slider, TextBox…) fonctionnent normalement à l'intérieur.

```xml
<DataTemplate x:Key="wpflow-node-carte">
    <Grid>
        <Border Background="{DynamicResource Flow.NodeBackground}" CornerRadius="6" Padding="12">
            <TextBlock Text="{Binding Data.Titre}" />
        </Border>
        <flow:NodeResizer />                                       <!-- après la carte, avant les handles -->
        <flow:NodeToolbar ToolbarPosition="Top"> … </flow:NodeToolbar>
        <flow:Handle Type="Target" Position="Left" />
        <flow:Handle Type="Source" Position="Right" HandleId="sortie" />
    </Grid>
</DataTemplate>
```

Types intégrés : `default` (handle haut + bas), `input` (source seule), `output` (cible seule), `group` (zone translucide pour nodes imbriqués).

### Edges

`Type` : `default` (bézier), `straight`, `step`, `smoothstep`. `MarkerEnd` / `MarkerStart` (`Arrow`, `ArrowClosed`), `Animated`, `Label`. Pour un label avec du contenu libre (équivalent d'`EdgeLabelRenderer`) : `EdgeLabelTemplate="{StaticResource …}"` (DataContext = l'`Edge`).

### Nodes imbriqués

`new Node("enfant", position, ParentId: "groupe", ExtentParent: true)` : la position est relative au parent, l'enfant le suit et reste dans le groupe ; supprimer le parent supprime ses descendants.

### Connexions

`ConnectionMode` : `Strict` (source → cible) ou `Loose` (tout handle vers tout handle). `IsValidConnection` (`Func<Connection,bool>`) refuse des connexions. `ConnectionRadius` règle la distance d'accroche. Un edge se **reconnecte** en attrapant son extrémité.

## Interactions

| Action | Geste |
|---|---|
| Zoom | Molette (centré sur le curseur), pincement tactile, boutons `FlowControls` |
| Déplacer la vue | Glisser le fond, clic milieu, Espace + glisser |
| Déplacer des nodes | Glisser ; flèches (5 px, 20 avec Maj) |
| Sélection | Clic, Ctrl/Maj + clic, rectangle avec Maj, Tab / Maj+Tab, Ctrl+A, Échap |
| Supprimer | Suppr / Retour arrière |
| Annuler / rétablir | Ctrl+Z, Ctrl+Y (mode non contrôlé) |
| Connecter | Glisser d'un handle à un autre |

## Fonctionnalités et réglages

`MinZoom`/`MaxZoom`, `SnapToGrid`/`SnapGrid`, `NodeExtent` (limites des nodes), `TranslateExtent` (limites du pan), `IsLocked`, `FitViewOnInit`/`FitView()`, `BackgroundVariant` (`Dots`, `Lines`, `Cross`) et `BackgroundGap`, `EdgeStroke`, `ColorMode` (`Light`/`Dark`), `OnlyRenderVisibleElements`, `DropRequested` (glisser-déposer depuis une palette), `Undo()`/`Redo()`.

**Thèmes.** `ColorMode="Dark"` charge `Themes/Dark.xaml`. Toutes les couleurs sont des clés `Flow.*` (`Flow.Background`, `Flow.Edge`, `Flow.NodeBackground`, `Flow.MiniMapNode`…) : redéfinissez-les dans `FlowView.Resources` pour personnaliser, et utilisez `{DynamicResource Flow.…}` dans vos templates.

**JSON.** `GraphSerializer.ToJson(nodes, edges, viewport)` / `FromJson(json)` lisent et écrivent la forme `{ nodes, edges, viewport }` de React Flow (`parentId`, `extent: "parent"`, `markerEnd`…). `Node.Data` revient en `JsonElement`.

**Disposition automatique.** `GraphLayout.LayeredLeftToRight(nodes, edges)` calcule des positions en couches.

**Performance.** Mesuré dans la démo (nodes par défaut, un edge par node, sur la machine de développement) : 1 000 nodes → 0,8 s d'affichage, ~20 ms/image en déplacement et en pan ; 3 000 nodes rendus en totalité → 3,5 à 3,8 s et 65 à 95 ms/image. Avec `OnlyRenderVisibleElements="True"`, 5 000 nodes → 2,5 s, 11 ms et 7 ms/image : **activez-le dès ~1 000 nodes**. Les nodes hors vue n'étant pas mesurés, `FitViewOnInit` et la minimap ne connaissent que les nodes mesurés : donnez `FixedWidth`/`FixedHeight` aux nodes d'un grand graphe virtualisé.

**Accessibilité.** `FlowView` et `NodeHost` exposent des peers UI Automation (nom, sélection) ; navigation complète au clavier (voir tableau).

## Correspondance avec xyflow

| xyflow | Wpflow |
|---|---|
| `<ReactFlow nodes edges>` | `FlowView` (`Nodes`, `Edges`) |
| `onNodesChange` / `applyNodeChanges` | `NodeChangesRequested` / `Changes.Apply` |
| `nodeTypes` | `DataTemplate` de clé `wpflow-node-{type}` |
| `<Handle>` | `Handle` |
| `<Background>`, `<Controls>`, `<MiniMap>`, `<Panel>` | `BackgroundVariant`, `FlowControls`, `MiniMap`, `FlowPanel` |
| `<NodeResizer>`, `<NodeToolbar>`, `EdgeLabelRenderer` | `NodeResizer`, `NodeToolbar`, `EdgeLabelTemplate` |
| `getBezierPath`, `getSmoothStepPath`… | `EdgePaths.Bezier`, `EdgePaths.SmoothStep`… (sortie SVG, utilisable avec `Geometry.Parse`) |
| `useReactFlow().fitView` / `zoomIn` | `FitView()` / `ZoomIn()` |
| `connectionMode`, `isValidConnection` | `ConnectionMode`, `IsValidConnection` |
| `translateExtent`, `nodeExtent` | `TranslateExtent`, `NodeExtent` |
| `onlyRenderVisibleElements` | `OnlyRenderVisibleElements` |
| `colorMode` | `ColorMode` |

Les chemins d'edges ont été comparés au code source de `@xyflow/system` (bézier identique ; `getSmoothStepPath` porté dans sa version récente : `stepPosition`, `centerX/Y`, suppression des points alignés).

## Limites connues

- WPF uniquement (Windows). Le noyau `Wpflow.Core` n'a aucune dépendance UI.
- Le pincement tactile est implémenté mais n'a pas pu être testé sur un écran tactile.
- Pas encore : `isValidConnection` asynchrone, edges personnalisés par type (seuls les 4 types intégrés), marqueurs personnalisés, sélection d'edges au rectangle, minimap zoomable, `NodeResizer` avec ratio d'aspect.
- Licence : [MIT](LICENSE). Le portage de xyflow (lui aussi MIT) est signalé dans [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Développement

**Documentation** : [`doc/`](doc/README.md) (guides « Learn », référence de l'API, exemples, organisés comme reactflow.dev). Pour contribuer : `CLAUDE.md` (architecture, commandes, auto-tests visuels pilotant la vraie souris) et `work-doc/` (roadmap, décisions d'architecture).
