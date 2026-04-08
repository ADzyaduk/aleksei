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
    }

    [Fact]
    public async Task AutoCombat_PrefersNearestCandidateWithinDynamicLocalCluster()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Combat.AggroRadius = 2000;
        profile.Combat.AnchorLeash = 2000;
        var task = new AutoCombatTask();

        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;

        world.Npcs[804] = CreateNpc(804, x: 780, y: 0, z: 0);
        world.Npcs[805] = CreateNpc(805, x: 980, y: 0, z: 0);
        world.Npcs[806] = CreateNpc(806, x: 1550, y: 0, z: 0);

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Equal(804, world.LastEngagedTargetId);
        Assert.Equal(804, world.Me.PendingTargetId);
    }

    [Fact]
    public async Task AutoCombat_RelaxesZFilterForBartz_WhenStrictFilterRejectsEverything()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Combat.AggroRadius = 2000;
        profile.Combat.ZHeightLimit = 200;
        profile.Combat.UseTargetEnter = true;
        var task = new AutoCombatTask();

        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;

        world.Npcs[811] = CreateNpc(811, x: 180, y: 0, z: 500);
        world.Npcs[812] = CreateNpc(812, x: 260, y: 0, z: 650);

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Equal(811, world.LastEngagedTargetId);
        Assert.Equal(811, world.Me.PendingTargetId);
    }

    [Fact]
    public async Task AutoCombat_PrefersRecentAggroTarget_OverNearestNeutral()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Combat.PreferAggroTargets = true;
        profile.Combat.AggroRadius = 1000;
        var task = new AutoCombatTask();

        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;

        world.Npcs[831] = CreateNpc(831, x: 120, y: 0, z: 0);
        var aggro = CreateNpc(832, x: 220, y: 0, z: 0);
        aggro.LastAttackOnMeUtc = DateTime.UtcNow;
        world.Npcs[832] = aggro;

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Equal(832, world.LastEngagedTargetId);
        Assert.Equal(832, world.Me.PendingTargetId);
    }

    [Fact]
    public async Task AutoCombat_DoesNotPreferAggroTarget_WhenPreferenceDisabled()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Combat.PreferAggroTargets = false;
        profile.Combat.AggroRadius = 1000;
        var task = new AutoCombatTask();

        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;

        world.Npcs[841] = CreateNpc(841, x: 120, y: 0, z: 0);
        var aggro = CreateNpc(842, x: 320, y: 0, z: 0);
        aggro.LastAttackOnMeUtc = DateTime.UtcNow;
        world.Npcs[842] = aggro;

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Equal(841, world.LastEngagedTargetId);
        Assert.Equal(841, world.Me.PendingTargetId);
    }

    [Fact]
    public async Task AutoCombat_RestartsFromRecentCorpseArea_WhenSelfPositionIsStaleAfterLoot()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Combat.AggroRadius = 1500;
        profile.Combat.AnchorLeash = 4000;
        profile.Combat.PostKillSpawnWaitMs = 0;
        profile.Combat.PostKillLootWindowMs = 250;

        var task = new AutoCombatTask();
        var killedNpc = CreateNpc(951, x: 1200, y: 0, z: 0);
        var nextNpc = CreateNpc(952, x: 1450, y: 0, z: 0);

        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Npcs[killedNpc.ObjectId] = killedNpc;

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        world.Me.TargetId = killedNpc.ObjectId;
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        killedNpc.IsDead = true;
        killedNpc.LastDeathEvidenceUtc = DateTime.UtcNow;

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        await Task.Delay(20);
        for (var i = 0; i < 6 && world.CurrentCombatPhase != CombatPhase.Idle; i++)
        {
            await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
            await Task.Delay(100);
        }

        world.Npcs[nextNpc.ObjectId] = nextNpc;

        for (var i = 0; i < 6 && world.CurrentCombatPhase != CombatPhase.Idle; i++)
        {
            await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
            await Task.Delay(100);
        }

        Assert.Equal(CombatPhase.Idle, world.CurrentCombatPhase);

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Equal(CombatPhase.SelectingTarget, world.CurrentCombatPhase);
        Assert.Equal(nextNpc.ObjectId, world.LastEngagedTargetId);
        Assert.Equal(nextNpc.ObjectId, world.Me.PendingTargetId);
    }

    [Fact]
    public async Task AutoCombat_UsesOpeningSkill_WhenConditionsMatch_AndRespectsRuleCooldown()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Combat.PostSkillDelayMs = 0;
        profile.Combat.ReattackIntervalMs = 0;
        profile.Combat.SkillRotation.Add(new SkillRotationEntry
        {
            SkillId = 29,
            Level = 1,
            Enabled = true,
            MinMpPct = 10,
            CooldownMs = 5000,
            TargetHpBelowPct = 60,
            MaxRange = 200
        });

        var task = new AutoCombatTask();
        var npc = CreateNpc(901, x: 120, y: 0, z: 0);
        npc.CurHp = 50;
        npc.MaxHp = 100;
        npc.HpPercent = 50;

        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Me.CurMp = 100;
        world.Me.MaxMp = 100;
        world.Npcs[npc.ObjectId] = npc;
        world.Skills[29] = new SkillInfo { SkillId = 29, Level = 1 };

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        world.Me.TargetId = npc.ObjectId;
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        harness.SentPackets.Clear();
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.RequestMagicSkillUse);

        harness.SentPackets.Clear();
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        await Task.Delay(300);
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.DoesNotContain(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.RequestMagicSkillUse);
        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.TargetEnter);
    }

    [Fact]
    public async Task AutoCombat_SkipsSkill_WhenServerCooldownActive_AndStillAttacks()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Combat.SkillRotation.Add(new SkillRotationEntry
        {
            SkillId = 29,
            Level = 1,
            Enabled = true,
            MinMpPct = 10,
            CooldownMs = 0
        });

        var task = new AutoCombatTask();
        var npc = CreateNpc(902, x: 120, y: 0, z: 0);
        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Me.CurMp = 100;
        world.Me.MaxMp = 100;
        world.Npcs[npc.ObjectId] = npc;
        var skill = new SkillInfo { SkillId = 29, Level = 1 };
        skill.SetCooldown(5000);
        world.Skills[29] = skill;

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        world.Me.TargetId = npc.ObjectId;
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        harness.SentPackets.Clear();
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.DoesNotContain(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.RequestMagicSkillUse);
        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.TargetEnter);
    }

    [Fact]
    public async Task AutoCombat_SkipsSkill_WhenMpTargetHpOrRangeConditionsFail_AndFallsBackToAttack()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Combat.SkillRotation.Add(new SkillRotationEntry
        {
            SkillId = 29,
            Level = 1,
            Enabled = true,
            MinMpPct = 80,
            CooldownMs = 0,
            TargetHpBelowPct = 40,
            MaxRange = 50
        });

        var task = new AutoCombatTask();
        var npc = CreateNpc(903, x: 120, y: 0, z: 0);
        npc.CurHp = 60;
        npc.MaxHp = 100;
        npc.HpPercent = 60;

        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Me.CurMp = 50;
        world.Me.MaxMp = 100;
        world.Npcs[npc.ObjectId] = npc;
        world.Skills[29] = new SkillInfo { SkillId = 29, Level = 1 };

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        world.Me.TargetId = npc.ObjectId;
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        harness.SentPackets.Clear();
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.DoesNotContain(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.RequestMagicSkillUse);
        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.TargetEnter);
    }

    [Fact]
    public async Task AutoBuff_DoesNotSpamImmediately_WhenEffectTrackingIsMissing_ButStillRespectsServerCooldown()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Buffs.Enabled = true;
        profile.Buffs.List.Add(new BuffEntry
        {
            SkillId = 120,
            Level = 1,
            Enabled = true,
            IntervalSec = 1200,
            RebuffOnMissing = true,
            Target = "self"
        });

        world.Skills[120] = new SkillInfo { SkillId = 120, Level = 1 };
        var task = new AutoBuffTask();

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.RequestTargetCancel);
        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.RequestMagicSkillUse);

        harness.SentPackets.Clear();
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        Assert.Empty(harness.SentPackets);

        harness.SentPackets.Clear();
        world.Skills[120].SetCooldown(5000);
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        Assert.Empty(harness.SentPackets);

        harness.SentPackets.Clear();
        world.Skills[120].CooldownUntil = DateTime.MinValue;
        var lastCastField = typeof(AutoBuffTask).GetField("_lastCast", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var lastCast = (Dictionary<int, DateTime>)lastCastField.GetValue(task)!;
        lastCast[120] = DateTime.UtcNow.AddSeconds(-16);
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.RequestMagicSkillUse);
    }

    [Fact]
    public async Task AutoHeal_UsesThresholdAndPerSkillCooldown()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Party.Enabled = true;
        profile.Party.HealRules.Add(new HealRule
        {
            SkillId = 121,
            Level = 1,
            HpThreshold = 70,
            MpMinPct = 20,
            CooldownMs = 5000,
            Enabled = true
        });

        world.Me.CurHp = 40;
        world.Me.MaxHp = 100;
        world.Me.CurMp = 50;
        world.Me.MaxMp = 100;
        world.Skills[121] = new SkillInfo { SkillId = 121, Level = 1 };

        var task = new AutoHealTask();

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.RequestMagicSkillUse);

        harness.SentPackets.Clear();
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        Assert.DoesNotContain(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.RequestMagicSkillUse);
    }

    [Fact]
    public async Task PartyHeal_UsesEligibleRule_AndRespectsCooldown()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Party.Enabled = true;
        profile.Party.HealRules.Add(new HealRule
        {
            SkillId = 122,
            Level = 1,
            HpThreshold = 70,
            MpMinPct = 20,
            CooldownMs = 5000,
            Enabled = true
        });

        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Me.CurMp = 50;
        world.Me.MaxMp = 100;
        world.Skills[122] = new SkillInfo { SkillId = 122, Level = 1 };
        world.Party[1] = new PartyMember { ObjectId = 1, Name = "A", CurHp = 40, MaxHp = 100 };
        world.Party[2] = new PartyMember { ObjectId = 2, Name = "B", CurHp = 90, MaxHp = 100 };

        var task = new PartyHealTask();

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.TargetEnter);
        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.RequestMagicSkillUse);

        harness.SentPackets.Clear();
        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);
        Assert.Empty(harness.SentPackets);
    }
    [Fact]
    public async Task PartyHeal_CastsRecharge_WhenPartyMemberMpIsBelowThreshold()
    {
        await using var harness = await PacketSenderHarness.CreateAsync();
        var world = new GameWorld();
        var profile = CreateBartzProfile();
        profile.Party.Enabled = true;
        profile.Party.HealRules.Add(new HealRule
        {
            SkillId = 1013,
            Level = 1,
            HpThreshold = 0,
            MpThreshold = 60,
            MpMinPct = 20,
            CooldownMs = 5000,
            Enabled = true
        });

        world.Me.X = 0;
        world.Me.Y = 0;
        world.Me.Z = 0;
        world.Me.CurMp = 80;
        world.Me.MaxMp = 100;
        world.Skills[1013] = new SkillInfo { SkillId = 1013, Level = 1 };
        world.Party[1] = new PartyMember { ObjectId = 1, Name = "LowMp", CurHp = 100, MaxHp = 100, CurMp = 20, MaxMp = 100 };
        world.Party[2] = new PartyMember { ObjectId = 2, Name = "FullMp", CurHp = 100, MaxHp = 100, CurMp = 90, MaxMp = 100 };

        var task = new PartyHealTask();

        await task.ExecuteAsync(world, harness.Sender, profile, CancellationToken.None);

        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.TargetEnter);
        Assert.Contains(harness.SentPackets, packet => packet.Opcode == Opcodes.GameC2S.RequestMagicSkillUse);
    }

    private static CharacterProfile CreateBartzProfile() =>
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

