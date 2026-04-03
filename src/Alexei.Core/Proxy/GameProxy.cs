using Alexei.Core.Config;
using Alexei.Core.Diagnostics;
using Alexei.Core.GameState;
using Alexei.Core.Protocol;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;

namespace Alexei.Core.Proxy;

/// <summary>
/// Game MITM proxy. Listens on localhost, relays to real game server.
/// Shadow-decrypts S2C packets for BotEngine analysis.
/// Real game endpoint is updated dynamically from LoginProxy's ServerList discovery.
/// </summary>
public sealed class GameProxy
{
    private readonly int _listenPort;
    private readonly GameWorld _world;
    private readonly PacketDispatcher _dispatcher;
    private readonly ServerEntry _serverEntry;
    private readonly ILogger? _logger;
    private readonly PacketEvidenceCollector? _collector;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    private readonly object _endpointLock = new();
    private string _realHost;
    private int _realPort;

    public ProxySession? ActiveSession { get; private set; }
    public PacketSender? Sender { get; private set; }

    public event Action? ClientConnected;
    public event Action? ClientDisconnected;
    public event Action<PacketSender>? SenderReady;
    public event Action<string>? Log;

    public GameProxy(ServerEntry server, int listenPort, GameWorld world, PacketDispatcher dispatcher, ILogger? logger = null, PacketEvidenceCollector? collector = null)
    {
        _realHost = server.GameHost;
        _realPort = server.GamePort;
        _listenPort = listenPort;
        _serverEntry = server;
        _world = world;
        _dispatcher = dispatcher;
        _logger = logger;
        _collector = collector;
    }

    /// <summary>
    /// Update real game server endpoint (called from LoginProxy discovery).
    /// </summary>
    public void UpdateEndpoint(string host, int port)
    {
        lock (_endpointLock)
        {
            _realHost = host;
            _realPort = port;
        }
        Log?.Invoke($"Game endpoint updated: {host}:{port}");
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _listener = new TcpListener(IPAddress.Loopback, _listenPort);
        _listener.Start();
        _logger?.LogInformation("GameProxy listening on :{Port}", _listenPort);
        Log?.Invoke($"GameProxy listening on :{_listenPort}");

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                _ = HandleClientAsync(client, _cts.Token);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _listener.Stop();
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        ActiveSession = null;
        Sender = null;
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        ClientConnected?.Invoke();
        Log?.Invoke("Game client connected");
        _world.Reset();
        _world.IsConnected = true;

        // Read dynamically discovered endpoint
        string host;
        int port;
        lock (_endpointLock)
        {
            host = _realHost;
            port = _realPort;
        }

        using var server = new TcpClient();
        try
        {
            Log?.Invoke($"Connecting to real server {host}:{port}...");
            await server.ConnectAsync(host, port, ct);
            Log?.Invoke("Connected to real server");

            var session = new ProxySession(client, server, _world, _dispatcher, _serverEntry, _logger, _collector);
            session.Log += msg => Log?.Invoke(msg);
            session.SenderReady += sender =>
            {
                Sender = sender;
                SenderReady?.Invoke(sender);
            };
            ActiveSession = session;

            await session.RunAsync(ct);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Game error: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _world.IsConnected = false;
            ActiveSession = null;
            Sender = null;
            client.Close();
            ClientDisconnected?.Invoke();
            Log?.Invoke("Game client disconnected");
            _world.NotifyUpdated();
        }
    }
}
