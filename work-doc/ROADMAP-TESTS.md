# Roadmap : tests complémentaires et usage réel

Objectif : trouver les défauts **avant** qu'une application qui utilise la bibliothèque ne les trouve.

**WPF et Blazor sont concernés à égalité.** Chaque lot a un résultat attendu pour les deux ; un lot n'est fini que lorsque les deux le sont.

## Pourquoi

Trois défauts corrigés dans `26b32b3` sont apparus alors que les tests passaient.

| Défaut | Blazor | WPF | Pourquoi les tests ne l'ont pas vu |
|---|---|---|---|
| Double-clic sur un node jamais reçu, zoom du fond à la place (capture du pointeur) | corrigé | non reproduit à ce jour : un auto-test à la vraie souris couvre `NodeDoubleClick`, mais il ne tourne pas en CI | bUnit envoie l'évènement directement à l'élément ; aucun scénario de bout en bout ne double-cliquait. |
| Impossible de savoir quel élément d'un node a été cliqué (une colonne d'une table) | corrigé (`TargetData`) | **à corriger** : `ElementEventArgs` ne donne que le node, pas l'élément cliqué | Les démos n'ont aucun node à plusieurs zones cliquables. |
| Un fournisseur de tracé ne recevait pas l'edge | corrigé | corrigé (noyau commun), avec la même rupture d'API | Les démos et les tests donnaient le même tracé à tous les edges d'un type. |

Il manque deux choses des deux côtés : des **entrées réelles** pour chaque évènement, et une **application qui ressemble à un vrai usage**.

## État de départ (2026-11-25)

| | Blazor | WPF |
|---|---|---|
| Tests sans entrée réelle | bUnit | xUnit sur un thread STA |
| Tests avec entrées réelles | 18 scénarios, Server et WebAssembly, Chrome seul, 1280 × 800 | auto-tests à la vraie souris de la démo (`WPFLOW_SELFTEST`, `WPFLOW_EXAMPLES`) |
| Entrées réelles en CI | oui (`ci.yml`) | **non** : lancées à la main seulement |
| Entrées réelles avant publication | non | non |
| Évènements publics | 41 `EventCallback` `On…` | 45 déclarations `public event`, plus les commandes MVVM |
| Consommation des paquets | jamais testée en CI | vérifiée à la main avec une source locale |

La correction `26b32b3` est sur `main` mais dans aucune version publiée. Elle retire `EdgeGeometry.Options` : les fournisseurs de tracé personnalisés qui le lisent, WPF comme Blazor, doivent être modifiés.

## Règles

1. Un défaut trouvé en usage réel donne **d'abord un test qui échoue**, au niveau où il se produit (navigateur ou vraie souris), puis la correction.
2. Un défaut trouvé d'un côté est **cherché de l'autre** avant d'être clos.
3. Une interaction n'est couverte que par une **entrée réelle** (Playwright, souris et clavier Win32). bUnit et les tests WPF en mémoire vérifient la logique, pas le branchement.
4. Chaque test dit aussi ce qui **ne doit pas** se produire (pas de zoom au double-clic, pas de clic après un glisser).
5. Ce qui n'est pas vérifiable automatiquement va dans la liste manuelle (lot 7), avec la date du dernier essai.

## Lots

Ordre conseillé : 0, 1, 2, puis le reste. Les tailles sont relatives (S, M, L).

### Lot 0 — Barrière avant publication (M)

