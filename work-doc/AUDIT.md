# Audit : simplification, mutualisation, performances (novembre 2026)

Périmètre : `src/`, `tests/` et les auto-tests de `demo/`. Les constats sont classés par gain, puis par risque. Rien de ce qui est appliqué ne change l'API publique.

## Appliqué

| Constat | Gain | Risque |
|---|---|---|
| Blazor : chaque handle cherchait son état après chaque rendu, pan compris | fort (pan : 284 → 2 Ko) | faible |
| Blazor : copie absolue de chaque node, styles, classes et délégués refaits à chaque rendu | fort (glisser : 4,2 → 2,2 Mo) | faible |
| WPF : copie absolue de chaque node d'un graphe à groupes ou origine, à chaque image ; un `HashSet` par node dans `NodeTree` | fort (2,1 → 0,7 Mo) | faible |
| Noyau : la recherche du handle visé construisait et triait la liste des candidats à chaque mouvement | fort sur ce chemin (331 → 0,1 Ko) | faible (départage à distance égale testé) |
| WPF et Blazor : signature de la sélection reconstruite en chaîne à chaque changement | moyen avec une grosse sélection | faible |
| Doublons entre vues : tolérance de clic, côté opposé, tracé du fil, défilement vers un élément, report dans l'historique, suivi de la sélection → `Interaction/Shared/` | maintenance | faible |
| WPF : logique de redimensionnement écrite deux fois (`NodeResizer`, `NodeResizeControl`) → `ResizeGesture` | maintenance | faible |
| `wpflow.js` : écouteurs retirés un à un, rastérisation et téléchargement écrits deux fois | maintenance | moyen (couvert par les tests de bout en bout) |
| Code mort : champ jamais lu, paramètres inutilisés, méthodes relais, surcharge sans appelant ; message de trace des edges qui disait « nodes » | faible | nul |
| Tests : harnais bUnit en trois ou quatre exemplaires → `FlowViewTestContext` ; collecte des erreurs de page (E2E), gestes à la souris (`Sim.cs`) et variables d'environnement (`run-wpf-selftests.ps1`) mutualisés | maintenance | faible |

### Mesures

