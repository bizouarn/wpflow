# Roadmap — portage de xyflow vers WPF

Objectif : fournir en WPF l'équivalent de [xyflow](https://github.com/xyflow/xyflow) (React Flow) : un éditeur de graphes node/edge interactif. Les décisions d'architecture sont dans [ARCHITECTURE-DECISIONS.md](ARCHITECTURE-DECISIONS.md) ; l'écart détaillé avec la bibliothèque d'origine est dans [ECART-XYFLOW.md](ECART-XYFLOW.md).

**Tout ce qui était planifié est fait** (noyau, surface et viewport, interactions, composants annexes, performance, accessibilité de base, thèmes, JSON, NuGet, personnalisation par ressources, MVVM, lots P1 à P3 de l'écart). Cette page ne garde que ce qui reste.

## Démo cible

**La démo (`demo/`) doit rester proche de celle de la page d'accueil de [reactflow.dev](https://reactflow.dev/)** : fond en points, nodes à l'apparence React Flow, edges bézier avec flèches et edges animés, minimap, boutons de contrôle, pan/zoom fluides, drag, connexions à la souris, sélection. Capture de référence : `screenshots/demo.png`. Une fonctionnalité n'est « finie » que quand elle est visible dans la démo ou couverte par un auto-test (`WPFLOW_SELFTEST=1`, `WPFLOW_EXAMPLES=selftest`).

## Reste à faire — impossible depuis cette machine

| Élément | Pourquoi |
|---|---|
| Pincement tactile (`ManipulationDelta`) | implémenté, jamais essayé sur un écran tactile |
| Lecteurs d'écran (Narrateur, NVDA) | les `AutomationPeer` sont vérifiés par code, pas avec un lecteur |
| CI GitHub Actions (`.github/workflows/ci.yml`) | écrite, jamais exécutée : aucun dépôt distant n'est configuré |
| Publication des packages NuGet | `Wpflow.Core` et `Wpflow.Wpf` 0.1.0 se construisent (`dotnet pack`) et s'utilisent depuis un projet tiers (vérifié avec une source locale) ; reste à les publier (clé d'API, dépôt) |

## Reste à faire — faisable

Rien d'identifié. Les écarts restants avec React Flow, tous volontaires ou sans objet en WPF, sont dans [ECART-XYFLOW.md](ECART-XYFLOW.md) (§ « Écarts restants »). Pour des graphes complexes, brancher un moteur de disposition externe : `GraphLayout.Layered` réduit les croisements mais n'est pas dagre/ELK.

## Risques à garder en tête

- **Performance WPF** avec beaucoup d'éléments : mesurée (3 000 nodes en 3,4 s, 86 ms/image ; 5 000 virtualisés en 0,4 s) ; revérifier avec `WPFLOW_PERF=N` après tout changement du rendu.
- **Mesure des nodes** : les edges attendent la mesure des handles (cycle measure/arrange asynchrone).
- **Divergence de modèle** : viser la parité fonctionnelle, pas syntaxique ; l'API reste optionnelle et sans effet quand elle n'est pas utilisée.
- **Cible** : WPF est Windows-only ; le noyau sans UI laisse la porte ouverte à un portage Avalonia.
