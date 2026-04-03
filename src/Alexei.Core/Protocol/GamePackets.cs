namespace Alexei.Core.Protocol;

/// <summary>
/// Static builders for Client в†’ Server game packets (Interlude).
/// Each method returns (opcode, payload).
/// </summary>
public static class GamePackets
{
    public static (byte opcode, byte[] payload) Move(int destX, int destY, int destZ, int origX, int origY, int origZ, int moveMode = 0)
    {
        var w = new PacketWriter(28);
        w.WriteInt32(destX).WriteInt32(destY).WriteInt32(destZ);
        w.WriteInt32(origX).WriteInt32(origY).WriteInt32(origZ);
        w.WriteInt32(moveMode);
        return (Opcodes.GameC2S.MoveBackwardToLocation, w.ToArray());
    }

    public static (byte opcode, byte[] payload) Action(int objectId, int x, int y, int z, byte actionId = 0)
    {
        var w = new PacketWriter(17);
        w.WriteInt32(objectId).WriteInt32(x).WriteInt32(y).WriteInt32(z).WriteByte(actionId);
        return (Opcodes.GameC2S.Action, w.ToArray());
    }

    /// <summary>
    /// Target or loot an object on Bartz (opcode 0x1F).
    /// Used instead of Action (0x04) for targeting and GetItem (0x48) for loot pickup.
    /// </summary>
    public static (byte opcode, byte[] payload) TargetEnter(int objectId, int x, int y, int z, byte tail = 0)
    {
        var w = new PacketWriter(17);
        w.WriteInt32(objectId).WriteInt32(x).WriteInt32(y).WriteInt32(z).WriteByte(tail);
        return (Opcodes.GameC2S.TargetEnter, w.ToArray());
    }

    public static (byte opcode, byte[] payload) AttackRequest(int objectId, int x, int y, int z, byte shiftFlag = 0)
    {
        var w = new PacketWriter(17);
        w.WriteInt32(objectId).WriteInt32(x).WriteInt32(y).WriteInt32(z).WriteByte(shiftFlag);
        return (Opcodes.GameC2S.AttackRequest, w.ToArray());
    }

    /// <summary>
    /// Cast a skill. Format depends on server: "ddd" (12B), "dcb" (9B), "dcc" (6B).
    /// </summary>
    public static (byte opcode, byte[] payload) UseSkill(int skillId, string format = "ddd", bool ctrl = false, bool shift = false)
    {
        var w = new PacketWriter(12);
        w.WriteInt32(skillId);
        switch (format.ToLower())
        {
            case "ddd":
                w.WriteInt32(ctrl ? 1 : 0).WriteInt32(shift ? 1 : 0);
                break;
            case "dcb":
                w.WriteInt32(ctrl ? 1 : 0).WriteByte(shift ? (byte)1 : (byte)0);
                break;
            case "dcc":
                w.WriteByte(ctrl ? (byte)1 : (byte)0).WriteByte(shift ? (byte)1 : (byte)0);
                break;
            default:
                w.WriteInt32(ctrl ? 1 : 0).WriteInt32(shift ? 1 : 0);
                break;
        }
        return (Opcodes.GameC2S.RequestMagicSkillUse, w.ToArray());
    }

    /// <summary>
    /// Cast a skill via shortcut bar (opcode 0x2F). Confirmed on Teon/Elmorelab.
    /// Format: skillId(int32) + ctrl(int32) + shift(byte), 9-byte payload.
    /// </summary>
    public static (byte opcode, byte[] payload) ShortcutSkillUse(int skillId, bool ctrl = false, bool shift = false)
    {
        var w = new PacketWriter(9);
        w.WriteInt32(skillId).WriteInt32(ctrl ? 1 : 0).WriteByte(shift ? (byte)1 : (byte)0);
        return (Opcodes.GameC2S.RequestActionAttack, w.ToArray());
    }

    /// <summary>
    /// Force-attack current target via opcode 0x2F with actionId=16.
    /// Confirmed from real Teon client traffic.
    /// </summary>
    public static (byte opcode, byte[] payload) ForceAttack()
    {
        var w = new PacketWriter(9);
        w.WriteInt32(16).WriteInt32(0).WriteByte(0);
        return (Opcodes.GameC2S.RequestActionAttack, w.ToArray());
    }

    public static (byte opcode, byte[] payload) CancelTarget()
    {
        var w = new PacketWriter(4);
        w.WriteInt32(0);
        return (Opcodes.GameC2S.RequestTargetCancel, w.ToArray());
    }

    /// <summary>
    /// Sit/stand (actionId=0), walk/run, etc.
    /// </summary>
    public static (byte opcode, byte[] payload) ActionUse(int actionId, bool ctrl = false, bool shift = false)
    {
        var w = new PacketWriter(9);
        w.WriteInt32(actionId).WriteInt32(ctrl ? 1 : 0).WriteByte(shift ? (byte)1 : (byte)0);
        return (Opcodes.GameC2S.RequestActionUse, w.ToArray());
    }

    public static (byte opcode, byte[] payload) PickupItem(int objectId, int x, int y, int z)
    {
        var w = new PacketWriter(20);
        w.WriteInt32(x).WriteInt32(y).WriteInt32(z).WriteInt32(objectId).WriteInt32(0);
        return (Opcodes.GameC2S.RequestGetItem, w.ToArray());
    }

    /// <summary>
    /// Bartz-specific engage/attack action captured from the live client.
    /// Format: xyz(int32 * 3) + attackParam(int32) + reserved(int32).
    /// </summary>
    public static (byte opcode, byte[] payload) AttackUse59(int x, int y, int z, int attackParam = 0)
    {
        var w = new PacketWriter(20);
        w.WriteInt32(x).WriteInt32(y).WriteInt32(z).WriteInt32(attackParam).WriteInt32(0);
        return (Opcodes.GameC2S.RequestAttackUse59, w.ToArray());
    }

    /// <summary>
    /// Bartz client was observed sending a short GetItem packet with a 2-byte zero payload.
    /// </summary>
    public static (byte opcode, byte[] payload) PickupItemShort()
    {
        var w = new PacketWriter(2);
        w.WriteInt16(0);
        return (Opcodes.GameC2S.RequestGetItem, w.ToArray());
    }

    public static (byte opcode, byte[] payload) UseItem(int objectId, bool ctrl = false)
    {
        var w = new PacketWriter(8);
        w.WriteInt32(objectId).WriteInt32(ctrl ? 1 : 0);
        return (Opcodes.GameC2S.RequestItemUse, w.ToArray());
    }

    public static (byte opcode, byte[] payload) Ping()
    {
        return (Opcodes.GameC2S.RequestPing, Array.Empty<byte>());
    }

    public static (byte opcode, byte[] payload) EnterWorld()
    {
        return (Opcodes.GameC2S.RequestEnterWorld, Array.Empty<byte>());
    }
}