- [x] Blazor : `release.yml` lance les tests de bout en bout avant `Pack`. *(workflow écrit, pas encore exécuté sur GitHub)*
- [x] WPF : les auto-tests à la vraie souris rendent un **code de sortie** en échec sur toute ligne `ECHEC`, une exception ou un délai dépassé, et un script unique les enchaîne. *(`tests/run-wpf-selftests.ps1`, quatre runs ; 2026-11-25 en local : démo 24/24, exemples 86/86, performances, éditeur de schéma 78/78)*
- [ ] WPF : les exécuter automatiquement demande un bureau interactif. Essayer le runner `windows-latest`, sinon une machine auto-hébergée avec session ouverte, sinon une exécution locale obligatoire avant le tag. *(jobs écrits dans `ci.yml` et `release.yml`, à valider au premier push)*
- [x] WPF : donner l'équivalent de `TargetData` (élément d'origine du clic, du double-clic et du menu contextuel, dans les évènements et les commandes), avec un test à la vraie souris qui échoue d'abord. *(`FlowView.TargetData`, `ElementEventArgs.TargetData`/`OriginalSource`, `CommandsReceiveEventArgs`, vérifiés à la vraie souris le 2026-11-25. L'API n'existant pas, le test ne pouvait pas d'abord échouer à l'exécution)*
- [x] Activer la validation de paquet (`EnablePackageValidation` et `PackageValidationBaselineVersion`) pour les trois paquets : une rupture d'API fait échouer le pack. *(vérifié : le pack du noyau échouait sur les quatre membres retirés avant leur déclaration)*
- [x] Décider du numéro de la version qui contient `26b32b3` : **1.1.0**, avec la rupture assumée (`EdgeGeometry.Options` retiré, sans `[Obsolete]`), déclarée dans `CompatibilitySuppressions.xml` et le `CHANGELOG.md`.
- [x] Tenir un `CHANGELOG.md` avec les ruptures.

**Fini quand** : un test à entrée réelle cassé, WPF ou Blazor, ou une API retirée bloque la publication.

### Lot 1 — Consommer les paquets comme un utilisateur (M)

Un script (`tests/consumers/run.ps1`) fabrique les paquets dans une source locale, crée des projets **neufs hors de la solution** avec `dotnet new`, installe les paquets, copie le code du « Démarrage rapide » du README, publie et teste. Utiliser un cache NuGet isolé (`NUGET_PACKAGES`) et une version unique à chaque essai, pour ne pas tester un ancien paquet.

| Application WPF | À vérifier |
|---|---|
| `dotnet new wpf`, .NET 8 et .NET 10 | styles par défaut chargés sans rien déclarer, compilation sans avertissement, exécution |
| Thèmes et personnalisation | clair, sombre, ressources `wpflow-node-*`, `wpflow-edge-*` et marqueurs définies dans la fenêtre puis dans `App.xaml` |
| MVVM avec CommunityToolkit.Mvvm et ReactiveUI | liaisons, commandes, aucune erreur de liaison dans la sortie de débogage |
| `FlowView` dans un `UserControl` d'une bibliothèque tierce | résolution des ressources et des templates |
| Publication autonome en un seul fichier | démarrage, thèmes, export d'image |
| Concepteur XAML de Visual Studio (manuel) | la fenêtre s'ouvre sans erreur |

| Application Blazor | À vérifier |
|---|---|
| Web App, `InteractiveServer`, avec et sans pré-rendu | premier rendu, aucune erreur console |
| Web App, interactivité par page (pas globale) | arrivée et départ de la page du graphe par navigation améliorée |
| Web App, `InteractiveAuto` | premier chargement (Server) puis suivant (WebAssembly) |
| WebAssembly autonome, `dotnet publish` avec trimming | JSON, export, réflexion après élagage |
| Application sous un sous-chemin (`<base href="/outil/">`) | chargement de `wpflow.js` et `wpflow.css` |
| Server publié, `ASPNETCORE_ENVIRONMENT=Production` | ressources `_content/Wpflow.Blazor/` servies |
| .NET 8 et .NET 10 | compilation sans avertissement, exécution |

- [x] Script et applications témoins WPF. *(2026-11-25 : 8/8 en local)*
- [x] Script et applications témoins Blazor. *(2026-11-25 : 7/7 en local avec Edge)*
- [ ] Test qui compile les extraits de code, C#, XAML et Razor, du README et de `doc/`. *(seuls les deux « Démarrage rapide » du README le sont, par le script)*
- [ ] Après publication : relancer le script avec les paquets de nuget.org (`-FromNuget -Version …`).

**Fini quand** : le script passe en CI sur les paquets du build, sans aucune référence de projet. *(job `consumers` écrit dans `ci.yml`, pas encore exécuté sur GitHub)*

Restent hors du script : le concepteur XAML de Visual Studio (manuel) et l'**exécution en .NET 8**, faute de runtime sur le poste (WPF net8.0 exécuté par roll-forward sur le runtime 10 ; Blazor net8.0 seulement compilé).

