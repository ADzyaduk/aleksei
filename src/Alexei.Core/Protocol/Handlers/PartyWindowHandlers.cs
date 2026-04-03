using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

public sealed class PartySmallWindowAllHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.PartySmallWindowAll;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (payload.Length < 6) return;

        var r = new PacketReader(payload);
        world.Party.Clear();

        // Skip leader objectId
        r.Skip(4);
        // Party loot type
        r.Skip(2); // short

        int count = r.Remaining >= 4 ? r.ReadInt32() : 0;

        for (int i = 0; i < count && r.Remaining >= 20; i++)
        {
            int objectId = r.ReadInt32();
            string name = r.ReadString();
            int curHp = r.ReadInt32();
            int maxHp = r.ReadInt32();
            int curMp = r.ReadInt32();
            int maxMp = r.ReadInt32();
            int level = r.ReadInt32();
            int classId = r.ReadInt32();
            r.Skip(4); // unk

            world.Party[objectId] = new PartyMember
            {
                ObjectId = objectId,
                Name = name,
                CurHp = curHp,
                MaxHp = maxHp,
                CurMp = curMp,
                MaxMp = maxMp,
                Level = level,
                ClassId = classId
            };
        }

        world.NotifyUpdated();
    }
}

public sealed class PartySmallWindowAddHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.PartySmallWindowAdd;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (payload.Length < 20) return;

        var r = new PacketReader(payload);
        int objectId = r.ReadInt32();
        string name = r.ReadString();
        int curHp = r.ReadInt32();
        int maxHp = r.ReadInt32();
        int curMp = r.ReadInt32();
        int maxMp = r.ReadInt32();
        int level = r.ReadInt32();
        int classId = r.ReadInt32();

        world.Party[objectId] = new PartyMember
        {
            ObjectId = objectId,
            Name = name,
            CurHp = curHp,
            MaxHp = maxHp,
            CurMp = curMp,
            MaxMp = maxMp,
            Level = level,
            ClassId = classId
        };

        world.NotifyUpdated();
    }
}

public sealed class PartySmallWindowDeleteHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.PartySmallWindowDelete;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (payload.Length < 4) return;

        var r = new PacketReader(payload);
        int objectId = r.ReadInt32();

        world.Party.TryRemove(objectId, out _);
        world.NotifyUpdated();
    }
}

public sealed class PartySmallWindowUpdateHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.PartySmallWindowUpdate;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (payload.Length < 16) return;

        var r = new PacketReader(payload);
        int objectId = r.ReadInt32();

        if (world.Party.TryGetValue(objectId, out var member))
        {
            if (r.Remaining >= 4) member.CurHp = r.ReadInt32();
            if (r.Remaining >= 4) member.MaxHp = r.ReadInt32();
            if (r.Remaining >= 4) member.CurMp = r.ReadInt32();
            if (r.Remaining >= 4) member.MaxMp = r.ReadInt32();
            world.NotifyUpdated();
        }
    }
}
