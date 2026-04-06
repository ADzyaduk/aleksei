using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Alexei.Core.Config;
using Alexei.Core.Crypto;
using Alexei.Core.Engine.Tasks;
using Alexei.Core.GameState;
using Alexei.Core.Protocol;
using Alexei.Core.Proxy;
using Xunit;

namespace Alexei.Core.Tests.Engine.Tasks;

public sealed class PartyBuffTaskTests
{
    [Fact]
    public async Task PartyBuff_CastsConfiguredBuff_OnPartyMember()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Party.Enabled = true;
        profile.Party.BuffRules.Add(new BuffEntry
        {
            SkillId = 120,
            Level = 1,
            IntervalSec = 1200,
            RebuffOnMissing = true,
            Target = "party",
            Enabled = true
        });

        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Party[100] = new PartyMember
        {
            ObjectId = 100,
            Name = "Ally",
            X = 150,
            Y = 0,
            Z = 0,
            LastUpdateUtc = DateTime.UtcNow
        };
        world.Skills[120] = new SkillInfo { SkillId = 120, Level = 1 };

        var task = new PartyBuffTask();

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.TargetEnter);
        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.RequestMagicSkillUse);
    }

    [Fact]
    public async Task PartyBuff_DoesNotRecastBeforeIntervalExpires()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Party.Enabled = true;
        profile.Party.BuffRules.Add(new BuffEntry
        {
            SkillId = 120,
            Level = 1,
            IntervalSec = 1200,
            RebuffOnMissing = true,
            Target = "party",
            Enabled = true
        });

        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Party[100] = new PartyMember
        {
            ObjectId = 100,
            Name = "Ally",
            X = 150,
            Y = 0,
            Z = 0,
            LastUpdateUtc = DateTime.UtcNow
        };
        world.Skills[120] = new SkillInfo { SkillId = 120, Level = 1 };

        var task = new PartyBuffTask();

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        harness.SentPackets.Clear();

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Empty(harness.SentPackets);
    }

    [Fact]
    public async Task PartyBuff_DoesNotUseMissingEffectRecast_WithoutPartyBuffEvidence()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Party.Enabled = true;
        profile.Party.BuffRules.Add(new BuffEntry
        {
            SkillId = 120,
            Level = 1,
            IntervalSec = 1200,
            RebuffOnMissing = true,
            Target = "party",
            Enabled = true
        });

        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Party[100] = new PartyMember
        {
            ObjectId = 100,
            Name = "Ally",
            X = 150,
            Y = 0,
            Z = 0,
            LastUpdateUtc = DateTime.UtcNow
        };
        world.Skills[120] = new SkillInfo { SkillId = 120, Level = 1 };

        var task = new PartyBuffTask();

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        harness.SentPackets.Clear();

        var field = typeof(PartyBuffTask).GetField("_lastCast", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var state = (Dictionary<(int SkillId, int TargetId), DateTime>)field.GetValue(task)!;
        state[(120, 100)] = DateTime.UtcNow.AddSeconds(-10);

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Empty(harness.SentPackets);
    }

    private static CharacterProfile CreateBartzProfile() =>
        new()
        {
            Combat =
            {
                Enabled = true,
                UseTargetEnter = true,
                CombatSkillPacket = "39dcb"
            },
            Party =
            {
                Enabled = false
            }
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