Défauts trouvés (2026-11-25), tous deux WPF, corrigés avec un test d'abord en échec :

1. `RenderToBitmap` / `SaveImage` : image prise avec le viewport courant (décalée, tronquée ou blanche). Le test existant ne contrôlait que la taille du bitmap. Côté Blazor, l'export SVG est produit depuis le modèle et n'est pas concerné par cette cause ; `SaveImage` Blazor (capture dans le navigateur) n'est pas vérifié par le script.
2. MVVM : `UpdateNode` n'écrivait rien dans le ViewModel, `UpdateEdge` seulement les extrémités. Blazor n'a pas d'adaptateur MVVM.

Ticket d'API : `FlowView.ApplyNodeChanges` / `ApplyEdgeChanges` sont publics en Blazor, mais internes en WPF (visibles des seuls tests du dépôt).

### Lot 2 — Application témoin réaliste : éditeur de schéma de base (L)

Le même scénario dans deux applications de test, plus exigeantes que les démos : `tests/Scenarios.Wpf/` (pilotée à la vraie souris et au clavier) et `tests/Scenarios.Blazor/` (pilotée par Playwright, Server et WebAssembly). Les modèles et les règles métier sont communs.

Commun :

- [x] Tables : nodes à lignes, un handle source et un handle cible **par colonne**.
- [x] Savoir quelle colonne est cliquée, double-cliquée ou visée par le menu contextuel.
- [x] Colonnes ajoutées, retirées, réordonnées, node replié : les handles sont remesurés et les edges suivent.
- [x] Double-clic sur une colonne pour renommer dans un champ : saisie, Suppr, flèches, Ctrl+A, Ctrl+Z et Espace restent dans le champ.
- [x] Menu contextuel par table, par colonne, par relation, sur le fond et sur une sélection.
- [ ] Types d'edges qui varient selon `Edge.Data` (cardinalités, marqueurs, labels cliquables).
- [x] `IsValidConnection` selon le type des colonnes.
- [x] Mode contrôlé : l'état vit dans un service, avec refus de certains changes.
- [x] Chargement asynchrone après le premier affichage, puis rechargement d'un autre schéma (cadrage, historique, sélection).
- [x] Enregistrement et relecture JSON avec un `Data` typé (il revient en `JsonElement`).
- [x] Mise à jour par minuterie ou tâche de fond **pendant** un glisser ou une connexion.
- [x] 200 tables et 400 relations, avec et sans virtualisation.

Propre à WPF :

- [x] `ListBox`, `DataGrid`, `ComboBox`, `TextBox`, `ScrollViewer` et boutons dans un node : molette, menu déroulant, saisie et focus restent au contrôle.
- [ ] Le même scénario en MVVM (`NodesSource`, commandes) : colonnes en `ObservableCollection`, modifications venant d'un autre thread renvoyées sur le thread d'interface.
- [x] `ContextMenu` et `ToolTip` WPF posés dans les templates ; glisser-déposer depuis une liste de tables.

Propre à Blazor :

- [x] Boutons, listes, cases à cocher, zone défilante (`nowheel`), lien et texte sélectionnable dans un node.

**Fini quand** : chaque ligne a un scénario sur WPF, Blazor Server et Blazor WebAssembly. Tout besoin impossible sans contourner la bibliothèque devient un ticket d'API.

**État (2026-11-25).** Modèles et règles : `tests/Scenarios.Shared`. Blazor : `tests/Scenarios.Blazor` (hôtes `.Server` et `.Wasm`), scénarios dans `tests/Wpflow.Blazor.E2E/ScenarioTests.cs` sur Server et WebAssembly (Edge) ; les classes de bout en bout passent une à une : à quatre en parallèle, la machine de 8 Go saturait et les circuits tombaient. WPF : `tests/Scenarios.Wpf` (scénarios dans `Runner.cs`, 78 vérifications à la vraie souris, lancés par `tests/run-wpf-selftests.ps1` ; `WPFLOW_SCENARIO=<nom>` n'en lance qu'un).

Deux lignes restent ouvertes :

- **Types d'edges** : complet en Blazor. En WPF, le label cliquable est essayé à la souris et le fournisseur est vérifié hors de la vue, mais ni le tracé dessiné ni les marqueurs.
- **MVVM** : le scénario WPF couvre le réordonnancement (`ObservableCollection.Move`), la modification venue d'un autre thread et le glisser, pas tout le scénario ni les commandes.

