namespace Scenarios.Blazor;

internal static class DictionaryExtensions
{
    /// <summary>Le dictionnaire, complété de <paramref name="more" /> si <paramref name="condition" />.</summary>
    public static Dictionary<string, object> With(this Dictionary<string, object> values, bool condition,
        Func<Dictionary<string, object>> more)
    {
        if (condition)
            foreach (var (key, value) in more())
                values[key] = value;
        return values;
    }
}
