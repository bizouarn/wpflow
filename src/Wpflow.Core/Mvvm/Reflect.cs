using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

namespace Wpflow.Core;

/// <summary>Lecture/écriture de propriétés par nom, avec conversions numériques ; les <see cref="PropertyInfo" /> sont mises en cache par type.</summary>
internal static class Reflect
{
    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> Cache = new();

    private static PropertyInfo? Find(object target, string name) =>
        Cache.GetOrAdd((target.GetType(), name), static k =>
            k.Item1.GetProperty(k.Item2, BindingFlags.Public | BindingFlags.Instance) is { } p &&
            p.GetIndexParameters().Length == 0
                ? p
                : null);

    public static object? Get(object target, string? name) =>
        name == null ? null : Find(target, name)?.GetValue(target);

    public static double? Double(object target, string? name) => Get(target, name) switch
    {
        null => null,
        double d => double.IsNaN(d) ? null : d,
        IConvertible c => ToDouble(c),
        _ => null
    };

    private static double? ToDouble(IConvertible c)
    {
        try
        {
            var d = c.ToDouble(CultureInfo.InvariantCulture);
            return double.IsNaN(d) ? null : d;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public static bool? Bool(object target, string? name) => Get(target, name) switch
    {
        bool b => b,
        _ => null
    };

    public static string? Text(object target, string? name) => Get(target, name)?.ToString();

    /// <summary>Écrit la valeur (convertie au type de la propriété) ; ne touche pas à la propriété si elle a déjà cette valeur.</summary>
    public static void Set(object target, string? name, object? value)
    {
        if (name == null || Find(target, name) is not { CanWrite: true } property) return;
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        object? converted;
        try
        {
            converted = value switch
            {
                null => null,
                _ when type == typeof(string) => value.ToString(),
                _ when type.IsInstanceOfType(value) => value,
                _ => Convert.ChangeType(value, type, CultureInfo.InvariantCulture)
            };
        }
        catch (Exception e) when (e is InvalidCastException or FormatException or OverflowException)
        {
            return; // type non convertible : le ViewModel garde sa valeur
        }

        if (converted == null && property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) == null)
        {
            if (property.PropertyType != typeof(double)) return;
            converted = double.NaN; // une taille automatique, pour un double non nullable
        }

        if (Equals(property.GetValue(target), converted)) return;
        property.SetValue(target, converted);
    }
}