Release, meilleur de trois séries, moteur de rendu Blazor qui ignore les lots (bUnit masquait le coût réel : l'essentiel de son temps est le sien). WPF : les temps varient d'environ 0,2 ms d'une passe à l'autre, les allocations sont stables.

| Mesure | Avant | Après |
|---|---|---|
| Blazor, déplacer 1 node (1 500, à plat) | 11,0 ms, 4 176 Ko | 7,1 ms, 2 248 Ko |
| Blazor, déplacer 1 node (1 500, groupes) | 13,3 ms, 4 935 Ko | 7,7 ms, 2 492 Ko |
| Blazor, un pas de pan | 0,32 ms, 284 Ko | 0,07 ms, 2,4 Ko |
| WPF, déplacer 1 node (2 000, groupes) | 2,7 ms, 2 100 Ko | 2,0 ms, 704 Ko |
| WPF, déplacer 1 node (2 000, à plat) | 1,1 ms, 48 Ko | 1,1 ms, 47 Ko |
| WPF, déplacer 1 node (2 000, tous sélectionnés) | 1,1 ms, 89 Ko | 1,1 ms, 47 Ko |
| Noyau, handle visé (4 000 handles) | 0,36 ms, 331 Ko | 0,10 ms, 0,1 Ko |

## Vérification

Après les changements : build sans avertissement, validation d'API au `dotnet pack` réussie pour les trois paquets, noyau 157 tests, WPF 128, bUnit 145, Blazor de bout en bout 312 sur 312, et les cinq auto-tests WPF (demo, examples, perf, scenarios, matrix) dans le Bac à sable (`tests/run-wpf-selftests-sandbox.ps1`).

À surveiller : à la première passe de bout en bout, `ServerScenarioTests.Loading_another_schema_reframes_and_forgets_selection_and_history` a échoué une fois, sans que son message soit journalisé. Il ne s'est pas reproduit (seul sur les quatre hôtes, six passes de sa classe, puis la suite complète). Cause inconnue : si l'échec revient, relancer avec `--logger "console;verbosity=detailed"`.

## Écarts de comportement entre les vues (corrigés ensuite, WPF servant de référence)

L'audit les avait laissés, car les corriger change le comportement (voir `CHANGELOG.md`). `ViewParityTests` ne compare que les évènements et les réglages, et la matrice des gestes ne jouait pas ces cas : ils lui avaient échappé.

| Écart | Correction | Ce qui le détecte |
|---|---|---|
| `Node.ExpandParent` n'agissait qu'en WPF | `ParentExpansion` (`Interaction/Shared/`), appelé par les deux vues pendant un glisser | `SharedInteractionTests` (noyau), deux tests bUnit de glisser (droite et bas, gauche et haut), `P2Tests` en WPF |
| Edge court : WPF reconnectait l'extrémité la plus proche, Blazor la source | `ViewLogic.ReconnectedEnd` | geste `reconnect.nearest-end` de la matrice (`ReconnectRadius=1000` met les deux extrémités à portée), noyau, bUnit |
| Molette sur la minimap moins sensible en Blazor | `ViewLogic.MiniMapZoomFactor` | geste `minimap.wheel` (un cran zoome comme sur la surface), noyau, bUnit |
| `SelectOnly` en Blazor ignorait `ElementsSelectable` | passe par la même sélection que les gestes | bUnit, `ApiParityTests` en WPF |

Les 6 tests bUnit ajoutés échouent avec l'ancien comportement Blazor.

**Mesure de la molette.** Un `WM_MOUSEWHEEL` Windows de ±120 envoyé à Chrome (3 lignes par cran, échelle 100 %, une seule machine) donne `deltaY = ∓100` en pixels ; ±240 donne ∓200. Par cran : WPF zoomait de 2^0,24 (×1,18) sur la surface comme sur la minimap ; Blazor de 2^0,2 (×1,15) sur la surface et de 2^0,1 (×1,07) sur la minimap, soit deux fois moins. La minimap Blazor suit maintenant sa surface. Le petit écart entre les surfaces (unités de molette de chaque plateforme) reste.

Le geste `minimap.wheel` a trouvé un autre défaut Blazor, corrigé : `@onwheel:preventDefault` restait sans effet (écouteur passif), la page pouvait défiler pendant le zoom. `wpflow.js` s'en charge.

Vérification de ces corrections : build sans avertissement, noyau 169 tests, WPF 128, bUnit 151, les cinq auto-tests WPF dans le Bac à sable. Blazor de bout en bout : passe complète à 313 sur 316 ; les deux échecs de `minimap.wheel` (le défaut ci-dessus) passent après la correction, relancés avec les autres gestes de molette et de minimap (la passe complète n'a pas été refaite ensuite). Reste `WasmMvvmScenarioTests.Controls_inside_a_node_keep_their_behaviour`, en échec à chacune de ses trois passes : le navigateur reçoit un 404 sur `/_framework/dotnet-browser-tools/hot-reload-settings.json`, un fichier des outils du SDK .NET demandé en développement, et le test refuse toute erreur de console. Corrigé depuis : les erreurs de ce seul dossier sont ignorées.

## Laissé de côté

| Constat | Pourquoi |
|---|---|
| MVVM : chaque ajout à la collection source relit tous les ViewModels (N ajouts un par un : N² lectures) | la relecture complète rattrape aussi les ViewModels sans `INotifyPropertyChanged` : un ajout incrémental changerait ce comportement |
| MVVM : un `PropertyChanged` recopie la liste des records | coût linéaire, acceptable ; à revoir avec le point précédent |
| `Changesets.Diff` et `NodeSourceAdapter.Diff` se ressemblent | tous deux publics, et ils ne produisent pas les mêmes changes |
| Blazor : abonnement à `Rendered` répété dans quatre composants | une classe de base publique élargirait l'API |
| Blazor : construction de `ResizeOptions` répétée dans les deux composants de redimensionnement | quelques lignes, entre deux fichiers `.razor` |
| WPF, groupes : positions, profondeurs et descendants recalculés pour tout le graphe à chaque image (environ 0,7 Mo) | demanderait un index de la hiérarchie tenu d'un passage à l'autre |
| `FlowViewFeatureTests` (1 400 lignes) et `P2Tests` (plusieurs classes dans un fichier) | découpage sans gain de code, qui brouillerait l'historique |
| Helpers `N(...)` des tests WPF et du noyau | une ligne chacun, avec des valeurs par défaut propres à chaque classe |
| `Runner.cs` et `MatrixRunner.cs` : quelques helpers semblables | une ligne chacun ; les fusionner couplerait les deux suites |
| `using` inutiles (une quarantaine, surtout dans les tests) | sans effet ; le nettoyage toucherait presque tous les fichiers |
| `tests/consumers/` | rien à mutualiser qui vaille le risque de casser la barrière de publication |
