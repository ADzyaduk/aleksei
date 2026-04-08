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

public sealed class PartyModeTaskTests
{
    [Fact]
    public async Task FollowMode_MovesTowardLeader_AndDoesNotAttack()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Party.Enabled = true;
        profile.Party.Mode = PartyMode.Follow;
        profile.Party.LeaderName = "Leader";
        profile.Party.FollowDistance = 150;
        profile.Party.RepathDistance = 300;
        profile.Party.PositionTimeoutMs = 2_000;

        world.PositionConfidence = PositionConfidence.Low;
        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Party[100] = new PartyMember
        {
            ObjectId = 100,
            Name = "Leader",
            X = 600,
            Y = 0,
            Z = 0,
            LastUpdateUtc = DateTime.UtcNow,
            LastPositionUpdateUtc = DateTime.UtcNow
        };

        var task = new PartyModeTask();

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.MoveBackwardToLocation);
        Assert.DoesNotContain(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.TargetEnter);
        Assert.DoesNotContain(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.RequestActionAttack);
    }
    [Fact]
    public async Task FollowMode_Moves_WhenLeaderIsOutsideFollowDistance_ButInsideRepathDistance()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Party.Enabled = true;
        profile.Party.Mode = PartyMode.Follow;
        profile.Party.LeaderName = "Leader";
        profile.Party.FollowDistance = 150;
        profile.Party.RepathDistance = 300;
        profile.Party.PositionTimeoutMs = 2_000;

        world.PositionConfidence = PositionConfidence.Low;
        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Party[100] = new PartyMember
        {
            ObjectId = 100,
            Name = "Leader",
            X = 220,
            Y = 0,
            Z = 0,
            LastUpdateUtc = DateTime.UtcNow,
            LastPositionUpdateUtc = DateTime.UtcNow
        };

        var task = new PartyModeTask();

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.MoveBackwardToLocation);
    }

    [Fact]
    public async Task FollowMode_DoesNothing_WhenSelfPositionIsUnknown()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Party.Enabled = true;
        profile.Party.Mode = PartyMode.Follow;
        profile.Party.LeaderName = "Leader";

        world.Party[100] = new PartyMember
        {
            ObjectId = 100,
            Name = "Leader",
            X = 600,
            Y = 0,
            Z = 0,
            LastUpdateUtc = DateTime.UtcNow,
            LastPositionUpdateUtc = DateTime.UtcNow
        };

        var task = new PartyModeTask();

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Empty(harness.SentPackets);
    }

    [Fact]
    public async Task FollowMode_DoesNothing_WhenLeaderPositionIsStale()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Party.Enabled = true;
        profile.Party.Mode = PartyMode.Follow;
        profile.Party.LeaderName = "Leader";
        profile.Party.PositionTimeoutMs = 1_000;

        world.PositionConfidence = PositionConfidence.Low;
        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Party[100] = new PartyMember
        {
            ObjectId = 100,
            Name = "Leader",
            X = 600,
            Y = 0,
            Z = 0,
            LastUpdateUtc = DateTime.UtcNow.AddSeconds(-15),
            LastPositionUpdateUtc = DateTime.UtcNow.AddSeconds(-15)
        };

        var task = new PartyModeTask();

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Empty(harness.SentPackets);
    }
    [Fact]
    public async Task FollowMode_UsesRecentLastKnownPosition_WhenUpdateIsSlightlyStale()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Party.Enabled = true;
        profile.Party.Mode = PartyMode.Follow;
        profile.Party.LeaderName = "Leader";
        profile.Party.FollowDistance = 150;
        profile.Party.RepathDistance = 300;
        profile.Party.PositionTimeoutMs = 2_000;

        world.PositionConfidence = PositionConfidence.Low;
        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Party[100] = new PartyMember
        {
            ObjectId = 100,
            Name = "Leader",
            X = 260,
            Y = 0,
            Z = 0,
            LastUpdateUtc = DateTime.UtcNow.AddSeconds(-3),
            LastPositionUpdateUtc = DateTime.UtcNow.AddSeconds(-3)
        };

        var task = new PartyModeTask();

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.MoveBackwardToLocation);
    }

    [Fact]
    public async Task FollowMode_UsesPartyLeaderObjectId_WhenBartzDidNotProvideNames()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Party.Enabled = true;
        profile.Party.Mode = PartyMode.Follow;
        profile.Party.LeaderName = "Leader";
        profile.Party.FollowDistance = 150;
        profile.Party.RepathDistance = 300;
        profile.Party.PositionTimeoutMs = 2_000;

        world.PositionConfidence = PositionConfidence.Low;
        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.PartyLeaderObjectId = 100;
        world.Party[100] = new PartyMember
        {
            ObjectId = 100,
            Name = string.Empty,
            X = 600,
            Y = 0,
            Z = 0,
            LastUpdateUtc = DateTime.UtcNow,
            LastPositionUpdateUtc = DateTime.UtcNow
        };

        var task = new PartyModeTask();

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.MoveBackwardToLocation);
    }


    [Fact]
    public async Task FollowMode_UsesConfiguredLeaderSelection_WhenPresent()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Party.Enabled = true;
        profile.Party.Mode = PartyMode.Follow;
        profile.Party.LeaderName = "ChosenLeader";
        profile.Party.FollowDistance = 150;
        profile.Party.RepathDistance = 300;
        profile.Party.PositionTimeoutMs = 2_000;

        world.PositionConfidence = PositionConfidence.Low;
        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.PartyLeaderObjectId = 100;
        world.Party[100] = new PartyMember
        {
            ObjectId = 100,
            Name = "ActualLeader",
            X = 600,
            Y = 0,
            Z = 0,
            LastUpdateUtc = DateTime.UtcNow,
            LastPositionUpdateUtc = DateTime.UtcNow
        };
        world.Party[200] = new PartyMember
        {
            ObjectId = 200,
            Name = "ChosenLeader",
            X = 600,
            Y = 0,
            Z = 0,
            LastUpdateUtc = DateTime.UtcNow,
            LastPositionUpdateUtc = DateTime.UtcNow
        };

        var task = new PartyModeTask();

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.MoveBackwardToLocation);
        Assert.Equal(0, world.Me.TargetId);
    }

    [Fact]
    public async Task FollowMode_UsesSolePartyMember_WhenLeaderMetadataIsMissing()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Party.Enabled = true;
        profile.Party.Mode = PartyMode.Follow;
        profile.Party.LeaderName = "Changolist";
        profile.Party.FollowDistance = 150;
        profile.Party.RepathDistance = 300;
        profile.Party.PositionTimeoutMs = 2_000;

        world.PositionConfidence = PositionConfidence.Low;
        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Party[123] = new PartyMember
        {
            ObjectId = 123,
            Name = string.Empty,
            X = 600,
            Y = 0,
            Z = 0,
            LastUpdateUtc = DateTime.UtcNow,
            LastPositionUpdateUtc = DateTime.UtcNow
        };

        var task = new PartyModeTask();

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.MoveBackwardToLocation);
    }

    [Fact]
    public async Task AssistMode_AttacksAssistTarget_AndUsesLeaderNameAsFallback()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Party.Enabled = true;
        profile.Party.Mode = PartyMode.Assist;
        profile.Party.LeaderName = "Leader";
        profile.Party.AssistName = string.Empty;
        profile.Party.FollowDistance = 150;
        profile.Party.RepathDistance = 300;
        profile.Party.PositionTimeoutMs = 2_000;

        world.PositionConfidence = PositionConfidence.Low;
        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Party[100] = new PartyMember
        {
            ObjectId = 100,
            Name = "Leader",
            X = 120,
            Y = 0,
            Z = 0,
            TargetId = 500,
            LastUpdateUtc = DateTime.UtcNow,
            LastPositionUpdateUtc = DateTime.UtcNow
        };
        world.Npcs[500] = new Npc
        {
            ObjectId = 500,
            NpcTypeId = 1_000_500,
            X = 130,
            Y = 0,
            Z = 0,
            IsAttackable = true,
            CurHp = 100,
            MaxHp = 100
        };

        var task = new PartyModeTask();

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.TargetEnter);
        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.RequestActionAttack);
    }

    [Fact]
    public async Task AssistMode_DoesNotAttack_WhenAssistHasNoTarget()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Party.Enabled = true;
        profile.Party.Mode = PartyMode.Assist;
        profile.Party.LeaderName = "Leader";
        profile.Party.PositionTimeoutMs = 2_000;

        world.PositionConfidence = PositionConfidence.Low;
        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Party[100] = new PartyMember
        {
            ObjectId = 100,
            Name = "Leader",
            X = 120,
            Y = 0,
            Z = 0,
            LastUpdateUtc = DateTime.UtcNow,
            LastPositionUpdateUtc = DateTime.UtcNow
        };

        var task = new PartyModeTask();

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.DoesNotContain(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.RequestActionAttack);
    }

    [Fact]
    public async Task AssistMode_FallsBackToPartyLeaderObjectId_WhenNamesDoNotResolve()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Party.Enabled = true;
        profile.Party.Mode = PartyMode.Assist;
        profile.Party.LeaderName = "GhostLeader";
        profile.Party.AssistName = "GhostAssist";
        profile.Party.FollowDistance = 150;
        profile.Party.RepathDistance = 300;
        profile.Party.PositionTimeoutMs = 2_000;

        world.PositionConfidence = PositionConfidence.Low;
        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.PartyLeaderObjectId = 100;
        world.Party[100] = new PartyMember
        {
            ObjectId = 100,
            Name = "ActualLeader",
            X = 120,
            Y = 0,
            Z = 0,
            TargetId = 500,
            LastUpdateUtc = DateTime.UtcNow,
            LastPositionUpdateUtc = DateTime.UtcNow
        };
        world.Npcs[500] = new Npc
        {
            ObjectId = 500,
            NpcTypeId = 1_000_500,
            X = 130,
            Y = 0,
            Z = 0,
            IsAttackable = true,
            CurHp = 100,
            MaxHp = 100
        };

        var task = new PartyModeTask();

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.TargetEnter);
        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.RequestActionAttack);
    }

    [Fact]
    public async Task AutoCombat_DoesNotSelectOwnTarget_WhenPartyModeIsActive()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Party.Enabled = true;
        profile.Party.Mode = PartyMode.Follow;

        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Npcs[101] = CreateNpc(101, 120, 0, 0);

        var task = new AutoCombatTask();

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Equal(CombatPhase.Idle, world.CurrentCombatPhase);
        Assert.Empty(harness.SentPackets);
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
                RetainTargetMaxDist = 325
            },
            Party =
            {
                Enabled = false,
                Mode = PartyMode.None,
                FollowDistance = 150,
                RepathDistance = 300,
                PositionTimeoutMs = 2_000
            }
        };

    private static Npc CreateNpc(int objectId, int x, int y, int z) =>
        new()
        {
            ObjectId = objectId,
            NpcTypeId = 1_000_000 + objectId,
            X = x,
            Y = y,
            Z = z,
            IsAttackable = true,
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



