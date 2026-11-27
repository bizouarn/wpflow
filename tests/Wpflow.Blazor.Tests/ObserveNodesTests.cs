using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Wpflow.Core;

namespace Wpflow.Blazor.Tests;

/// <summary>
///     La boucle qui (dé)branche l'observation de taille des nodes attend le pont JS à chaque node. Pendant cette attente un
///     rendu peut ajouter ou retirer des nodes : la boucle doit travailler sur une copie, sans lever d'exception.
/// </summary>
public sealed class ObserveNodesTests : FlowViewTestContext
{
    private static readonly Node C = new("c", new XYPosition(600, 0), Data: "C");

    /// <summary>
    ///     Retient les appels <c>observeNode</c> jusqu'à <see cref="Release" />, comme un aller-retour JS réel. Enveloppe le pont
    ///     de test ; les autres appels passent sans changement.
    /// </summary>
    private sealed class GatedJSRuntime(IJSRuntime inner, Gate gate) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            var result = await inner.InvokeAsync<TValue>(identifier, cancellationToken, args);
            return result is IJSObjectReference module ? (TValue)(object)new GatedModule(module, gate) : result;
        }
    }

    private sealed class GatedModule(IJSObjectReference inner, Gate gate) : IJSObjectReference
    {
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => await InvokeAsync<TValue>(identifier, default, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "observeNode") await gate.Task;
            return await inner.InvokeAsync<TValue>(identifier, cancellationToken, args);
        }

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    private sealed class Gate
    {
        private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Task => _tcs.Task;
        public void Release() => _tcs.TrySetResult();
    }

    /// <summary>Garde les erreurs que Blazor journalise : une exception dans OnAfterRenderAsync n'est pas relancée au test.</summary>
    private sealed class ErrorLog : ILoggerProvider, ILogger
    {
        public List<Exception> Errors { get; } = [];
        public ILogger CreateLogger(string categoryName) => this;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error && exception != null) Errors.Add(exception);
        }

        public void Dispose()
        {
        }
    }

    [Fact]
    public async Task Rendering_more_nodes_while_one_is_observed_does_not_break_the_observe_loop()
    {
        var log = new ErrorLog();
        Services.AddLogging(b => b.ClearProviders().AddProvider(log));
        var gate = new Gate();
        Services.AddSingleton<IJSRuntime>(_ => new GatedJSRuntime(JSInterop.JSRuntime, gate));

        var cut = Render<FlowView>(p => p
            .Add(f => f.Nodes, (IReadOnlyList<Node>)[A])
            .Add(f => f.Edges, (IReadOnlyList<Edge>)[])
            .Add(f => f.FitViewOnInit, false));

        // L'observation de "a" attend : un rendu remplace le contenu de _renderedIds par a, b et c.
        cut.Render(p => p.Add(f => f.Nodes, (IReadOnlyList<Node>)[A, B, C]));
        gate.Release();
        await Task.Delay(200);
        await cut.InvokeAsync(() => { });

        Assert.Empty(log.Errors);
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "observeNode" && i.Arguments.Contains("c"));
    }
}
