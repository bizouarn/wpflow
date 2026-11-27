# Roadmap — portage de xyflow vers WPF

Objectif : fournir en WPF l'équivalent de [xyflow](https://github.com/xyflow/xyflow) (React Flow) : un éditeur de graphes node/edge interactif. Les décisions d'architecture sont dans [ARCHITECTURE-DECISIONS.md](ARCHITECTURE-DECISIONS.md) ; l'écart détaillé avec la bibliothèque d'origine est dans [ECART-XYFLOW.md](ECART-XYFLOW.md). Cette page ne garde que ce qui reste.

## Démo cible

**La démo (`demo/`) a sa propre identité** (elle ne copie plus la page d'accueil de reactflow.dev) : un « établi » sombre — marine, menthe, bleu ciel (les couleurs du logo) —, un bandeau d'accueil, et le même type d'exemple interactif : trois nodes de réglage à vrais contrôles WPF câblés (`smoothstep` animé) à un node de rendu, minimap, boutons de contrôle, pan/zoom, drag, connexions à la souris, sélection. Capture de référence : `screenshots/demo.png`. Une fonctionnalité n'est « finie » que quand elle est visible dans la démo ou couverte par un auto-test (`WPFLOW_SELFTEST=1`, `WPFLOW_EXAMPLES=selftest`).

## Reste à faire — impossible depuis cette machine

| Élément | Pourquoi |
|---|---|
| Lecteurs d'écran (Narrateur, NVDA) | les `AutomationPeer` sont vérifiés par code, pas avec un lecteur |

## Tests et usage réel

Plan des tests complémentaires : [ROADMAP-TESTS.md](ROADMAP-TESTS.md).

## Risques à garder en tête

- **Performance WPF** avec beaucoup d'éléments : mesurée (3 000 nodes en 3,4 s, 86 ms/image ; 5 000 virtualisés en 0,4 s) ; revérifier avec `WPFLOW_PERF=N` après tout changement du rendu.
- **Mesure des nodes** : les edges attendent la mesure des handles (cycle measure/arrange asynchrone).
- **Disposition de graphes complexes** : brancher un moteur externe ; `GraphLayout.Layered` réduit les croisements mais n'est pas dagre/ELK.
- **Divergence de modèle** : viser la parité fonctionnelle, pas syntaxique ; l'API reste optionnelle et sans effet quand elle n'est pas utilisée.
- **Cible** : WPF est Windows-only ; le noyau sans UI laisse la porte ouverte à un portage Avalonia.
