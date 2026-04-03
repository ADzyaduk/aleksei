using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

public sealed class ChangeWaitTypeHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.ChangeWaitType;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (payload.Length < 8) return;

        var r = new PacketReader(payload);
        int objectId = r.ReadInt32();
        int type = r.ReadInt32(); // 0=stand, 1=sit, 2=fake_death

        if (objectId == world.Me.ObjectId)
        {
            world.Me.IsSitting = type == 1;
            world.NotifyUpdated();
        }
    }
}
