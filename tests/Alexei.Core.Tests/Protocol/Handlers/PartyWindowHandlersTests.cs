using Alexei.Core.GameState;
using Alexei.Core.Protocol.Handlers;
using Xunit;

namespace Alexei.Core.Tests.Protocol.Handlers;

public sealed class PartyWindowHandlersTests
{
    [Fact]
    public void PartySmallWindowAllHandler_ParsesCompactBartzPayload()
    {
        var world = new GameWorld();
        world.Me.ObjectId = 4076;
        world.Me.Name = "Self";

        var handler = new PartySmallWindowAllHandler();
        handler.Handle(BuildIntPayload(
            1_209_025_772,
            4_076,
            2,
            1,
            1_236_294_729), world);

        Assert.Equal(1_209_025_772, world.PartyLeaderObjectId);
        Assert.Single(world.Party);
        Assert.True(world.Party.ContainsKey(1_236_294_729));
    }

    [Fact]
    public void PartySmallWindowAllHandler_PreservesKnownMemberState_OnCompactRefresh()
    {
        var world = new GameWorld();
        world.Me.ObjectId = 4076;
        world.Party[1_236_294_729] = new PartyMember
        {
            ObjectId = 1_236_294_729,
            Name = "Leader",
            CurHp = 500,
            MaxHp = 1000,
            CurMp = 250,
            MaxMp = 400
        };

        var handler = new PartySmallWindowAllHandler();
        handler.Handle(BuildIntPayload(
            1_209_025_772,
            4_076,
            2,
            1,
            1_236_294_729), world);

        var member = world.Party[1_236_294_729];
        Assert.Equal("Leader", member.Name);
        Assert.Equal(500, member.CurHp);
        Assert.Equal(1000, member.MaxHp);
        Assert.Equal(250, member.CurMp);
        Assert.Equal(400, member.MaxMp);
    }

    [Fact]
    public void PartySmallWindowAllHandler_IgnoresUnknownBartzVariant_InsteadOfClearingParty()
    {
        var world = new GameWorld();
        world.Me.ObjectId = 776_994;
        world.Party[1_255_197_788] = new PartyMember
        {
            ObjectId = 1_255_197_788,
            Name = "Leader",
            X = 3407,
            Y = 174_746,
            Z = -3_264,
            LastPositionUpdateUtc = DateTime.UtcNow,
            LastUpdateUtc = DateTime.UtcNow
        };

        var handler = new PartySmallWindowAllHandler();
        handler.Handle(BuildIntPayload(
            1_255_197_788,
            54,
            6,
            1,
            1_231_066_374), world);

        Assert.Single(world.Party);
        Assert.True(world.Party.ContainsKey(1_255_197_788));
        Assert.Equal("Leader", world.Party[1_255_197_788].Name);
    }

    [Fact]
    public void PartySmallWindowUpdateHandler_CreatesPlaceholderMember_FromCompactBartzPositionTail()
    {
        var world = new GameWorld();

        var handler = new PartySmallWindowUpdateHandler();
        handler.Handle(BuildIntPayload(
            1_209_025_772,
            1_236_294_729,
            4_051,
            20,
            0,
            0,
            888,
            177_792,
            -3_632,
            0), world);

        Assert.True(world.Party.ContainsKey(1_209_025_772));
        var member = world.Party[1_209_025_772];
        Assert.Equal(888, member.X);
        Assert.Equal(177_792, member.Y);
        Assert.Equal(-3_632, member.Z);
    }

    [Fact]
    public void PartySmallWindowUpdateHandler_ParsesCompactBartzPositionTail()
    {
        var world = new GameWorld();
        world.Party[1_209_025_772] = new PartyMember { ObjectId = 1_209_025_772, Name = "Leader" };

        var handler = new PartySmallWindowUpdateHandler();
        handler.Handle(BuildIntPayload(
            1_209_025_772,
            1_236_294_729,
            4_051,
            20,
            0,
            0,
            888,
            177_792,
            -3_632,
            0), world);

        var member = world.Party[1_209_025_772];
        Assert.Equal(888, member.X);
        Assert.Equal(177_792, member.Y);
        Assert.Equal(-3_632, member.Z);
        Assert.True(member.LastUpdateUtc > DateTime.MinValue);
        Assert.True(member.LastPositionUpdateUtc > DateTime.MinValue);
    }

    [Fact]
    public void PartySmallWindowDeleteHandler_RemovesMember_WhenPayloadContainsExtraBartzFields()
    {
        var world = new GameWorld();
        world.Party[100] = new PartyMember { ObjectId = 100, Name = "Leader" };

        var handler = new PartySmallWindowDeleteHandler();
        handler.Handle(BuildIntPayload(100, 8, 0, 0), world);

        Assert.Empty(world.Party);
    }

    [Fact]
    public void MoveToPointHandler_UpdatesPartyMemberPosition()
    {
        var world = new GameWorld();
        world.Party[100] = new PartyMember { ObjectId = 100, Name = "Leader" };

        var handler = new MoveToPointHandler();
        handler.Handle(BuildMovePayload(100, 150, 250, 350, 10, 20, 30), world);

        var member = world.Party[100];
        Assert.Equal(150, member.X);
        Assert.Equal(250, member.Y);
        Assert.Equal(350, member.Z);
        Assert.True(member.LastPositionUpdateUtc > DateTime.MinValue);
    }

    [Fact]
    public void AttackHandler_TracksPartyMemberTargetId()
    {
        var world = new GameWorld();
        world.Party[100] = new PartyMember { ObjectId = 100, Name = "Leader" };

        var handler = new AttackHandler();
        handler.Handle(BuildIntPayload(100, 500, 999), world);

        Assert.Equal(500, world.Party[100].TargetId);
    }

    private static byte[] BuildIntPayload(params int[] values)
    {
        var payload = new byte[values.Length * 4];
        for (var i = 0; i < values.Length; i++)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(values[i]), 0, payload, i * 4, 4);
        }

        return payload;
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
}

