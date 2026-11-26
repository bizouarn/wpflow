// API du BCL absentes de .NET Framework 4.7.2 et de .NET Standard 2.0, réécrites en internal sous leur nom d'origine : le
// code appelant est le même pour toutes les cibles, et la version .NET 8 (rien de ce fichier n'y est compilé) garde les
// API natives. Compilé dans Wpflow.Core et lié dans Wpflow.Wpf.
#if NETFRAMEWORK || NETSTANDARD2_0
#nullable enable

namespace System.Runtime.CompilerServices
{
    // Demandé par le compilateur pour les accesseurs « init » des records.
    internal static class IsExternalInit { }
}

namespace System
{
    // Demandé par le compilateur pour « liste[^1] ».
    internal readonly struct Index
    {
        private readonly int _value;

        public Index(int value, bool fromEnd = false) => _value = fromEnd ? ~value : value;

        public bool IsFromEnd => _value < 0;
        public int Value => _value < 0 ? ~_value : _value;
        public int GetOffset(int length) => IsFromEnd ? length - Value : Value;

        public static implicit operator Index(int value) => new(value);
    }
}

namespace System.Collections.Generic
{
    internal sealed class ReferenceEqualityComparer : IEqualityComparer<object?>
    {
        public static ReferenceEqualityComparer Instance { get; } = new();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
        public int GetHashCode(object? obj) => Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj!);
    }

    internal static class CollectionPolyfills
    {
        public static bool TryAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
            where TKey : notnull
        {
            if (dictionary.ContainsKey(key)) return false;
            dictionary.Add(key, value);
            return true;
        }

        public static TValue? GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key) =>
            dictionary.TryGetValue(key, out var value) ? value : default;

        public static TValue GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key,
            TValue fallback) => dictionary.TryGetValue(key, out var value) ? value : fallback;

        public static bool Remove<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, out TValue? value)
            where TKey : notnull
        {
            if (!dictionary.TryGetValue(key, out value)) return false;
            dictionary.Remove(key);
            return true;
        }

        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
        {
            key = pair.Key;
            value = pair.Value;
        }
    }
}

namespace System.Linq
{
    internal static class LinqPolyfills
    {
        public static IEnumerable<(TFirst First, TSecond Second)> Zip<TFirst, TSecond>(this IEnumerable<TFirst> first,
            IEnumerable<TSecond> second) => first.Zip(second, (a, b) => (a, b));

#if NETSTANDARD2_0
        // Présent dans .NET Framework 4.7.2, absent de .NET Standard 2.0.
        public static Collections.Generic.HashSet<T> ToHashSet<T>(this IEnumerable<T> source) => new(source);
#endif
    }
}
#endif
