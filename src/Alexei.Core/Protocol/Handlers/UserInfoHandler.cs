using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

public sealed class UserInfoHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.UserInfo;

    public void Handle(byte[] payload, GameWorld world)
    {
        var r = new PacketReader(payload);
        var me = world.Me;

        me.X = r.ReadInt32();
        me.Y = r.ReadInt32();
        me.Z = r.ReadInt32();
        me.Heading = r.ReadInt32();
        me.ObjectId = r.ReadInt32();
        me.Name = r.ReadString();

        me.Race = r.ReadInt32();
        me.Sex = r.ReadInt32();
        me.ClassId = r.ReadInt32();
        me.Level = r.ReadInt32();
        me.Exp = r.ReadInt64();

        // STR, DEX, CON, INT, WIT, MEN
        r.Skip(6 * 4);

        me.MaxHp = r.ReadInt32();
        me.CurHp = r.ReadInt32();
        me.MaxMp = r.ReadInt32();
        me.CurMp = r.ReadInt32();
        me.SP = r.ReadInt32();

        // Clear dead flag on respawn (HP > 0 means alive)
        if (me.CurHp > 0)
            me.IsDead = false;

        // Skip remaining fields (load, equipment, etc.)
        // We'll parse more as needed

        world.NotifyUpdated();
    }
}
