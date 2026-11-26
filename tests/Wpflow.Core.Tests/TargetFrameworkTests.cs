#if NET8_0_OR_GREATER
using System.Runtime.CompilerServices;
using Wpflow.Core;

namespace Wpflow.Core.Tests;

public class TargetFrameworkTests
{
    // Sans ce renvoi, une bibliothèque compilée contre la version netstandard2.0 plante ici sur « node with { … } ».
    [Fact]
    public void IsExternalInit_is_forwarded_for_netstandard_libraries() =>
        Assert.Contains(typeof(IsExternalInit), typeof(Node).Assembly.GetForwardedTypes());
}
#endif
