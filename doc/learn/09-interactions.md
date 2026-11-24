# 9. Interactions et connexions

*Équivalent React Flow : Interaction props, Connection validation, Drag and Drop, Keyboard.*

## Gestes

| Action | Geste |
|---|---|
| Zoomer | Molette (centré sur le curseur) ; pincement tactile ; `FlowControls` |
| Déplacer la vue | Glisser le fond ; clic milieu ; **Espace** maintenu + glisser (même sur un node) |
| Déplacer des nodes | Glisser un node (tous les nodes sélectionnés suivent) |
| Sélectionner | Clic ; Ctrl/Maj + clic pour ajouter ou retirer ; **Maj + glisser** sur le fond pour un rectangle |
| Tout sélectionner / désélectionner | Ctrl+A / Échap |
| Supprimer | Suppr ou Retour arrière |
| Annuler / rétablir | Ctrl+Z / Ctrl+Y (ou Ctrl+Maj+Z) |
| Connecter | Glisser d'un handle vers un autre |
| Reconnecter | Attraper un edge près de son extrémité et le déposer sur un autre handle |

## Clavier

| Touche | Effet |
|---|---|
| Tab / Maj+Tab | Sélectionne le node suivant / précédent |
| ← ↑ → ↓ | Déplace la sélection de 5 px (20 avec Maj) |
| Échap | Désélectionne |

Le clavier agit quand `FlowView` a le focus (un clic sur la surface le lui donne). Si le focus est dans un contrôle d'un node (un `TextBox`…), Tab et les flèches restent à ce contrôle.

## Verrouiller

`IsLocked="True"` désactive le déplacement des nodes, les connexions, la sélection et la suppression ; le pan et le zoom restent possibles. Le bouton cadenas de `FlowControls` agit sur cette propriété.

## Magnétisme et limites

```xml
<flow:FlowView SnapToGrid="True" SnapGrid="20" />
```

```csharp
Flow.NodeExtent      = new Rect(0, 0, 2000, 1200);    // nodes: ne sortent pas de cette zone (coordonnées flow)
Flow.TranslateExtent = new Rect(-100, -100, 800, 500); // vue: ne montre jamais plus que cette zone
Flow.MinZoom = 0.2; Flow.MaxZoom = 4;
```

(`Rect` désigne ici `Wpflow.Core.Rect`.) Si la zone est plus petite que la vue, elle est centrée.

## Connexions

```csharp
Flow.ConnectionMode = ConnectionMode.Strict;   // défaut : un handle source ne se relie qu'à un handle cible
Flow.ConnectionMode = ConnectionMode.Loose;    // n'importe quel handle vers n'importe quel handle
Flow.ConnectionRadius = 20;                    // distance d'accroche autour du curseur (unités flow)
```

Pendant le glisser, une ligne suit le curseur. Au relâchement, le handle compatible le plus proche **dans le rayon** est choisi (à distance égale, celui de type opposé). En mode `Loose`, la connexion est orientée selon le type du handle de **départ** : partir d'une cible inverse la connexion.

### Valider une connexion

```csharp
Flow.IsValidConnection = c => c.Source != c.Target                      // pas de boucle
                           && !Flow.Edges.Any(e => e.Target == c.Target); // une seule entrée par node
```

Le prédicat reçoit la `Connection` déjà orientée. Retourner `false` annule le geste. Les doublons exacts sont toujours refusés.

### Réagir à une connexion

```csharp
Flow.ConnectRequested += (_, e) => Log($"{e.Connection.Source} -> {e.Connection.Target}");
```

En mode non contrôlé l'edge est ajouté ensuite, avec l'identifiant `xy-edge__{source}{sourceHandle}-{target}{targetHandle}` (même format que React Flow).

## Glisser-déposer depuis une palette

`FlowView` accepte les dépôts (`AllowDrop`). Démarrez le glisser depuis n'importe quel élément avec `DragDrop.DoDragDrop`, puis créez le node à la position du dépôt :

```csharp
// Palette : un élément qui lance le glisser
private void OnPaletteMove(object sender, MouseEventArgs e)
{
    if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement { Tag: string type } item)
        DragDrop.DoDragDrop(item, new DataObject("wpflow-node-type", type), DragDropEffects.Copy);
}

// Surface : réception
Flow.DropRequested += (_, e) =>
{
    if (e.Data.GetData("wpflow-node-type") is not string type) return;
    var node = new Node(Guid.NewGuid().ToString(), e.Position - new XYPosition(75, 20), type, $"{type} node");
    Flow.Nodes = Flow.Nodes.Append(node).ToList();
};
```

`e.Position` est déjà en coordonnées flow (zoom et déplacement pris en compte).

## Sélection programmatique

```csharp
Flow.SelectNode("2");                    // seul
Flow.SelectNode("3", additive: true);    // en plus
Flow.Nodes = Flow.Nodes.Select(n => n with { Selected = n.Id == "2" }).ToList();
```

## Événements de la surface

| Événement | Quand |
|---|---|
| `NodeChangesRequested` / `EdgeChangesRequested` | avant l'application d'un lot de changes |
| `ConnectRequested` | connexion valide créée à la souris |
| `DropRequested` | objet déposé |
| `StateChanged` | nodes, edges, viewport ou verrouillage modifiés (pour dessiner vos propres vues liées) |

Suivant : [Calculer avec un flow](10-computing-flows.md).
