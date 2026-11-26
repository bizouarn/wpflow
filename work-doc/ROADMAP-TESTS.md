# Roadmap : tests complémentaires et usage réel

Objectif : trouver les défauts **avant** qu'une application qui utilise la bibliothèque ne les trouve. Cette page ne garde que ce qui reste.

**WPF et Blazor sont concernés à égalité.** Chaque lot a un résultat attendu pour les deux ; un lot n'est fini que lorsque les deux le sont.

`ci.yml` et `release.yml` ne lancent que les tests de base (noyau, WPF, bUnit), en une minute au plus. Le bout en bout Blazor, les auto-tests WPF et la consommation des paquets se lancent à la main avant de poser le tag : rien ne bloque automatiquement la publication sur leur résultat.

## Règles

1. Un défaut trouvé en usage réel donne **d'abord un test qui échoue**, au niveau où il se produit (navigateur ou vraie souris), puis la correction.
2. Un défaut trouvé d'un côté est **cherché de l'autre** avant d'être clos.
3. Une interaction n'est couverte que par une **entrée réelle** (Playwright, souris et clavier Win32). bUnit et les tests WPF en mémoire vérifient la logique, pas le branchement.
4. Chaque test dit aussi ce qui **ne doit pas** se produire (pas de zoom au double-clic, pas de clic après un glisser).
5. Ce qui n'est pas vérifiable automatiquement va dans la liste manuelle (lot 7), avec la date du dernier essai.

## Lots

Les tailles sont relatives (S, M, L).

### Lot 0 — Barrière avant publication (M)

- [ ] Exécuter automatiquement les auto-tests WPF à la vraie souris : cela demande un bureau interactif. Ils sont sortis de la CI ; en attendant, exécution locale avant le tag.

### Lot 1 — Consommer les paquets comme un utilisateur (M)

- [ ] Test qui compile les extraits de code, C#, XAML et Razor, du README et de `doc/`. *(seuls les deux « Démarrage rapide » du README le sont, par `tests/consumers/run.ps1`)*
- [ ] Relancer le script avec les paquets de nuget.org (`-FromNuget -Version …`).
- [ ] Exécution en **.NET 8**, faute de runtime sur le poste (WPF net8.0 exécuté par roll-forward sur le runtime 10 ; Blazor net8.0 seulement compilé).
- [ ] Concepteur XAML de Visual Studio (manuel) : la fenêtre s'ouvre sans erreur.
- [ ] `SaveImage` Blazor (capture dans le navigateur), non vérifié par le script.

### Lot 4 — Conditions d'intégration hostiles (L)

WPF :

- [ ] DPI 100, 150 et 200 %, deux écrans de DPI différents, fenêtre déplacée d'un écran à l'autre.
- [ ] `FlowView` dans un `TabControl` (déchargement puis rechargement), un `ScrollViewer`, un `Popup`, un `Expander` fermé, un `Viewbox` et une fenêtre secondaire.
- [ ] Parent avec `LayoutTransform` ou `RenderTransform` : les clics visent toujours juste.
- [ ] Deux `FlowView` dans la même fenêtre : sélection, clavier, presse-papier et raccourcis indépendants.
- [ ] Styles implicites agressifs dans l'application (`Button`, `Control`, `TextBlock`), thème changé à chaud.
- [ ] Cinquante ouvertures et fermetures : vues et ViewModels libérés (références faibles), minuteries arrêtées.
- [ ] Exception levée dans un gestionnaire de l'application : capture relâchée, vue utilisable.
- [ ] Bureau à distance et rendu logiciel.

Blazor :

- [ ] Page défilée, `FlowView` dans un conteneur défilant, mise en page qui change au-dessus du graphe : les clics visent toujours juste.
- [ ] Parent avec `transform: scale()`, zoom du navigateur à 67 % et 150 %, `deviceScaleFactor` 2.
- [ ] `FlowView` dans un onglet masqué, une boîte de dialogue ou un conteneur de taille nulle, puis affiché.
- [ ] Deux `FlowView` sur la même page : gestes, clavier, marqueurs SVG et identifiants indépendants.
- [ ] CSS global agressif (Bootstrap, `* { box-sizing }`, styles sur `button` et `svg`).
- [ ] 50 allers-retours de navigation : écouteurs JS, minuteries et références .NET libérés, mémoire stable.
- [ ] Server avec 100 et 300 ms de latence : pas de saut au glisser, ordre des évènements respecté, double-clic toujours reconnu.
- [ ] Coupure puis reprise du circuit SignalR ; fermeture de la page pendant un geste (pas de `JSDisconnectedException`).
- [ ] Exception levée dans un gestionnaire de l'application : le geste se termine et la vue reste utilisable.

