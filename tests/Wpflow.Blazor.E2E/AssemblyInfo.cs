// Chaque classe de test lance une application et un navigateur : à quatre en parallèle (démo et éditeur de schéma, Server et
// WebAssembly, dont des graphes de 200 tables), la machine sature et les circuits SignalR tombent. Les classes passent une à une.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
