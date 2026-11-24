# Exemples

Tous les exemples sont dans le dossier `demo/` et se lancent avec :

```bash
dotnet run --project demo
```

La page d'accueil reproduit la démo de [reactflow.dev](https://reactflow.dev/) ; son bouton **« Exemples »** ouvre la fenêtre d'exemples (un onglet par cas). Chaque exemple est vérifié par un auto-test qui pilote la vraie souris et le vrai clavier (voir `CLAUDE.md`).

![Page d'accueil](../../work-doc/screenshots/demo.png)

## Page d'accueil : calculer avec un flow

*Fichiers : `demo/MainWindow.xaml`, `MainWindow.xaml.cs`, `DemoState.cs`, `CubeField.cs`.*

Trois nodes d'entrée (sélecteur de couleur, forme, zoom) reliés par des edges animés à un node de sortie qui dessine un nuage de solides **3D animés**. Chaque entrée n'agit que si son edge existe : supprimez l'edge du zoom, il retombe à sa valeur par défaut ; reconnectez-le, le curseur reprend la main (le zoom suit avec une inertie).

- Le sélecteur de couleur est un vrai `Popup` (pastilles + curseur de teinte) ; la couleur change aussi à la saisie d'un code hexadécimal.
- Le node de sortie est redimensionnable (`NodeResizer`).

Guide détaillé : [Calculer avec un flow](../learn/10-computing-flows.md).

## Types d'edges

*Onglet « Types d'edges » — `ExamplesWindow.xaml.cs`, `BuildEdgeTypes`.*

`default`, `straight`, `step`, `smoothstep` ; flèches (`MarkerEnd` / `MarkerStart`), edge animé, labels sous forme de pastilles (`EdgeLabelTemplate`), et un edge qui part du bas d'un node pour arriver à gauche d'un autre. Les edges sont roses grâce à une surcharge locale de `Flow.Edge`.

Guide : [Edges](../learn/05-edges.md).

## Groupes

*Onglet « Groupes ».*

Un groupe de deux enfants dont la position est relative (`ParentId`, `ExtentParent`). Déplacez le groupe : les enfants suivent ; tirez un enfant hors du groupe : il reste dedans ; supprimez le groupe : les enfants disparaissent avec lui. Navigation au clavier (Tab, flèches).

Guide : [Sous-flows et groupes](../learn/06-sub-flows.md).

## Toolbar et resizer

*Onglet « Toolbar et resizer » — template `wpflow-node-tool`.*

Sélectionnez un node : sa barre d'outils (Dupliquer, Supprimer, branchées sur des `ICommand` de `Node.Data`) apparaît au-dessus et huit poignées permettent de le redimensionner.

Guide : [Nodes personnalisés](../learn/04-custom-nodes.md).

## Palette (glisser-déposer)

*Onglet « Palette ».*

Glissez « Input node », « Default node » ou « Output node » de la colonne de gauche vers la surface : un node du bon type est créé à l'endroit du dépôt (`DropRequested`). Fond en lignes (`BackgroundVariant.Lines`).

Guide : [Interactions](../learn/09-interactions.md#glisser-déposer-depuis-une-palette).

## Disposition automatique

*Onglet « Disposition auto ».*

Sept nodes placés au hasard ; « Disposition automatique » les range en couches de gauche à droite (`GraphLayout.LayeredLeftToRight`), « Mélanger » les disperse de nouveau.

Guide : [Disposition automatique](../learn/07-layouting.md).

## Options et JSON

*Onglet « Options et JSON ».*

Cases à cocher pour : mode sombre, connexion `Loose`, magnétisme à la grille, verrouillage, limite du pan (`TranslateExtent`), et un `IsValidConnection` qui refuse les connexions vers « Node D ». « Exporter JSON » écrit le graphe dans la zone de texte, « Importer JSON » le recharge.

Guides : [Thèmes](../learn/08-theming.md), [Interactions](../learn/09-interactions.md), [JSON](../learn/12-serialization.md).

## Performance

*Onglet « Performance ».*

« Charger 3 000 nodes » avec ou sans virtualisation (`OnlyRenderVisibleElements`) ; le texte affiche le nombre de nodes réellement rendus (de l'ordre de 260 sur 3 000 en virtualisé).

Guide : [Performance et accessibilité](../learn/11-performance-accessibility.md).

## Mesurer les performances en ligne de commande

```bash
WPFLOW_PERF=5000 WPFLOW_PERF_VIRT=1 WPFLOW_SCREENSHOT="chemin/perf.png" dotnet run --project demo --no-build
# écrit chemin/perf.perf.log : temps d'affichage, ms/image en déplacement et en pan
```

## Équivalents des exemples de React Flow

| Exemple React Flow | Dans Wpflow |
|---|---|
| Overview / Basic | Démarrage rapide, page d'accueil |
| Custom Node, Add Node on Edge Drop | Nodes personnalisés, palette |
| Edge Types, Custom Edge, Edge Label Renderer | Types d'edges |
| Sub Flows / Nested | Groupes |
| Node Resizer, Node Toolbar | Toolbar et resizer |
| Drag and Drop | Palette |
| Layouting | Disposition automatique |
| Dark Mode | Options et JSON |
| Save and Restore | Options et JSON |
| Computing Flows | Page d'accueil |
| Validation | Options et JSON (`IsValidConnection`) |
| Stress | Performance |