Limites des scénarios WPF : en mode contrôlé, `Undo()` ne fait rien en WPF, donc « Ctrl+Z reste dans le champ » n'y prouve que le comportement du champ ; la `ListBox`, le `ScrollViewer` et le `DataGrid` portent `FlowView.NoWheel` (équivalent de `nowheel`), la molette sans cette marque n'est pas essayée ; la modification en mode contrôlé est appliquée de façon synchrone (le cas différé du défaut 4 n'est couvert que par le test du noyau) ; un seul DPI (100 %).

Écarts de parité à traiter au lot 3 :

- Au clic droit sur une sélection, WPF lève le menu de sélection **et** celui du node, Blazor seulement celui de la sélection.
- Au double-clic, WPF lève `NodeClick` **après** `NodeDoubleClick` (au second relâchement) ; Blazor lève les clics avant.
- En mode contrôlé, l'historique de la vue Blazor reste actif, celui de WPF non.
- `ClearHistory()` est public en Blazor, privé en WPF (ticket d'API).

Défauts trouvés côté Blazor, corrigés, chacun avec un scénario d'abord en échec :

1. **Server** : circuit coupé quand les mesures dépassent 32 Ko en un message. *WPF : sans objet.*
2. Handles déplacés sans changement de taille du node : edges laissés à l'ancienne place. *WPF : **confirmé** à la vraie souris. Les colonnes ajoutées, retirées, réordonnées ou repliées sont suivies, mais pas un déplacement seul (marge changée) : `FlowView.UpdateNodeInternals(id)` ajouté, comme `useUpdateNodeInternals`.*
3. `@onkeydown` sur un champ dans un node jamais reçu. *WPF : non concerné (renommage au clavier réussi).*
4. Mode contrôlé : l'ancienne sélection déplacée à la place du node saisi. *Noyau commun, corrigé aussi en WPF.*
5. Clic droit ou milieu : sélection vidée et `OnPaneClick` levé ; menu de sélection inaccessible. *WPF : non concerné pour le clic droit (sélection gardée, menu de sélection reçu) ; clic milieu non essayé.*

Comportements voulus, ajoutés à `doc/blazor.md` : modèles de nodes mémorisés (l'état d'affichage va dans un composant enfant) ; `ClearHistory()` après le chargement d'un autre document.

Écart de parité à traiter au lot 3 : au clic droit sur une sélection, WPF lève le menu de sélection **et** celui du node, Blazor seulement celui de la sélection.

### Lot 3 — Matrice des évènements et des gestes (L)

- [ ] Un journal d'évènements dans chaque application témoin ; chaque évènement est déclenché par une entrée réelle, avec contrôle de son contenu, de son **nombre d'appels** et de son ordre (début, déplacement, fin).
- [ ] Un test de garde par réflexion échoue si un évènement public manque à la matrice : les `event` et les propriétés `…Command` côté WPF, les `EventCallback` côté Blazor.
- [ ] Combinaisons : simple et double-clic, clic droit, clic milieu, Ctrl/Maj + clic, glisser sous et au-delà du seuil, relâchement hors de la fenêtre, Échap pendant un geste, perte de focus (Alt+Tab).
- [ ] Les mêmes sur node, edge, label d'edge, handle, fond, minimap, toolbar et élément interactif dans un node.
- [ ] Chaque option qui coupe une interaction (`IsLocked`, déplaçable, sélectionnable, connectable…) est vérifiée avec une entrée réelle.
- [ ] Un tableau de parité : un même geste lève les mêmes évènements, dans le même ordre, en WPF et en Blazor ; chaque écart voulu est documenté.
- [ ] WPF : perte de la capture souris (`LostMouseCapture`), par exemple quand un gestionnaire de clic ou de double-clic ouvre une boîte de dialogue.

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

- [ ] Edge, Firefox et WebKit avec Playwright, en CI (Firefox et WebKit demandent de télécharger les moteurs).
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

## Suivi

Pour chaque lot terminé : cocher, noter la date et les défauts trouvés. Un lot qui ne trouve rien reste utile s'il tourne en CI.
