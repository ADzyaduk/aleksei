using System.Net;
using System.Net.Sockets;
using Alexei.Core.Config;
using Alexei.Core.Crypto;
using Alexei.Core.Engine.Tasks;
using Alexei.Core.GameState;
using Alexei.Core.Protocol;
using Alexei.Core.Protocol.Handlers;
using Alexei.Core.Proxy;
using Xunit;

namespace Alexei.Core.Tests.Engine.Tasks;

public sealed class CombatStateMachineTests
{
    [Fact]
    public async Task AutoCombat_SelectsNearestTarget_AndStartsSelectionPhase()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        var task = new AutoCombatTask();

        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Npcs[101] = CreateNpc(101, x: 120, y: 0, z: 0);
        world.Npcs[202] = CreateNpc(202, x: 220, y: 0, z: 0);

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Equal(CombatPhase.SelectingTarget, world.CurrentCombatPhase);
        Assert.Equal(101, world.LastEngagedTargetId);
        Assert.Equal(101, world.Me.PendingTargetId);
        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.TargetEnter);
    }

    [Fact]
    public async Task AutoCombat_ConfirmsDeath_AndLootsDropperItem_DuringPostKillWindow()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Combat.PostKillSpawnWaitMs = 100;
        profile.Combat.PostKillLootWindowMs = 1000;

        var task = new AutoCombatTask();
        var npc = CreateNpc(333, x: 110, y: 0, z: 0);
        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Npcs[npc.ObjectId] = npc;

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        world.Me.TargetId = npc.ObjectId;
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Equal(CombatPhase.KillLoop, world.CurrentCombatPhase);

        npc.IsDead = true;
        npc.LastDeathEvidenceUtc = DateTime.UtcNow;

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        Assert.Equal(CombatPhase.PostKill, world.CurrentCombatPhase);

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        Assert.Equal(0, world.Me.TargetId);

        world.Items[9001] = new GroundItem
        {
            ObjectId = 9001,
            ItemId = 57,
            X = -1711,
            Y = 102295,
            Z = -3760,
            Count = 1,
            DropperObjectId = npc.ObjectId,
            SpawnedAtUtc = DateTime.UtcNow
        };

        await Task.Delay(150);
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        Assert.Equal(CombatPhase.Looting, world.CurrentCombatPhase);

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.RequestGetItem);
        Assert.Equal(1, world.Items[9001].PickupAttempts);
    }

    [Fact]
    public async Task AutoLoot_SkipsOutsideIdlePhase_AndDoesNotConsumeItemOptimistically()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        var task = new AutoLootTask();

        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Items[7001] = new GroundItem
        {
            ObjectId = 7001,
            ItemId = 57,
            X = 80,
            Y = 0,
            Z = 0,
            Count = 1,
            SpawnedAtUtc = DateTime.UtcNow
        };
        world.SetCombatPhase(CombatPhase.KillLoop);

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.DoesNotContain(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.RequestGetItem);
        Assert.True(world.Items.ContainsKey(7001));
        Assert.Equal(0, world.Items[7001].PickupAttempts);
    }
    [Fact]
    public async Task AutoCombat_StopsRetryingStaleLoot_AndReturnsToIdle()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Combat.PostKillSpawnWaitMs = 100;
        profile.Combat.PostKillLootWindowMs = 1200;

        var task = new AutoCombatTask();
        var npc = CreateNpc(444, x: 110, y: 0, z: 0);
        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Npcs[npc.ObjectId] = npc;

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        world.Me.TargetId = npc.ObjectId;
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        npc.IsDead = true;
        npc.LastDeathEvidenceUtc = DateTime.UtcNow;

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        world.Items[9101] = new GroundItem
        {
            ObjectId = 9101,
            ItemId = 57,
            X = -1711,
            Y = 102295,
            Z = -3760,
            Count = 1,
            DropperObjectId = npc.ObjectId,
            SpawnedAtUtc = DateTime.UtcNow
        };

        await Task.Delay(150);
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        Assert.Equal(CombatPhase.Looting, world.CurrentCombatPhase);

        for (var i = 0; i < 3; i++)
        {
            await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
            await Task.Delay(350);
        }

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Equal(CombatPhase.Idle, world.CurrentCombatPhase);
        Assert.Equal(3, world.Items[9101].PickupAttempts);
    }

    [Fact]
    public async Task AutoLoot_SkipsItemAfterRepeatedPickupAttempts()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        var task = new AutoLootTask();

        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Items[7002] = new GroundItem
        {
            ObjectId = 7002,
            ItemId = 57,
            X = 80,
            Y = 0,
            Z = 0,
            Count = 1,
            PickupAttempts = 3,
            SpawnedAtUtc = DateTime.UtcNow
        };

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.DoesNotContain(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.RequestGetItem);
        Assert.Equal(3, world.Items[7002].PickupAttempts);
    }
    [Fact]
    public async Task AutoCombat_DoesNotSendManualMove_WhenFightStalls()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        var task = new AutoCombatTask();
        var npc = CreateNpc(555, x: 340, y: 0, z: 0);
        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Npcs[npc.ObjectId] = npc;

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        world.Me.TargetId = npc.ObjectId;
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        harness.SentPackets.Clear();
        var phaseField = typeof(AutoCombatTask).GetField("_phaseSince", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        phaseField.SetValue(task, DateTime.UtcNow.AddSeconds(-7));
        world.LastCombatProgressUtc = null;
        world.LastSelfMoveEvidenceUtc = DateTime.UtcNow.AddSeconds(-3);

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.DoesNotContain(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.MoveBackwardToLocation);
    }

    [Fact]
    public void MoveToPointHandler_UpdatesSelfPositionFromDestination_AndRecordsMovementEvidence()
    {
        var world = new GameWorld();
        world.Me.ObjectId = 501;
        world.Me.X = 10;
        world.Me.Y = 20;
        world.Me.Z = 30;

        var handler = new MoveToPointHandler();
        handler.Handle(BuildMovePayload(501, destX: 150, destY: 250, destZ: 350, origX: 10, origY: 20, origZ: 30), world);

        Assert.Equal(150, world.Me.X);
        Assert.Equal(250, world.Me.Y);
        Assert.Equal(350, world.Me.Z);
        Assert.NotNull(world.LastSelfMoveEvidenceUtc);
        Assert.Equal(PositionConfidence.Confirmed, world.PositionConfidence);
    }

    private static byte[] BuildMovePayload(int objectId, int destX, int destY, int destZ, int origX, int origY, int origZ)
    {
        var payload = new byte[28];
        Buffer.BlockCopy(BitConverter.GetBytes(objectId), 0, payload, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(destX), 0, payload, 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(destY), 0, payload, 8, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(destZ), 0, payload, 12, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(origX), 0, payload, 16, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(origY), 0, payload, 20, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(origZ), 0, payload, 24, 4);
        return payload;
    }

    [Fact]
    public async Task AutoCombat_PrefersNearestTarget_WhenMultipleCandidatesAvailable()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        var task = new AutoCombatTask();

        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;

        var nearest = CreateNpc(701, x: 120, y: 0, z: 0);
        var aggro = CreateNpc(702, x: 220, y: 0, z: 0);
        world.Npcs[nearest.ObjectId] = nearest;
        world.Npcs[aggro.ObjectId] = aggro;

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Equal(701, world.LastEngagedTargetId);
        Assert.Equal(701, world.Me.PendingTargetId);
    }

    [Fact]
    public async Task AutoCombat_PrefersLocalCluster_BeforeFarTargetsInsideAggroRadius()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Combat.AggroRadius = 2000;
        var task = new AutoCombatTask();

        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;

        world.Npcs[801] = CreateNpc(801, x: 180, y: 0, z: 0);
        world.Npcs[802] = CreateNpc(802, x: 320, y: 0, z: 0);
        world.Npcs[803] = CreateNpc(803, x: 1600, y: 0, z: 0);

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Equal(801, world.LastEngagedTargetId);
        Assert.Equal(801, world.Me.PendingTargetId);
    }    private static CharacterProfile CreateBartzProfile() =>
        new()
        {
            Combat =
            {
                Enabled = true,
                UseTargetEnter = true,
                CombatSkillPacket = "39dcb",
                PreferAggroTargets = false,
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
            },
            Spoil =
            {
                Enabled = false,
                SweepEnabled = false
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













