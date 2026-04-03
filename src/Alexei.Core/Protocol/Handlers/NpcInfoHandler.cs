using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

/// <summary>
/// NpcInfo (base 0x16) — NPC/monster appeared in range.
/// Structure verified against Python reference (alesha bot).
/// </summary>
public sealed class NpcInfoHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.NpcInfo;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (payload.Length < 48) return;

        var r = new PacketReader(payload);

        int objectId = r.ReadInt32();
        int npcTypeId = r.ReadInt32();
        int isAttackable = r.ReadInt32();
        int x = r.ReadInt32();
        int y = r.ReadInt32();
        int z = r.ReadInt32();
        int heading = r.ReadInt32();

        // unk, mAtk, pAtk, runSpeed, walkSpeed, swimRunSpeed, swimWalkSpeed,
        // flRunSpeed, flWalkSpeed, flyRunSpeed, flyWalkSpeed (11 ints = 44 bytes)
        r.Skip(4 * 11);

        // movMult, atkSpeedMult, colRadius, colHeight (4 doubles = 32 bytes)
        r.Skip(8 * 4);

        // rHand, unk, lHand (3 ints = 12 bytes)
        r.Skip(4 * 3);

        // nameAboveChar, isRunning, inCombat, isDead, isSummoned (5 bytes)
        r.Skip(3); // nameAboveChar, isRunning, inCombat
        bool isDead = r.ReadByte() != 0;
        r.Skip(1); // isSummoned

        // name, title (UTF-16LE null-terminated)
        string name = r.ReadString();
        string title = r.ReadString();

        // pvpFlag, karma, abnormalEffect, clanId, clanCrestId, allyId, allyCrestId
        r.Skip(4 * 7);

        // isFlying, team (2 bytes)
        r.Skip(2);

        // colRadius2, colHeight2 (2 doubles = 16 bytes)
        r.Skip(8 * 2);

        // enchant (4 bytes)
        r.Skip(4);

        // hpPercent
        int hpPercent = 100;
        if (r.Remaining >= 4)
            hpPercent = r.ReadInt32();

        var npc = world.Npcs.GetOrAdd(objectId, _ => new Npc());
        npc.ObjectId = objectId;
        npc.NpcTypeId = npcTypeId;
        npc.Name = name;
        npc.X = x;
        npc.Y = y;
        npc.Z = z;
        npc.Heading = heading;
        npc.IsAttackable = isAttackable != 0;
        npc.HpPercent = Math.Clamp(hpPercent, 0, 100);
        npc.IsDead = isDead;
        if (!isDead)
        {
            npc.LastDeathEvidenceUtc = null;
            npc.LastDropEvidenceUtc = null;
            if (npc.MaxHp > 0 && npc.CurHp <= 0)
                npc.CurHp = npc.MaxHp;
        }
        npc.LastUpdate = DateTime.UtcNow;

        world.NotifyUpdated();
    }
}
