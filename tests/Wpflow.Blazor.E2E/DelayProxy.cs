using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace Wpflow.Blazor.E2E;

/// <summary>
///     Un relais TCP entre le navigateur et l'application : il retarde chaque paquet sans en changer l'ordre (latence d'un
///     serveur distant, y compris sur le WebSocket que l'émulation réseau de Chrome ne ralentit pas) et sait couper les
///     connexions (perte puis reprise du circuit SignalR).
/// </summary>
public sealed class DelayProxy : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentDictionary<TcpClient, byte> _clients = new();
    private readonly int _target;
    private volatile bool _refusing;

    public DelayProxy(string targetUrl)
    {
        _target = new Uri(targetUrl).Port;
        _listener.Start();
        Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/";
        _ = AcceptAsync();
    }

    public string Url { get; }

    /// <summary>Retard d'un aller simple ; l'aller-retour vaut le double.</summary>
    public TimeSpan Delay { get; set; }

    /// <summary>Coupe toutes les connexions et refuse les nouvelles jusqu'à <see cref="Restore" />.</summary>
    public void Cut()
    {
        _refusing = true;
        foreach (var client in _clients.Keys) client.Dispose();
        _clients.Clear();
    }

    public void Restore() => _refusing = false;

    private async Task AcceptAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (Exception e) when (e is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                return;
            }

            if (_refusing) client.Dispose();
            else _ = RelayAsync(client);
        }
    }

    private async Task RelayAsync(TcpClient client)
    {
        var server = new TcpClient();
        try
        {
            client.NoDelay = server.NoDelay = true;
            await server.ConnectAsync(IPAddress.Loopback, _target, _stop.Token);
            _clients[client] = 0;
            _clients[server] = 0;
            await Task.WhenAny(PumpAsync(client.GetStream(), server.GetStream()), PumpAsync(server.GetStream(), client.GetStream()));
        }
        catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or ObjectDisposedException
                                      or InvalidOperationException)
        {
            // Connexion coupée d'un côté : l'autre est fermé.
        }
        finally
        {
            client.Dispose();
            server.Dispose();
        }
    }

    private async Task PumpAsync(NetworkStream from, NetworkStream to)
    {
        // La lecture ne s'arrête pas pendant l'attente : le retard décale le flux, il ne réduit pas le débit.
        var queue = Channel.CreateUnbounded<(long Due, byte[] Data)>();
        var write = Task.Run(async () =>
        {
            await foreach (var (due, data) in queue.Reader.ReadAllAsync(_stop.Token))
            {
                var wait = due - Environment.TickCount64;
                if (wait > 0) await Task.Delay((int)wait, _stop.Token);
                await to.WriteAsync(data, _stop.Token);
            }
        });
        var buffer = new byte[64 * 1024];
        try
        {
            int read;
            while ((read = await from.ReadAsync(buffer, _stop.Token)) > 0)
                queue.Writer.TryWrite((Environment.TickCount64 + (long)Delay.TotalMilliseconds, buffer[..read]));
        }
        finally
        {
            queue.Writer.Complete();
            await write;
        }
    }

    public ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        Cut();
        return ValueTask.CompletedTask;
    }
}
