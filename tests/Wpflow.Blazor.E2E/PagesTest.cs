namespace Wpflow.Blazor.E2E;

/// <summary>
///     Ferme après chaque test les pages qu'il a ouvertes : laissées là, elles gardent un circuit ou un runtime actif jusqu'à
///     la fin de la classe, et prennent le processeur aux classes qui passent en même temps.
/// </summary>
public abstract class PagesTest(DemoFixture fixture) : IAsyncLifetime
{
    /// <summary>L'application et le navigateur de la classe.</summary>
    protected DemoFixture App { get; } = fixture;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => App.CloseContextsAsync();
}
