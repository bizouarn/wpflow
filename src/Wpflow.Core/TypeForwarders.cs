#if NET8_0_OR_GREATER
using System.Runtime.CompilerServices;

// Une bibliothèque compilée contre la version netstandard2.0, puis exécutée avec celle-ci, référence les accesseurs
// « init » des records avec le IsExternalInit de Wpflow.Core (voir Polyfills). Sans ce renvoi vers celui du framework,
// un « node with { … } » ou un initialiseur d'objet y lève MissingMethodException.
[assembly: TypeForwardedTo(typeof(IsExternalInit))]
#endif
