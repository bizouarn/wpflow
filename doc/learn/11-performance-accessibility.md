# 11. Performance et accessibilité

*Équivalent React Flow : Performance, Accessibility.*

## Performance

### Mesures

Faites dans la démo (`WPFLOW_PERF=N`), nodes par défaut, un edge par node, sur la machine de développement. Ce sont des ordres de grandeur, pas des garanties :

| | 3 000 nodes | 5 000 nodes, `OnlyRenderVisibleElements` | 20 000 nodes, `OnlyRenderVisibleElements` |
|---|---|---|---|
| Premier affichage + mesure | 3,5 à 3,8 s | 0,4 s | 0,4 s |
| Déplacer un node (par image) | 65 à 95 ms | 9 à 12 ms | 24 ms |
| Déplacer la vue (par image) | 65 à 90 ms | 9 à 13 ms | 26 ms |
| Mémoire managée ajoutée | +32 Mo (≈ 11 Ko/node) | +6,8 Mo | +8,4 Mo |
| Mémoire résidente (working set) ajoutée | +84 Mo | +13 à 18 Mo | +21 Mo |

### Activer la virtualisation

```xml
<flow:FlowView OnlyRenderVisibleElements="True" />
```

Seuls les nodes et edges proches de la zone visible ont un élément visuel : ceux de la vue plus 300 px sont affichés, ceux jusqu'à 900 px sont gardés masqués (pour ne pas les recréer à chaque va-et-vient du pan), les autres **n'existent pas** — ni en mémoire visuelle, ni en mise en page, ni au rendu. Les nodes sélectionnés restent toujours affichés. Ce n'est donc pas qu'un gain de vitesse : la mémoire ne dépend plus du nombre total de nodes (voir les mesures ci-dessous). À activer dès ~1 000 nodes.

**Limite connue** : un node n'est mesuré que s'il a été affiché. Les nodes jamais vus restent sans dimensions, donc `FitViewOnInit`, `FitView()` et la minimap ne connaissent que les nodes mesurés. Pour un grand graphe virtualisé, donnez une taille explicite :

```csharp
new Node(id, position, "default", data, FixedWidth: 150, FixedHeight: 40)
```

### Ce que Wpflow fait déjà pour vous

- Les mesures de nodes sont **regroupées** : N nodes mesurés = une seule mise à jour (sans cela, le chargement était quadratique).
- Une mise à jour ne recalcule que les nodes dont la référence ou la position absolue a changé, et les edges qui y sont reliés.
- Un edge dont le tracé, les extrémités et le style n'ont pas changé n'est ni re-parsé ni redessiné ; chaque edge est un seul élément qui se dessine lui-même.

### Conseils

- Gardez les templates de nodes légers : le coût par node vient surtout du nombre d'éléments visuels (un handle, un edge et un contour de sélection sont dessinés par un seul élément chacun).
- Évitez les `Effect` (ombres portées) sur des centaines de nodes ; un seul effet par carte reste raisonnable.
- Ne reconstruisez pas toute la liste quand un seul node change : `Flow.Nodes.Select(n => n.Id == x ? n with { … } : n)` conserve la référence des autres.

## Accessibilité

### Clavier

Tout est faisable sans souris : Tab / Maj+Tab pour parcourir les nodes, flèches pour les déplacer, Suppr, Ctrl+A, Échap, Ctrl+Z / Ctrl+Y. Voir [Interactions](09-interactions.md#clavier).

### UI Automation

| Contrôle | Peer | Expose |
|---|---|---|
| `FlowView` | `FlowViewAutomationPeer` | `Pane` nommé « Flow diagram » (ou `AutomationProperties.Name`) |
| `NodeHost` | `NodeHostAutomationPeer` | `ListItem` nommé d'après `Data` (texte) ou l'`Id`, pattern **SelectionItem** (`Select`, `AddToSelection`, `RemoveFromSelection`) |

Nommez votre flow :

```xml
<flow:FlowView AutomationProperties.Name="Processus de validation" … />
```

Pour qu'un node à template personnalisé soit annoncé clairement, renseignez `AutomationProperties.Name` sur la racine de son template.

> Les peers sont vérifiés par des tests de code ; ils n'ont pas été éprouvés avec un lecteur d'écran réel (Narrateur, NVDA).

### Contraste et thèmes

Le thème sombre (`ColorMode="Dark"`) est fourni ; surchargez les clés `Flow.*` pour un thème à fort contraste. Les couleurs ne portent jamais seules l'information (les edges animés et sélectionnés changent aussi de style).

### Tactile

Le pincement à deux doigts zoome (`ManipulationDelta`) ; le glisser à un doigt déplace nodes ou vue via les événements souris promus par WPF. **Non testé sur matériel tactile.**

Suivant : [Sauvegarde et JSON](12-serialization.md).
