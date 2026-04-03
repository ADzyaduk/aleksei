using Alexei.Core.GameState;
using Alexei.Core.Protocol.Handlers;
using Xunit;

namespace Alexei.Core.Tests.Protocol.Handlers;

public sealed class BartzPacketHandlersTests
{
    [Fact]
    public void TargetSelectedHandler_ClearsTarget_WhenBartzStateSignalsLoss()
    {
        var world = new GameWorld();
        world.Me.TargetId = 12345;
        var handler = new TargetSelectedHandler();

        handler.Handle(
            new byte[]
            {
                0x39, 0x30, 0x00, 0x00,
                0x02, 0x00, 0x00, 0x00
            },
            world);

        Assert.Equal(0, world.Me.TargetId);
        Assert.Equal(0, world.Me.PendingTargetId);
    }

    [Fact]
    public void TargetSelectedHandler_UsesFirstDword_ForBartzEightBytePayload()
    {
        var world = new GameWorld();
        world.Me.PendingTargetId = 12345;
        var handler = new TargetSelectedHandler();

        handler.Handle(
            new byte[]
            {
                0x39, 0x30, 0x00, 0x00,
                0x01, 0x00, 0x00, 0x00
            },
            world);

        Assert.Equal(12345, world.Me.TargetId);
        Assert.Equal(0, world.Me.PendingTargetId);
    }

    [Fact]
    public void TargetSelectedHandler_IgnoresUnrelatedBartzTargetPackets()
    {
        var world = new GameWorld();
        world.Me.TargetId = 12345;
        world.Me.PendingTargetId = 12345;
        var handler = new TargetSelectedHandler();

        handler.Handle(
            new byte[]
            {
                0x31, 0xD4, 0x00, 0x00,
                0x01, 0x00, 0x00, 0x00
            },
            world);

        Assert.Equal(12345, world.Me.TargetId);
        Assert.Equal(12345, world.Me.PendingTargetId);
    }

    [Fact]
    public void TargetSelectedHandler_DoesNotClearTrackedTarget_ForUnrelatedLossPacket()
    {
        var world = new GameWorld();
        world.Me.TargetId = 12345;
        world.Me.PendingTargetId = 12345;
        var handler = new TargetSelectedHandler();

        handler.Handle(
            new byte[]
            {
                0x31, 0xD4, 0x00, 0x00,
                0x03, 0x00, 0x00, 0x00
            },
            world);

        Assert.Equal(12345, world.Me.TargetId);
        Assert.Equal(12345, world.Me.PendingTargetId);
    }

    [Fact]
    public void BartzTargetStatusHandler_DoesNotInventTarget_WhenNoPendingOrTrackedTargetExists()
    {
        var world = new GameWorld();
        var handler = new BartzTargetStatusHandler();

        handler.Handle(
            new byte[]
            {
                0x39, 0x30, 0x00, 0x00,
                0x07, 0x00, 0x00, 0x00,
                0x00, 0x00
            },
            world);

        Assert.Equal(0, world.Me.TargetId);
        Assert.Equal(0, world.Me.PendingTargetId);
    }

    [Fact]
    public void StatusUpdateHandler_UpdatesNpcVitals_FromBartzTargetStatusPayload()
    {
        var world = new GameWorld();
        var npc = new Npc { ObjectId = 1000 };
        world.Npcs[npc.ObjectId] = npc;
        var handler = new StatusUpdateHandler();

        handler.Handle(
            new byte[]
            {
                0xE8, 0x03, 0x00, 0x00,
                0x04, 0x00, 0x00, 0x00,
                0x09, 0x00, 0x00, 0x00,
                0x91, 0x03, 0x00, 0x00,
                0x0A, 0x00, 0x00, 0x00,
                0x02, 0x04, 0x00, 0x00,
                0x21, 0x00, 0x00, 0x00,
                0x6C, 0x01, 0x00, 0x00,
                0x22, 0x00, 0x00, 0x00,
                0x6C, 0x01, 0x00, 0x00
            },
            world);

        Assert.Equal(913, npc.CurHp);
        Assert.Equal(1026, npc.MaxHp);
        Assert.Equal(364, npc.CurCp);
        Assert.Equal(364, npc.MaxCp);
        Assert.Equal(89, npc.HpPercent);
        Assert.False(npc.IsDead);
    }
}

