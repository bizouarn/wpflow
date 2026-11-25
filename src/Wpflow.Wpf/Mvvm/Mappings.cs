using Wpflow.Core;

namespace Wpflow.Wpf;

/// <summary>
///     Correspondance entre les propriétés d'un ViewModel de node et un <see cref="Node" />
///     (voir <see cref="FlowView.NodesSource" />). Les propriétés sont décrites par <see cref="NodeMappingBase" />.
/// </summary>
public sealed class NodeMapping : NodeMappingBase
{
    internal static readonly NodeMapping Default = new();
}

/// <summary>
///     Correspondance entre les propriétés d'un ViewModel d'edge et un <see cref="Edge" />
///     (voir <see cref="FlowView.EdgesSource" />). Les propriétés sont décrites par <see cref="EdgeMappingBase" />.
/// </summary>
public sealed class EdgeMapping : EdgeMappingBase
{
    internal static readonly EdgeMapping Default = new();
}
