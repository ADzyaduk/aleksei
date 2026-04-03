using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

/// <summary>
/// AbnormalStatusUpdate (0x7F) — two variants:
/// Variant A: [objectId(4)] [count(2)] [skillId(4) level(2) duration(4)]×N
/// Variant B: [count(4)] [skillId(4) level(2) duration(4)]×N  (no objectId, self only)
/// Detection: if first 4 bytes ≤ 1000 and match effect count → Variant B.
/// </summary>
public sealed class AbnormalStatusHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.AbnormalStatusUpdate;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (payload.Length < 4) return;

        var r = new PacketReader(payload);

        // Try to detect variant
        int first4 = BitConverter.ToInt32(payload, 0);
        bool isVariantB = first4 >= 0 && first4 <= 1000 && CanBeEffectCount(first4, payload.Length, hasObjectId: false);

        int count;
        if (isVariantB)
        {
            // Variant B: no objectId, count is int32
            count = r.ReadInt32();
        }
        else
        {
            // Variant A: objectId + count(short)
            int objectId = r.ReadInt32();
            if (objectId != world.Me.ObjectId)
                return; // We only track self buffs for now
            count = r.ReadInt16();
        }

        world.Buffs.Clear();
        for (int i = 0; i < count && r.Remaining >= 10; i++)
        {
            int skillId = r.ReadInt32();
            short level = r.ReadInt16();
            int duration = r.ReadInt32();

            var effect = new AbnormalEffect
            {
                SkillId = skillId,
                Level = level
            };
            effect.SetDuration(duration);
            world.Buffs[skillId] = effect;
        }

        world.NotifyUpdated();
    }

    private static bool CanBeEffectCount(int count, int payloadLen, bool hasObjectId)
    {
        int headerSize = hasObjectId ? 6 : 4; // objectId(4)+count(2) or count(4)
        int effectSize = 10; // skillId(4) + level(2) + duration(4)
        int expected = headerSize + count * effectSize;
        return expected <= payloadLen + 4; // allow some slack
    }
}
