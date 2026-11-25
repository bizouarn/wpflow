# Utilitaires

Fonctions statiques de `Wpflow.Core`. Toutes sont pures (pas d'état, pas d'UI) et couvertes par des tests.

## EdgePaths

Chemins d'edges, portés de `getBezierPath`, `getSmoothStepPath`, `getStraightPath`. Les paramètres `source` et `target` sont les points d'attache (voir `GraphUtils.GetHandlePosition`).

```csharp
EdgePath Bezier(XYPosition source, Position sourcePosition, XYPosition target, Position targetPosition,
                double curvature = 0.25);
EdgePath Straight(XYPosition source, XYPosition target);
EdgePath Step(XYPosition source, Position sourcePosition, XYPosition target, Position targetPosition,
              double offset = 20, double stepPosition = 0.5);
EdgePath SmoothStep(XYPosition source, Position sourcePosition, XYPosition target, Position targetPosition,
                    double borderRadius = 5, double offset = 20, double stepPosition = 0.5,
                    double? centerX = null, double? centerY = null);

(double X, double Y, double OffsetX, double OffsetY) EdgeCenter(XYPosition source, XYPosition target);
(List<XYPosition> Points, double CenterX, double CenterY, double OffsetX, double OffsetY) StepPoints(…);
```

| Paramètre | Rôle |
|---|---|
| `curvature` | Courbure du Bézier quand la cible est « derrière » la source. |
| `offset` | Distance (px) parcourue en ligne droite en sortie de handle avant le premier coude. |
| `stepPosition` | Où se place le coude, de 0 (côté source) à 1 (côté cible). |
| `borderRadius` | Rayon des coins (`Step` = `SmoothStep` avec 0). |
| `centerX`/`centerY` | Force la position du coude. |

`EdgePath.Path` est une chaîne SVG (`M…C…`, `M…L…Q…`) ; `LabelX`/`LabelY` donnent le centre pour un label.

Les points alignés sont retirés du tracé « step », comme dans la version récente de xyflow.

## ViewportMath

```csharp
const double DefaultMinZoom = 0.5, DefaultMaxZoom = 2;

XYPosition ScreenToFlow(Viewport v, XYPosition screen, bool snapToGrid = false, double gridX = 15, double gridY = 15);
XYPosition FlowToScreen(Viewport v, XYPosition flow);
XYPosition SnapPosition(XYPosition p, double gridX, double gridY);
double     Clamp(double value, double min, double max);

Viewport PanBy(Viewport v, double dx, double dy);
Viewport ZoomAt(Viewport v, XYPosition screenPoint, double newZoom, double minZoom = 0.5, double maxZoom = 2);
double   WheelZoomFactor(double deltaY, bool ctrlKey = false);      // 2^(-deltaY × 0.002), ×10 si Ctrl

Viewport GetViewportForBounds(Rect bounds, double width, double height,
                              double minZoom = 0.5, double maxZoom = 2, double padding = 0.1);   // fitView
Rect     GetVisibleRect(Viewport v, double width, double height);                                // zone visible en coordonnées flow
Viewport ClampToExtent(Viewport v, double width, double height, Rect extent);                    // translateExtent
```

`ZoomAt` garde fixe le point écran donné : le point sous le curseur ne bouge pas pendant le zoom.

## GraphUtils

```csharp
Rect? GetNodesBounds(IEnumerable<Node> nodes);      // nodes mesurés, en coordonnées absolues ; null s'il n'y en a pas
XYPosition GetHandlePosition(Node node, Position position, HandleInfo? handle = null);   // point d'attache d'un edge
XYPosition GetHandleCenter(Node node, HandleInfo handle);                                // centre du handle (distance de connexion)
T? GetClosestHandle<T>(XYPosition point, double radius,
                       IEnumerable<(T Handle, XYPosition Position)> candidates) where T : class;
XYPosition ClampToExtent(XYPosition position, double width, double height, Rect extent); // nodeExtent
bool ConnectionExists(Connection c, IEnumerable<Edge> edges);   // null et "" désignent le même handle
bool IsEdgeVisible(Rect sourceNode, Rect targetNode, Rect visible);
```

`GetHandlePosition` renvoie le **bord extérieur** du handle du côté indiqué (comme `getHandlePosition` de xyflow) : c'est là qu'un edge se termine, ce qui laisse visible une flèche.

## NodeTree

```csharp
Dictionary<string, XYPosition> AbsolutePositions(IEnumerable<Node> nodes);
Dictionary<string, int> Depths(IEnumerable<Node> nodes);
HashSet<string> Descendants(string id, IEnumerable<Node> nodes);
HashSet<string> DescendantsOf(IEnumerable<string> ids, IEnumerable<Node> nodes);   // plusieurs racines, un seul parcours
XYPosition ClampToParent(Node child, XYPosition relativePosition, Node? parent);   // extent: 'parent'
```

## Connections

```csharp
bool AreTypesCompatible(HandleType from, HandleType to, ConnectionMode mode);   // Strict : types différents ; Loose : toujours
Connection Create(string fromNode, string? fromHandle, HandleType fromType, string toNode, string? toHandle);
bool IsValid(Connection connection, Func<Connection, bool>? isValidConnection);
```