Commun :

- [ ] Culture `fr-FR` (virgule décimale) : tracés, styles, JSON, export et liaisons restent valides.

### Lot 5 — Navigateurs, périphériques et tactile (M)

WPF :

- [ ] Tactile injecté (`InjectTouchInput`) : tap, double tap, appui long, glisser, pincement (`ManipulationDelta`, jamais essayé).
- [ ] Stylet, et conflit entre les évènements tactiles et les évènements souris simulés.
- [ ] Clavier AZERTY et boutons de souris inversés.

Blazor :

- [ ] Edge, Firefox et WebKit avec Playwright (Firefox et WebKit demandent de télécharger les moteurs).
- [ ] Contexte tactile : tap, double tap, appui long, glisser, pincement ; la page ne défile pas.
- [ ] Clavier macOS (touche Meta).

Commun :

- [ ] Pavé tactile : molette à petits deltas, Ctrl + molette, défilement horizontal.

### Lot 6 — Charge et durée (S)

- [ ] WPF : reprendre `WPFLOW_PERF=N` avec les nodes à contenu riche de l'application témoin, et enregistrer des chiffres de référence pour détecter une régression.
- [ ] Blazor, dans un vrai navigateur (500, 1 500 et 5 000 nodes, avec et sans virtualisation) : premier affichage, temps par image au pan et au glisser. Les chiffres actuels viennent de bUnit.
- [ ] Blazor Server : nombre et taille des messages SignalR par seconde de glisser.
- [ ] Commun : session longue d'actions aléatoires (graine fixe) pendant 10 minutes avec invariants : aucun edge orphelin, annuler puis rétablir redonne le même graphe, aucune erreur, mémoire stable.

### Lot 7 — Liste manuelle avant chaque version majeure

| Vérification | Dernier essai |
|---|---|
| Écran tactile et stylet réels (WPF et Blazor) | jamais |
| Narrateur et NVDA (WPF et Blazor) | jamais |
| WPF sur Windows 10 et en bureau à distance | jamais |
| Blazor sur iPhone ou iPad (Safari) et téléphone Android | jamais |
| Auto-tests WPF à la vraie souris, tant qu'ils ne sont pas automatisés (lot 0) | à noter |
| Essai libre de 30 minutes de chaque application témoin | jamais |

## Trous restants des lots 2 et 3

Les applications témoins (`tests/Scenarios.*`, lot 2) et la matrice des évènements et des gestes (`GestureMatrix.cs`, lot 3) sont en place. Il leur manque :

Scénarios :

- [ ] Blazor : le tracé et le marqueur d'un type d'edge sont contrôlés au chargement, pas leur changement en cours de route.
- [ ] WPF : la molette sur une `ListBox`, un `ScrollViewer` ou un `DataGrid` sans `FlowView.NoWheel` n'est pas essayée.
- [ ] WPF : en mode contrôlé, la modification est appliquée de façon synchrone ; le cas différé (ancienne sélection déplacée à la place du node saisi) n'est couvert que par un test du noyau.
- [ ] Blazor : le clic du milieu (sélection gardée, pas de `OnPaneClick`) a un équivalent WPF non essayé.

Matrice :

- [ ] Elle ne tourne qu'en **mode contrôlé** ; en MVVM, les évènements ne sont pas rejoués un à un, et les commandes ne sont pas déclenchées à la souris une à une.
- [ ] Perte de focus : en WPF, une boîte de dialogue modale (pas un vrai Alt+Tab) ; dans le navigateur, un relâchement qui n'arrive jamais, envoyé par le protocole DevTools. `pointercancel` n'est pas essayé.
- [ ] Pas de produit complet des combinaisons et des cibles : Échap n'est essayé que pendant un glisser de node et une connexion par clics ; le label d'edge au clic et au double-clic ; la minimap au clic sur un node (pas au glisser) ; la barre d'outils au clic.
- [ ] Options non couvertes : défilement (`PanOnScroll`), pincement, `SelectionOnDrag`, `PreventScrolling`.
- [ ] WPF : `edge.right-click` a échoué une fois (menu du fond reçu), sans se reproduire aux deux passages suivants.

À décider : au glisser sur le fond, WPF efface la sélection dès l'appui, Blazor la garde (comme React Flow). L'écart est déclaré dans la matrice.

## Suivi

Ce qui est fini est retiré de cette page ; les défauts trouvés vont dans le `CHANGELOG.md`. Un lot qui ne trouve rien reste utile si ses tests sont relancés avant chaque version.
