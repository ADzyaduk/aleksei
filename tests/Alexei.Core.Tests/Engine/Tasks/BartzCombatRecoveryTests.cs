using System.Net;
using System.Net.Sockets;
using Alexei.Core.Config;
using Alexei.Core.Crypto;
using Alexei.Core.Engine.Tasks;
using Alexei.Core.GameState;
using Alexei.Core.Protocol;
using Alexei.Core.Proxy;
using Xunit;

namespace Alexei.Core.Tests.Engine.Tasks;

public sealed class BartzCombatRecoveryTests
{
    [Fact]
    public async Task AutoCombat_BartzEngage_UsesTargetEnter_AndNotAttackUse59()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        var task = new AutoCombatTask();

        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Npcs[101] = CreateNpc(101, x: 120, y: 0, z: 0);

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        world.Me.TargetId = 101;
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        harness.SentPackets.Clear();

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.TargetEnter);
        Assert.DoesNotContain(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.RequestAttackUse59);
    }

    [Fact]
    public async Task AutoCombat_Idle_DoesNotAdoptForeignCurrentTarget_AndReselectsCleanly()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        var task = new AutoCombatTask();

        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Me.TargetId = 202;
        world.Npcs[101] = CreateNpc(101, x: 120, y: 0, z: 0);
        world.Npcs[202] = CreateNpc(202, x: 220, y: 0, z: 0);

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Equal(CombatPhase.SelectingTarget, world.CurrentCombatPhase);
        Assert.Equal(101, world.LastEngagedTargetId);
        Assert.Equal(101, world.Me.PendingTargetId);
        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.TargetEnter);
    }

    private static CharacterProfile CreateBartzProfile() =>
        new()
        {
            Combat =
            {
                Enabled = true,
                UseTargetEnter = true,
                CombatSkillPacket = "39dcb",
                AggroRadius = 250,
                AnchorLeash = 600,
                ZHeightLimit = 200,
                RetainTargetMaxDist = 325,
                ReattackIntervalMs = 50,
                PostKillSpawnWaitMs = 100,
                PostKillLootWindowMs = 1000,
                SkillRotation = new List<SkillRotationEntry>()
            },
            Loot =
            {
                Enabled = true,
                Radius = 250
            }
        };

    private static Npc CreateNpc(int objectId, int x, int y, int z) =>
        new()
        {
            ObjectId = objectId,
            NpcTypeId = 1_000_000 + 20000 + objectId,
            X = x,
            Y = y,
            Z = z,
            IsAttackable = true,
            IsDead = false,
            CurHp = 100,
            MaxHp = 100
        };

    private sealed class PacketSenderHarness : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly TcpClient _client;
        private readonly TcpClient _server;

        public PacketSender Sender { get; }
        public List<(byte Opcode, int Length)> SentPackets { get; } = new();

        private PacketSenderHarness(TcpListener listener, TcpClient client, TcpClient server, PacketSender sender)
        {
            _listener = listener;
            _client = client;
            _server = server;
            Sender = sender;
            Sender.PacketSent += (opcode, length) => SentPackets.Add((opcode, length));
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
