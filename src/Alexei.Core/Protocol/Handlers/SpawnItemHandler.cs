using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

public sealed class SpawnItemHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.SpawnItem;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (payload.Length < 28) return;

        var r = new PacketReader(payload);
        int dropperId = r.ReadInt32();
        int objectId = r.ReadInt32();
        int itemId = r.ReadInt32();
        int x = r.ReadInt32();
        int y = r.ReadInt32();
        int z = r.ReadInt32();
        int stackable = r.ReadInt32();
        long count = payload.Length >= 36 ? r.ReadInt64() : 1;

        var item = world.Items.GetOrAdd(objectId, _ => new GroundItem());
        item.ObjectId = objectId;
        item.ItemId = itemId;
        item.X = x;
        item.Y = y;
        item.Z = z;
        item.Count = count;
        item.DropperObjectId = dropperId != 0 ? dropperId : null;
        item.SpawnedAtUtc = DateTime.UtcNow;

        if (dropperId != 0 && world.Npcs.TryGetValue(dropperId, out var npc))
        {
            npc.LastDropEvidenceUtc = DateTime.UtcNow;
            npc.LastDeathEvidenceUtc ??= npc.LastDropEvidenceUtc;
            npc.IsDead = true;
            npc.LastUpdate = DateTime.UtcNow;
        }

        world.NotifyUpdated();
    }
}
