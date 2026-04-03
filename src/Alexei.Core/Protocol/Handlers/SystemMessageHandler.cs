using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

/// <summary>
/// SystemMessage (0x64 on Teon) — system notifications including spoil success.
/// SYSMSG_SPOIL_SUCCESS = 612, SYSMSG_ALREADY_SPOILED = 357
/// </summary>
public sealed class SystemMessageHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.SystemMessage;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (payload.Length < 4) return;

        var r = new PacketReader(payload);
        int msgId = r.ReadInt32();

        // 612 = spoil succeeded, 357 = already spoiled (also means we can sweep)
        if (msgId == 612 || msgId == 357)
        {
            int targetId = world.Me.TargetId;
            if (targetId != 0)
            {
                var spoil = world.SpoiledNpcs.GetOrAdd(targetId, _ => new SpoilStatus());
                spoil.Succeeded = true;
            }
        }
    }
}
