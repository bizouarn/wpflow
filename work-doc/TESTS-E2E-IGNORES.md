# Tests de bout en bout Blazor ignorés

État au 26 novembre 2026. Deux méthodes de `tests/Wpflow.Blazor.E2E` portent un attribut `Skip`, à retirer une fois la cause corrigée.

Configuration où les échecs ont été observés : Edge 158 (`WPFLOW_BROWSER_CHANNEL=msedge`, Chrome absent), Playwright .NET 1.49.0, seul le runtime ASP.NET 10 installé (`DOTNET_ROLL_FORWARD=Major`, `WPFLOW_SERVER_DEMO_TFM=net10.0`). **Ni Chrome ni .NET 8 n'ont été essayés.** `Skip` s'applique pourtant à toutes les configurations.

Avant ces attributs : 365 réussites et 8 échecs sur 373, les mêmes sur `main` avec ou sans le correctif du mode contrôlé en cours à ce moment-là.

## 1. Export PNG : le pilote Playwright s'arrête

Test ignoré : `BrowserTests.The_png_export_downloads_a_real_image` (hérité par `ServerBrowserTests` **et** `WasmBrowserTests`).

**Symptôme.** Ce test réussit, puis les 6 tests suivants de `ServerBrowserTests` échouent avec `TargetClosedException : Process exited`. En Wasm, il passait en dernier : aucun test ne le suivait.

**Cause.** Pendant un téléchargement, Edge ouvre une seconde cible de type `page`, sans URL. À la fermeture du contexte (`PagesTest.DisposeAsync`), le pilote Node de Playwright lève une rejection non gérée et s'arrête :

```
TargetClosedError: Target page, context or browser has been closed
    at FrameSession.dispose (.playwright/package/lib/server/chromium/crPage.js:497)
```

La trace de la seconde cible et la rejection ont été obtenues avec un script Node sur une page minimale, sans wpflow (téléchargement d'un fichier texte, `DEBUG=pw:protocol`). Dans ce script, quand la rejection est ignorée, Edge reste utilisable : c'est le pilote qui tombe, pas le navigateur.

**Vérification.** En ne fermant plus aucun contexte, les 18 tests de `ServerBrowserTests` passent, export compris.

**Couverture perdue.** L'export PNG de la démo (fichier `graphe.png`, signature PNG, plus de 8000 octets), en Server et en WebAssembly.

**Pistes.**
- Une version plus récente de Playwright (non essayée : rien ne dit qu'elle corrige le problème).
- Isoler ce test avec son propre pilote et tolérer l'erreur à la fermeture (non essayé).
- Ne pas retenir « ne plus fermer les contextes » : cela n'a servi qu'à prouver la cause.

## 2. Coupure réseau : fenêtre de reconnexion de Blazor 10

Test ignoré : `ServerNetworkTests.The_view_survives_a_lost_connection` (2 cas : au repos et pendant un glisser).

**Symptôme.** Délai de 20 s dépassé en attendant `#components-reconnect-modal`, trouvé mais jugé invisible.

**Cause.** Blazor 10 affiche son interface de reconnexion dans le shadow DOM de cet élément. L'hôte a une hauteur nulle ; le voile visible est `.components-reconnect-overlay.components-reconnect-visible`, à l'intérieur.

**Vérification.** Avec le sélecteur ci-dessous, les 5 tests de `ServerNetworkTests` passent, reprise du circuit comprise :

```csharp
var modal = page.Locator("#components-reconnect-modal .components-reconnect-visible");
```

**Couverture perdue.** La reprise du circuit Blazor Server après une coupure, y compris en plein glisser.

**Piste.** Ce sélecteur seul ne vise que l'interface de Blazor 10. Le correctif doit aussi accepter celle de Blazor 8 (l'élément hôte lui-même) et être vérifié avec ce runtime.
