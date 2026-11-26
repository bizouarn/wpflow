# Version Blazor (branche `blazor`)

Objectif : le même éditeur de graphes que la version WPF, rendu par Blazor (HTML/SVG), au-dessus du noyau `Wpflow.Core` (modèle immuable, changes, chemins d'edges, viewport, connexions, JSON, presse-papier, disposition). Aucune logique de graphe n'est dupliquée : seuls le rendu et les gestes sont propres à Blazor. Documentation utilisateur : [doc/blazor.md](../doc/blazor.md).

**État : la parité fonctionnelle avec WPF est atteinte** (voir « Écarts restants » pour ce qui diffère volontairement ou n'est pas vérifiable ici).

## Structure

- `src/Wpflow.Blazor` — bibliothèque de composants Razor (net8.0, `Microsoft.NET.Sdk.Razor`, dépend de `Microsoft.AspNetCore.Components.Web`, donc utilisable en Blazor Server **et** WebAssembly) ; ressources statiques `_content/Wpflow.Blazor/wpflow.css` et `wpflow.js`.
- `demo/blazor-shared/` — le composant `Demo` et sa feuille de style, partagés par les deux hôtes (`_content/demo-blazor-shared/demo.css`).
- `demo/blazor/` — hôte Blazor Server : `ASPNETCORE_ENVIRONMENT=Development dotnet run --project demo/blazor --urls http://localhost:5189` (en `Production`, les ressources des bibliothèques ne sont pas servies).
- `demo/blazor-wasm/` — hôte Blazor WebAssembly (serveur de développement) : `dotnet run --project demo/blazor-wasm --urls http://localhost:5190`.
- `tests/Wpflow.Blazor.Tests` — xUnit + bUnit (net8.0) : `dotnet test tests/Wpflow.Blazor.Tests`.
- `tests/Wpflow.Blazor.E2E` — xUnit + Playwright pilotant le **Chrome installé** (aucun téléchargement de navigateur) contre la démo lancée en vrai : `dotnet test tests/Wpflow.Blazor.E2E` (les deux hôtes, 14 scénarios chacun).

## Architecture

- **Logique partagée avec WPF** : la recherche du handle visé, les extrémités d'edges, les contraintes de déplacement, la sélection (clic, rectangle, annonces), l'historique, la suppression, la molette, le défilement automatique, le cadrage, la virtualisation, la minimap, les z-index, la rotation, les flèches clavier et les marqueurs viennent de `Wpflow.Core/Interaction/` (voir `ARCHITECTURE-DECISIONS.md`). `FlowView` Blazor ne garde que les gestes (pointeur via JS), le rendu HTML/SVG et les `EventCallback`.
- **Même modèle d'état que React Flow et que la version WPF** : `FlowView` traduit les interactions en `NodeChange` / `EdgeChange`, les publie (`OnNodesChange` / `OnEdgesChange`) puis, si `AutoApplyChanges`, les applique avec `Changes.Apply` et lève `NodesChanged` / `EdgesChanged` (`@bind-Nodes`, `@bind-Edges`, `@bind-Viewport`).
- **Fichiers de `FlowView`** (classe partielle) : `FlowView.razor` (balisage : fonds, couche mémorisée des edges SVG, des nodes et des labels, boîte de sélection), `FlowView.razor.cs` (données, cycle de vie, callbacks JS, rendu : virtualisation, marqueurs, edges et leur cache, minimap), `FlowView.Options.cs` (paramètres et évènements), `FlowView.Gestures.cs` (pan, glisser, connexion, reconnexion, redimensionnement, rotation, rectangle de sélection, défilement automatique), `FlowView.Editing.cs` (changes, sélection, suppression, presse-papier, historique, clavier, double-clic / menu / survol), `FlowView.Api.cs` (viewport animé, requêtes, JSON, export), `FlowTypes.cs` (arguments d'évènements, DTO de mesure) ; composants `FlowLayer` (mémorisation), `FlowNodeHost` (un node), `FlowHandle`, `FlowDefaultNode`, `NodeResizer`, `NodeResizeControl`, `NodeRotator`, toolbars, `FlowControls`, `FlowMiniMap`, `FlowPanel`.
- **Rendu** : un `div` racine, un calque `wf-viewport` transformé par le `Viewport` (CSS `translate` + `scale`), un `svg` pour les edges (chemins tracés par `EdgePathProviders`, marqueurs SVG, zones de clic larges), les nodes en `div` positionnés par `transform` (positions absolues, `NodeOrigin`, rotation), labels d'edges en HTML. Le fond est un `pattern` SVG décalé par le viewport (plusieurs couches possibles).
- **Performance du rendu** : un pas de pan ne change que la transformation du calque, et `FlowLayer` ne refait alors ni les nodes ni les edges (sauf avec `OnlyRenderVisibleElements`, où l'ensemble visible dépend du viewport) ; chaque node est un composant (`FlowNodeHost`) qui ne se rend que si son `Node`, son style, sa classe ou son modèle ont changé ; le tracé d'un edge est mis en cache tant que l'edge, ses extrémités et les réglages de la vue sont inchangés (déplacer un node ne refait que ses edges) ; les handles se mettent à jour seuls pendant un fil. Tout autre changement passe par `Refresh()` (ou `_forceLayer`), qui refait la couche. Mesure (bUnit, Release, 1 500 nodes et 1 499 edges, `PerfTests`) : un pas de pan 101 → 33 ms, déplacer un node 184 → 70 ms. Ces temps comprennent surtout le travail de bUnit. Avec un moteur de rendu qui ignore les lots (le coût de la bibliothèque et de la comparaison de Blazor seuls), même graphe : un pas de pan 0,07 ms et 2 Ko alloués, déplacer un node 7 ms et 2,2 Mo. Pour le garder : les copies en coordonnées absolues, les styles et les classes des nodes ne sont refaits que quand leur node change, et un handle ne cherche son état que pendant qu'un fil est tiré.
- **Pont JS minimal (`wpflow.js`)** : `ResizeObserver` pour mesurer les nodes et leurs handles (par lots, divisés par le zoom) → `NodeDimensionsChange` ; rectangle du conteneur ; molette (écouteur non passif) ; capture du pointeur et suivi des déplacements pendant un geste ; pincement à deux pointeurs ; indicateurs d'appui (`nodrag`, `nopan`, `DragHandle`, contrôle HTML) envoyés avant l'évènement Blazor (`OnPressInfo`) ; touches à neutraliser (Espace, flèches, Ctrl+A/Z/Y) ; export : capture du rendu réel (`saveImageFromDom` : le calque est cloné avec ses styles calculés dans un `foreignObject`, puis rastérisé) ou dessin vectoriel simplifié (`saveImage`).
- **Gestes en C#** : calculés en coordonnées flow (le défilement automatique près des bords, un minuteur de 16 ms, reste donc correct) ; seuils par geste ; historique enregistré au début d'une modification (glisser, redimensionner, pivoter, supprimer, connecter, coller, reconnecter).
- **Les écouteurs coûteux (survol, menu contextuel, mouvement du pointeur hors geste) ne sont attachés que si un gestionnaire existe** : sinon Blazor Server enverrait chaque évènement au serveur.
- **Thème** : variables CSS `--wf-*` (`Dark="true"` pour le thème sombre).

## Parité avec la version WPF

Fait : tout ce que couvre `ECART-XYFLOW.md` côté interface, sauf les points de la section suivante : options d'interaction, clavier, pan / zoom / pincement, glisser (seuils, aimantation, extents, sélection multiple, `DragHandle`), connexion (strict / `Loose`, validateurs de vue et de handle, rayon, par clics, reconnexion, ligne personnalisable avec son tracé), sélection (clic, Maj/Ctrl, rectangle partiel / complet, `SelectionOnDrag`), redimensionnement (`NodeResizer`, `NodeResizeControl`), rotation, presse-papier, annuler/rétablir, suppression (`OnBeforeDelete`, descendants), nodes enfants / groupes, edges (5 types, types et gabarits personnalisés, labels, marqueurs personnalisés, options de marqueur, animation, interaction), toolbars (node, edge, sélection), panneaux, contrôles (verrou), minimap (couleurs, pannable, zoomable, clic), fonds superposés, virtualisation, ARIA et annonces de sélection, évènements (clics, double-clics, menus, survol, glisser, connexion, mouvement du viewport, init, erreurs, dépôt), API d'instance (viewport animé, requêtes, mises à jour, JSON, export d'image fidèle).

## Écarts restants

| Élément | Raison |
|---|---|
| Adaptateur MVVM (`NodesSource`, mappages, `IFlowController`, `IFlowHistory`) | ✅ logique commune dans le noyau ; les `EventCallback` tiennent lieu de commandes, voir [doc/blazor.md](../doc/blazor.md#mvvm) |
| Export d'image | 🟡 le rendu est capturé par le navigateur (Chrome vérifié) ; le contenu non sérialisable en SVG (canvas, vidéo, images d'une autre origine) n'apparaît pas ; `vector: true` donne le dessin simplifié |
| Lecteurs d'écran (Narrateur, NVDA, VoiceOver) | non vérifiables ici : rôles ARIA et région `aria-live` vérifiés par les tests, pas avec un lecteur |
| Navigateurs autres que Chrome, vrai écran tactile | le pincement est vérifié avec des évènements tactiles simulés par Chrome (CDP), pas sur du matériel |
| Publication NuGet | `release.yml` empaquette et publie `Wpflow.Blazor` avec `Wpflow.Core` et `Wpflow.Wpf` (même version, tirée du tag) ; le premier tag publié après la fusion de la branche `blazor` créera le package |

## Tests

- `tests/Wpflow.Blazor.Tests` : 145 tests bUnit sans navigateur (socle commun : `FlowViewTestContext`) ; le pont JS est simulé (mode souple) et les callbacks que JS appellerait (`OnMeasured`, `OnPointerMove`, `OnPointerUp`, `OnWheel`, `OnPinch`, `OnRootRect`) sont invoqués directement. Rendu, mesure, glisser, pan, zoom, connexion, sélection, suppression, marqueurs, thème, culture invariante, historique, presse-papier, redimensionnement, rotation, reconnexion, connexion par clics, validateurs, clavier, boutons de pan, rectangle de sélection, verrou, extents, évènements, modèles d'edges / de labels / de fil, toolbars, panneaux, contrôles, minimap, fonds, marqueurs, virtualisation, z-index, origine, JSON, export, défilement automatique. `PerfTests` affiche les temps de rendu sans jamais échouer.
- `tests/Wpflow.Blazor.E2E` : 28 scénarios (14 × Blazor Server et WebAssembly) dans un vrai Chrome : mesure et tracé des edges, glisser et annuler, connexion, molette et pan, curseur dans un node (`input` : le node ne bouge pas), `DragHandle`, sélection / redimensionnement / rotation, clavier (tout sélectionner, flèches, supprimer), rectangle de sélection, défilement automatique près du bord, pincement tactile, thème / contrôles / verrou, export PNG (fichier PNG réel d'au moins 8 Ko), recentrage par la minimap. `WPFLOW_E2E_OUT=chemin.png` copie l'image exportée pour la regarder.

## Reste à faire

Rien de planifié. À surveiller : la performance au-delà de quelques milliers de nodes (activer `OnlyRenderVisibleElements`), et les lecteurs d'écran.

## Pièges rencontrés

- Une bibliothèque Razor hébergée à la fois en Server et en WebAssembly doit référencer le paquet `Microsoft.AspNetCore.Components.Web`, pas `FrameworkReference Microsoft.AspNetCore.App` (« aucun pack d'exécution pour browser-wasm »).
- Les ressources statiques d'une bibliothèque sont servies sous `_content/{nom du projet}/` : `demo-blazor-shared` pour le projet du même nom (pas son espace de noms).
- Blazor n'invoque pas `SetParametersAsync` sur un composant sans paramètre quand son parent se re-rend : minimap, contrôles, barre de sélection et handles s'abonnent à `FlowView.Rendered`.
- Les nombres dans le balisage passent par `FlowView.F` (culture invariante), sinon la culture française écrit des virgules dans les styles et attributs SVG.
- `dotnet sln add` (SDK 11) refuse d'ajouter un projet dans le dossier solution `src` : les projets Blazor sont à la racine de la solution (`--in-root`).
- Un `EventCallback` sans délégué n'attache pas l'écouteur : on s'en sert pour les gestionnaires de survol facultatifs (un `Func` nul déclenche un avertissement de nullabilité).
- Capturer le pointeur sur la racine retarget le `click` : les contrôles HTML des nodes sont donc exclus du suivi (pas de capture) et seulement sélectionnent le node.
- Une `ProjectReference` Razor a besoin de `GlobalUsings.cs` pour `Wpflow.Core` dans les fichiers C# (les `_Imports.razor` ne s'appliquent qu'aux fichiers `.razor`).
- Mémoriser un composant parent (`FlowLayer`) sans refaire ses enfants impose que tout changement autre qu'un pas de pan force la couche (`Refresh()`) ; un composant enfant dont l'état change seul (handle) doit s'abonner à `Rendered`.
- Un test de pincement doit partir d'un zoom assez bas : la vue démarre cadrée (≈ 1,5) et le zoom maximal est 2.
