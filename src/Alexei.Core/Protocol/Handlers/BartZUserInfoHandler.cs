using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

/// <summary>
/// UserInfo for Bartz: opcode 0x11 (wire 0x0B ^ key 0x1A).
/// Payload starts with Name string, not X/Y/Z like Teon.
/// HP/MP are stored as double (not int32).
/// </summary>
public sealed class BartZUserInfoHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.UserInfo;

    public void Handle(byte[] payload, GameWorld world)
    {
        var r = new PacketReader(payload);
        var me = world.Me;

        me.Name = r.ReadString();
        me.ObjectId = r.ReadInt32();
        r.ReadString();
        r.Skip(16);

        me.Race = r.ReadInt32();
        me.ClassId = r.ReadInt32();
        me.Sex = r.ReadInt32();

        me.X = r.ReadInt32();
        me.Y = r.ReadInt32();
        me.Z = r.ReadInt32();

        me.MaxHp = (int)r.ReadDouble();
        me.CurHp = me.MaxHp;
        me.MaxMp = (int)r.ReadDouble();
        me.CurMp = me.MaxMp;

        me.Heading = r.ReadInt32();
        me.Exp = r.ReadInt64();
        me.Level = r.ReadInt32();

        me.IsDead = false;
        world.PositionConfidence = PositionConfidence.Low;
        world.NotifyUpdated();
    }
}
