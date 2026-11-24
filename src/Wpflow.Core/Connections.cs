namespace Wpflow.Core;

/// <summary>Mode de connexion (<c>connectionMode</c>) : <c>Strict</c> = source vers cible uniquement ; <c>Loose</c> = tout handle vers tout handle.</summary>
public enum ConnectionMode
{
    Strict,
    Loose
}

public static class Connections
{
    /// <summary>Le handle de départ peut-il se connecter à un handle de type <paramref name="to" /> ?</summary>
    public static bool AreTypesCompatible(HandleType from, HandleType to, ConnectionMode mode) =>
        mode == ConnectionMode.Loose || from != to;

    /// <summary>
    ///     Construit la connexion en l'orientant source → cible. Comme dans xyflow, l'orientation ne dépend que du type du
    ///     handle de <b>départ</b> : un départ « target » donne une connexion inversée.
    /// </summary>
    public static Connection Create(string fromNode, string? fromHandle, HandleType fromType,
        string toNode, string? toHandle) =>
        fromType == HandleType.Source
            ? new Connection(fromNode, toNode, fromHandle, toHandle)
            : new Connection(toNode, fromNode, toHandle, fromHandle);

    /// <summary>Une connexion est valide si elle est acceptée par le prédicat utilisateur (s'il y en a un).</summary>
    public static bool IsValid(Connection connection, Func<Connection, bool>? isValidConnection) =>
        isValidConnection?.Invoke(connection) ?? true;
}
