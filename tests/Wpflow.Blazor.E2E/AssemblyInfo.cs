// Les classes de test passent en parallèle, six à la fois (mesuré sur un poste à 8 cœurs logiques et 32 Go) : chacune
// lance son application et son navigateur.
// Au-delà, la machine sature et les circuits SignalR tombent. Les tests sensibles au temps (latence, coupures) ou très
// lourds (graphes de 200 tables) sont dans la collection « isolated », jouée seule, après les autres.
// Moins sur un poste plus petit, une seule pour chercher un test instable : « -- xUnit.MaxParallelThreads=1 ».
[assembly: CollectionBehavior(MaxParallelThreads = 6)]

namespace Wpflow.Blazor.E2E;

/// <summary>Les tests qui ne partagent la machine avec aucun autre.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class Isolated
{
    public const string Name = "isolated";
}