`Create` oriente la connexion d'après le type du handle de **départ**.

## NodeResize

```csharp
Rect Resize(ResizeDirection direction, Rect start, double dx, double dy,
            double minWidth = 10, double minHeight = 10,
            double maxWidth = double.PositiveInfinity, double maxHeight = double.PositiveInfinity,
            bool keepAspectRatio = false);
```

`dx`/`dy` sont le déplacement de la poignée depuis `start`. Le bord opposé reste fixe, même quand un minimum ou un maximum est atteint.

## GraphLayout

```csharp
Dictionary<string, XYPosition> LayeredLeftToRight(IReadOnlyList<Node> nodes, IReadOnlyList<Edge> edges,
    double horizontalGap = 80, double verticalGap = 30, double defaultWidth = 150, double defaultHeight = 40);
```

Voir [Disposition automatique](../learn/07-layouting.md).

## GraphSerializer

```csharp
string ToJson(IEnumerable<Node> nodes, IEnumerable<Edge> edges, Viewport viewport, bool indented = true);
FlowGraph FromJson(string json);       // JsonException si un id ou une extrémité manque
```

Voir [Sauvegarde et JSON](../learn/12-serialization.md).

## Changes

Voir [Changes](changes.md).

## GraphQueries

Requêtes sur le graphe, équivalents de `getIncomers`, `getOutgoers`, `getConnectedEdges` et `addEdge`.

| Méthode | Description |
|---|---|
| `GetIncomers(node, nodes, edges)` | Nodes qui ont un edge vers `node`. |
| `GetOutgoers(node, nodes, edges)` | Nodes vers lesquels `node` a un edge. |
| `GetConnectedEdges(nodes, edges)` | Edges touchant l'un des `nodes`. |
| `AddEdge(edge, edges)` | Ajoute l'edge sauf si une connexion identique existe déjà ; renvoie la liste (inchangée en cas de doublon). |
| `ConnectionId(source, sourceHandle, target, targetHandle)` | Identifiant `xy-edge__…` fabriqué comme React Flow. |
| `GetNode(id, nodes)` / `GetEdge(id, edges)` | Recherche par identifiant. |
| `IsNodeIntersecting(node, area, partially = true)` | Le node (mesuré) touche la zone, ou y est entièrement avec `partially = false`. |
| `GetIntersectingNodes(area or node, nodes, partially = true)` | Nodes qui touchent une zone ou un autre node ; les nodes cachés sont ignorés. |
| `GetNodeConnections(nodeId, edges, type?, handleId?)` | Connexions d'un node (`NodeConnection` : `Connection` et `EdgeId`), filtrables par sens et par handle. |
| `ReconnectEdge(oldEdge, connection, edges, shouldReplaceId = true)` | Change les extrémités d'un edge ; liste inchangée si la connexion existe déjà. |

## GraphClipboard

Copier / couper / coller de nodes et d'edges, sans interface.

```csharp
ClipboardData? Copy(IReadOnlyList<Node> nodes, IReadOnlyList<Edge> edges);   // sélection + descendants + edges internes ; null si rien
ClipboardData Paste(ClipboardData data, IEnumerable<Node> existingNodes, IEnumerable<Edge> existingEdges, XYPosition offset);
```

`Paste` fabrique des ids neufs (`a-copy`, `a-copy2`…), décale les racines de `offset`, remappe parents et extrémités d'edges, sélectionne les nodes collés. Un node dont le parent n'est pas copié devient une racine à sa position absolue.

## Autres ajouts du noyau

| Élément | Description |
|---|---|
| `EdgePaths.SimpleBezier(...)` | Chemin `simplebezier` (`getSimpleBezierPath`). |
| `EdgeGeometry.Edge` (`Edge?`) | L'edge tracé, transmis aux fournisseurs de chemin (`null` sans edge). Les types `default`, `step`, `smoothstep` respectent ses `PathOptions` (`Curvature`, `BorderRadius`, `Offset`, `StepPosition`). |
| `GraphLayout.Layered(nodes, edges, LayoutDirection, rankGap, nodeGap, …)` | Disposition en couches dans les quatre directions (`LeftToRight`, `RightToLeft`, `TopToBottom`, `BottomToTop`). |
| `ViewportMath.GetViewportForCenter(x, y, zoom, w, h)` | Viewport centré sur un point (`setCenter`). |
| `ViewportMath.Interpolate(from, to, t)` / `EaseInOut(t)` | Transition de viewport (zoom géométrique, translation linéaire). |
| `Rect.Contains(Rect)` | Inclusion d'un rectangle. |

## Changesets

`Changesets.Diff(before, after)` (nodes ou edges) renvoie la liste de changes (`Add`, `Remove`, `Replace`) qui transforme `before` en `after` (`getNodesChangeset`, `getEdgesChangeset`) : `Changes.Apply(Changesets.Diff(a, b), a)` redonne `b`.

`GraphLayout.LayeredLeftToRight` et `Layered` réduisent les croisements d'edges : dans chaque couche, les nodes sont triés par barycentre de leurs voisins (balayages avant et arrière, tri stable : sans voisin, l'ordre d'entrée est conservé).
