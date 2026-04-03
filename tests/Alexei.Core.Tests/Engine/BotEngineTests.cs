using System.Net;
using System.Net.Sockets;
using Alexei.Core.Config;
using Alexei.Core.Crypto;
using Alexei.Core.Engine;
using Alexei.Core.GameState;
using Alexei.Core.Proxy;
using Xunit;

namespace Alexei.Core.Tests.Engine;

public sealed class BotEngineTests
{
    [Fact]
    public void Start_CapturesCurrentPosition_AsNewAnchor_And_Stop_ClearsIt()
    {
        var world = new GameWorld();
        var profileManager = new ProfileManager(Path.GetTempPath());
        var engine = new BotEngine(world, profileManager);

        world.Me.ObjectId = 1001;
        world.Me.X = 111;
        world.Me.Y = 222;
        world.Me.Z = 333;
        world.Me.AnchorSet = true;
        world.Me.AnchorX = 123;
        world.Me.AnchorY = 456;
        world.Me.AnchorZ = 789;

        engine.Start();

        Assert.True(world.Me.AnchorSet);
        Assert.Equal(111, world.Me.AnchorX);
        Assert.Equal(222, world.Me.AnchorY);
        Assert.Equal(333, world.Me.AnchorZ);

        world.Me.AnchorSet = true;
        world.Me.AnchorX = 321;
        world.Me.AnchorY = 654;
        world.Me.AnchorZ = 987;

        engine.Stop();

        Assert.False(world.Me.AnchorSet);
        Assert.Equal(0, world.Me.AnchorX);
        Assert.Equal(0, world.Me.AnchorY);
        Assert.Equal(0, world.Me.AnchorZ);
    }

    [Fact]
    public async Task Tick_ReanchorsToFirstFreshSelfMove_AfterStart()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld { IsConnected = true };
        world.Me.ObjectId = 1001;
        world.Me.X = 100;
        world.Me.Y = 200;
        world.Me.Z = 300;

        var profileManager = new ProfileManager(Path.GetTempPath());
        var engine = new BotEngine(world, profileManager);
        engine.SetSender(harness.Sender);
        engine.Start();

        Assert.Equal(100, world.Me.AnchorX);
        Assert.Equal(200, world.Me.AnchorY);
        Assert.Equal(300, world.Me.AnchorZ);

        await Task.Delay(20);
        world.Me.X = 400;
        world.Me.Y = 500;
        world.Me.Z = 600;
        world.LastSelfMoveEvidenceUtc = DateTime.UtcNow;

        await engine.TickAsync(CancellationToken.None);

        Assert.Equal(400, world.Me.AnchorX);
        Assert.Equal(500, world.Me.AnchorY);
        Assert.Equal(600, world.Me.AnchorZ);
    }

    private sealed class PacketSenderHarness : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly TcpClient _client;
        private readonly TcpClient _server;

        public PacketSender Sender { get; }

        private PacketSenderHarness(TcpListener listener, TcpClient client, TcpClient server, PacketSender sender)
        {
            _listener = listener;
            _client = client;
            _server = server;
            Sender = sender;
        }

        public static async Task<PacketSenderHarness> CreateAsync()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            var client = new TcpClient();
            var connectTask = client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
            var server = await listener.AcceptTcpClientAsync();
            await connectTask;

            var crypt = new L2GameCrypt(new byte[16]);
            var sender = new PacketSender(server.GetStream(), crypt, new SemaphoreSlim(1, 1));
            return new PacketSenderHarness(listener, client, server, sender);
        }

        public ValueTask DisposeAsync()
        {
            _server.Dispose();
            _client.Dispose();
            _listener.Stop();
            return ValueTask.CompletedTask;
        }
    }
}
