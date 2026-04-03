using Alexei.Core.GameState;
using Alexei.Core.Protocol.Handlers;
using Xunit;

namespace Alexei.Core.Tests.Protocol.Handlers;

public sealed class NpcRespawnStateTests
{
    [Fact]
    public void NpcInfoHandler_ClearsDeathEvidence_WhenNpcAppearsAliveAgain()
    {
        var world = new GameWorld();
        world.Npcs[1001] = new Npc
        {
            ObjectId = 1001,
            NpcTypeId = 1_020_001,
            IsAttackable = true,
            IsDead = true,
            MaxHp = 100,
            CurHp = 0,
            LastDeathEvidenceUtc = DateTime.UtcNow.AddSeconds(-5),
            LastDropEvidenceUtc = DateTime.UtcNow.AddSeconds(-5)
        };

        var handler = new NpcInfoHandler();
        handler.Handle(BuildNpcInfoPayload(objectId: 1001, npcTypeId: 1_020_001, isAttackable: 1, x: 10, y: 20, z: 30, isDead: false, hpPercent: 100), world);

        var npc = world.Npcs[1001];
        Assert.False(npc.IsDead);
        Assert.Null(npc.LastDeathEvidenceUtc);
        Assert.Null(npc.LastDropEvidenceUtc);
        Assert.Equal(100, npc.CurHp);
    }

    [Fact]
    public void StatusUpdateHandler_ClearsDeathEvidence_WhenNpcHasPositiveHp()
    {
        var world = new GameWorld();
        world.Npcs[2002] = new Npc
        {
            ObjectId = 2002,
            NpcTypeId = 1_020_002,
            IsAttackable = true,
            IsDead = true,
            MaxHp = 500,
            CurHp = 0,
            LastDeathEvidenceUtc = DateTime.UtcNow.AddSeconds(-10),
            LastDropEvidenceUtc = DateTime.UtcNow.AddSeconds(-10)
        };

        var handler = new StatusUpdateHandler();
        handler.Handle(BuildStatusUpdatePayload(2002, (9, 250), (10, 500)), world);

        var npc = world.Npcs[2002];
        Assert.False(npc.IsDead);
        Assert.Equal(250, npc.CurHp);
        Assert.Equal(500, npc.MaxHp);
        Assert.Null(npc.LastDeathEvidenceUtc);
        Assert.Null(npc.LastDropEvidenceUtc);
    }

    private static byte[] BuildStatusUpdatePayload(int objectId, params (int attrId, int value)[] entries)
    {
        var payload = new byte[8 + entries.Length * 8];
        Buffer.BlockCopy(BitConverter.GetBytes(objectId), 0, payload, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(entries.Length), 0, payload, 4, 4);
        int offset = 8;
        foreach (var (attrId, value) in entries)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(attrId), 0, payload, offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, payload, offset + 4, 4);
            offset += 8;
        }

        return payload;
    }

    private static byte[] BuildNpcInfoPayload(int objectId, int npcTypeId, int isAttackable, int x, int y, int z, bool isDead, int hpPercent)
    {
        var bytes = new List<byte>();

        static void AddInt(List<byte> target, int value) => target.AddRange(BitConverter.GetBytes(value));
        static void AddDouble(List<byte> target, double value) => target.AddRange(BitConverter.GetBytes(value));
        static void AddString(List<byte> target, string value)
        {
            target.AddRange(System.Text.Encoding.Unicode.GetBytes(value));
            target.AddRange(new byte[] { 0, 0 });
        }

        AddInt(bytes, objectId);
        AddInt(bytes, npcTypeId);
        AddInt(bytes, isAttackable);
        AddInt(bytes, x);
        AddInt(bytes, y);
        AddInt(bytes, z);
        AddInt(bytes, 0);

        for (int i = 0; i < 11; i++) AddInt(bytes, 0);
        for (int i = 0; i < 4; i++) AddDouble(bytes, 0);
        for (int i = 0; i < 3; i++) AddInt(bytes, 0);

        bytes.Add(0);
        bytes.Add(0);
        bytes.Add(0);
        bytes.Add(isDead ? (byte)1 : (byte)0);
        bytes.Add(0);

        AddString(bytes, "Mob");
        AddString(bytes, "");

        for (int i = 0; i < 7; i++) AddInt(bytes, 0);
        bytes.Add(0);
        bytes.Add(0);
        AddDouble(bytes, 0);
        AddDouble(bytes, 0);
        AddInt(bytes, 0);
        AddInt(bytes, hpPercent);

        return bytes.ToArray();
    }
}
