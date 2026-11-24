# 2. Termes et concepts

*Équivalent React Flow : Terms and Concepts, Built-In Components.*

## Vocabulaire

| Terme | Signification |
|---|---|
| **Flow** | Le graphe affiché par un `FlowView`. |
| **Node** | Un élément déplaçable du graphe (`Node`). Son contenu est n'importe quel contrôle WPF. |
| **Edge** | Une liaison entre deux nodes (`Edge`). |
| **Handle** | Un point de connexion sur le bord d'un node (`Handle`). Il est de type `Source` (départ) ou `Target` (arrivée). |
| **Connection** | Une liaison en cours de création ou terminée : `Source`, `Target`, `SourceHandle`, `TargetHandle`. |
| **Viewport** | La transformation affichée : `Viewport(X, Y, Zoom)`. |
| **Coordonnées flow** | Le référentiel des nodes, indépendant du zoom et du déplacement de la vue. |
| **Coordonnées écran** | Les pixels du contrôle. `écran = flow × Zoom + (X, Y)`. |
| **Change** | Une modification atomique demandée par l'interface (`NodeChange`, `EdgeChange`). |

## Le viewport

```csharp
Flow.Viewport = new Viewport(x: 100, y: 50, zoom: 1.5);

XYPosition flowPoint = Flow.ScreenToFlow(Mouse.GetPosition(Flow));   // écran -> flow
```

Le zoom est borné par `MinZoom` (0,5) et `MaxZoom` (2). `FitView()` cadre tous les nodes mesurés ; `FitViewOnInit="True"` le fait une fois que tous les nodes ont été mesurés.

## Les trois couches

À l'intérieur de `FlowView`, de bas en haut :

1. le **fond** (points, lignes ou croix), qui suit le viewport ;
2. la **surface transformée** : les edges, puis les nodes, puis la ligne de connexion en cours ;
3. l'**overlay** : le `Content` du `FlowView` (minimap, contrôles, panneaux, textes…), qui ne bouge pas avec le viewport.

## Composants intégrés

| React Flow | Wpflow | Rôle |
|---|---|---|
| `<Background>` | `BackgroundVariant`, `BackgroundGap`, `BackgroundColor` (propriétés de `FlowView`) | Fond |
| `<Controls>` | `FlowControls` | Zoom +/−, cadrage, verrouillage |
| `<MiniMap>` | `MiniMap` | Vue d'ensemble, cliquable |
| `<Panel>` | `FlowPanel` | Ancrer du contenu à un coin |
| `<NodeResizer>` | `NodeResizer` | Poignées de redimensionnement |
| `<NodeToolbar>` | `NodeToolbar` | Barre d'outils flottante |
| `<Handle>` | `Handle` | Point de connexion |

## Mesure des nodes

React Flow mesure les nodes dans le DOM ; Wpflow les mesure dans l'arbre visuel WPF. Après la mise en page, chaque `NodeHost` rapporte sa taille et les positions de ses handles. Conséquences :

- `Node.Width` / `Node.Height` sont **mesurés** (ne les renseignez pas pour dimensionner un node) ;
- une taille imposée se donne avec `FixedWidth` / `FixedHeight` ;
- un edge n'est dessiné qu'une fois ses deux extrémités mesurées.

## Immutabilité et changes

Les nodes et edges sont des `record` immuables. L'interface ne les modifie jamais : elle émet des *changes* et les applique à une **nouvelle** liste. Voir [Construire un flow](03-building-a-flow.md).
