using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

public sealed class PartySmallWindowAllHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.PartySmallWindowAll;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (TryHandleCompactBartz(payload, world))
            return;

        if (payload.Length == 20)
            return;

        if (payload.Length < 6) return;

        var r = new PacketReader(payload);
        world.Party.Clear();

        world.PartyLeaderObjectId = r.ReadInt32();
        r.Skip(2);

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
            r.Skip(4);

            world.Party[objectId] = new PartyMember
            {
                ObjectId = objectId,
                Name = name,
                CurHp = curHp,
                MaxHp = maxHp,
                CurMp = curMp,
                MaxMp = maxMp,
                Level = level,
                ClassId = classId,
                LastUpdateUtc = DateTime.UtcNow
            };
        }

        world.NotifyUpdated();
    }

    private static bool TryHandleCompactBartz(byte[] payload, GameWorld world)
    {
        if (payload.Length < 16 || payload.Length % 4 != 0)
            return false;

        var r = new PacketReader(payload);
        int leaderObjectId = r.ReadInt32();
        int selfObjectId = r.ReadInt32();
        int memberCount = r.ReadInt32();
        _ = r.ReadInt32();

        if (memberCount <= 0)
            return false;

        if (world.Me.ObjectId != 0 && selfObjectId != world.Me.ObjectId)
            return false;

        int trailingMembers = payload.Length / 4 - 4;
        if (trailingMembers != Math.Max(0, memberCount - 1))
            return false;

        var seenIds = new HashSet<int>();
        DateTime now = DateTime.UtcNow;
        world.PartyLeaderObjectId = leaderObjectId;

        while (r.Remaining >= 4)
        {
            int objectId = r.ReadInt32();
            if (objectId == 0 || objectId == selfObjectId)
                continue;

            seenIds.Add(objectId);
            var member = world.Party.GetOrAdd(objectId, id => new PartyMember { ObjectId = id });
            member.LastUpdateUtc = now;
        }

        foreach (var staleId in world.Party.Keys.Where(id => !seenIds.Contains(id)).ToArray())
            world.Party.TryRemove(staleId, out _);

        world.NotifyUpdated();
        return true;
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
            ClassId = classId,
            LastUpdateUtc = DateTime.UtcNow
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
        if (TryHandleCompactBartz(payload, world))
            return;

        if (payload.Length < 16) return;

        var r = new PacketReader(payload);
        int objectId = r.ReadInt32();

        if (world.Party.TryGetValue(objectId, out var member))
        {
            if (r.Remaining >= 4) member.CurHp = r.ReadInt32();
            if (r.Remaining >= 4) member.MaxHp = r.ReadInt32();
            if (r.Remaining >= 4) member.CurMp = r.ReadInt32();
            if (r.Remaining >= 4) member.MaxMp = r.ReadInt32();
            member.LastUpdateUtc = DateTime.UtcNow;
            world.NotifyUpdated();
        }
    }

    private static bool TryHandleCompactBartz(byte[] payload, GameWorld world)
    {
        if (payload.Length != 40)
            return false;

        var r = new PacketReader(payload);
        int objectId = r.ReadInt32();
        if (!world.Party.TryGetValue(objectId, out var member))
            return false;

        _ = r.ReadInt32();
        _ = r.ReadInt32();
        _ = r.ReadInt32();
        _ = r.ReadInt32();
        _ = r.ReadInt32();
        int x = r.ReadInt32();
        int y = r.ReadInt32();
        int z = r.ReadInt32();
        int heading = r.ReadInt32();

        DateTime now = DateTime.UtcNow;
        member.X = x;
        member.Y = y;
        member.Z = z;
        member.Heading = heading;
        member.LastPositionUpdateUtc = now;
        member.LastUpdateUtc = now;
        world.NotifyUpdated();
        return true;
    }
}
