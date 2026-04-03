using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

/// <summary>
/// Secondary StatusUpdate (0x0E on Teon). Same format as primary StatusUpdate,
/// but with fallback for packets without a count field.
/// </summary>
public sealed class StatusUpdate2Handler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.StatusUpdate2;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (payload.Length < 8) return;

        var r = new PacketReader(payload);
        int objectId = r.ReadInt32();
        int countOrAttrId = r.ReadInt32();

        bool isSelf = objectId == world.Me.ObjectId;

        // Check if countOrAttrId is a valid count (Teon 0x0E may not have count field)
        if (countOrAttrId >= 0 && countOrAttrId <= 10 && r.Remaining >= countOrAttrId * 8)
        {
            // Standard format: objectId + count + count×(attrId, value)
            for (int i = 0; i < countOrAttrId && r.Remaining >= 8; i++)
            {
                int attrId = r.ReadInt32();
                int value = r.ReadInt32();
                ApplyAttr(attrId, value, objectId, isSelf, world);
            }
        }
        else
        {
            // Alternate format: objectId + attrId + value + ...
            if (r.Remaining >= 4)
            {
                int value = r.ReadInt32();
                ApplyAttr(countOrAttrId, value, objectId, isSelf, world);
            }
            while (r.Remaining >= 8)
            {
                int attrId = r.ReadInt32();
                int value = r.ReadInt32();
                ApplyAttr(attrId, value, objectId, isSelf, world);
            }
        }

        world.NotifyUpdated();
    }

    private static void ApplyAttr(int attrId, int value, int objectId, bool isSelf, GameWorld world)
    {
        if (isSelf)
        {
            switch (attrId)
            {
                case Opcodes.Attr.CurHp: world.Me.CurHp = value; break;
                case Opcodes.Attr.MaxHp: world.Me.MaxHp = value; break;
                case Opcodes.Attr.CurMp: world.Me.CurMp = value; break;
                case Opcodes.Attr.MaxMp: world.Me.MaxMp = value; break;
                case Opcodes.Attr.CurCp: world.Me.CurCp = value; break;
                case Opcodes.Attr.MaxCp: world.Me.MaxCp = value; break;
                case Opcodes.Attr.Level: world.Me.Level = value; break;
                case Opcodes.Attr.CurExp: world.Me.Exp = value; break;
                case Opcodes.Attr.SP: world.Me.SP = value; break;
            }
        }
        else if (world.Party.TryGetValue(objectId, out var member))
        {
            switch (attrId)
            {
                case Opcodes.Attr.CurHp: member.CurHp = value; break;
                case Opcodes.Attr.MaxHp: member.MaxHp = value; break;
                case Opcodes.Attr.CurMp: member.CurMp = value; break;
                case Opcodes.Attr.MaxMp: member.MaxMp = value; break;
            }
        }
    }
}
