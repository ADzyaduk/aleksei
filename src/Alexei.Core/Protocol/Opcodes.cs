namespace Alexei.Core.Protocol;

/// <summary>
/// Base opcodes for Teon/Elmorelab Interlude protocol (before per-session XOR scrambling).
/// These differ from standard L2J — values taken from working Python bot reference.
/// </summary>
public static class Opcodes
{
    // ── Login Server → Client ──
    public static class LoginS2C
    {
        public const byte Init = 0x00;
        public const byte LoginFail = 0x01;
        public const byte ServerList = 0x04;
        public const byte PlayOk = 0x07;
    }

    // ── Login Client → Server ──
    public static class LoginC2S
    {
        public const byte AuthLogin = 0x00;
        public const byte RequestPlay = 0x02;
    }

    // ── Game Server → Client (Teon base opcodes) ──
    public static class GameS2C
    {
        public const byte BlowfishInit = 0x00;
        public const byte MoveToPoint = 0x01;
        public const byte UserInfo = 0x04;
        public const byte Die = 0x06;             // primary die on Teon (was 0x12 in std L2J)
        public const byte SpawnItem = 0x0C;
        public const byte StatusUpdate2 = 0x0E;   // secondary status update (self HP/MP/CP)
        public const byte Die2 = 0x12;             // short die notify (4-byte objectId only)
        public const byte ValidatePosition = 0x14;
        public const byte NpcInfo = 0x16;
        public const byte ItemList = 0x1B;
        public const byte ChangeWaitType = 0x25;
        public const byte StopMove = 0x2D;
        public const byte TargetSelected = 0x47;   // Python reference: 0x3D ^ 0x7A = 0x47 (base opcode)
        public const byte MagicSkillLaunched = 0x48;
        public const byte PartySmallWindowAll = 0x4E;
        public const byte PartySmallWindowAdd = 0x4F;
        public const byte PartySmallWindowDelete = 0x50;
        public const byte PartySmallWindowUpdate = 0x52;
        public const byte SkillList = 0x58;
        public const byte Attack = 0x60;
        public const byte SkillCoolTime = 0x6A;
        public const byte StatusUpdate = 0x6D;     // primary status update on Teon (was 0x0E in std L2J)
        public const byte DeleteObject = 0x72;     // Teon (was 0x0B in std L2J)
        public const byte AbnormalStatusUpdate = 0x7F;
        public const byte SystemMessage = 0x62;   // standard L2J, confirmed from Python reference
    }

    // ── Game Client → Server ──
    public static class GameC2S
    {
        public const byte MoveBackwardToLocation = 0x01;
        public const byte Action = 0x04;
        public const byte TargetEnter = 0x1F;     // Bartz: target NPC/item (same opcode for loot pickup)
        public const byte AuthLogin = 0x08;
        public const byte CharSelected = 0x09;
        public const byte AttackRequest = 0x0A;
        public const byte RequestItemUse = 0x19;
        public const byte RequestActionAttack = 0x2F;  // Teon: actionId=16 for attack, actionId=skillId for skill cast
        public const byte RequestTargetCancel = 0x37;
        public const byte RequestMagicSkillUse = 0x39;
        public const byte RequestActionUse = 0x45;
        public const byte RequestGetItem = 0x48;
        public const byte RequestAttackUse59 = 0x59;
        public const byte RequestEnterWorld = 0x5C;
        public const byte RequestPing = 0x9D;
    }

    // ── StatusUpdate Attribute IDs ──
    public static class Attr
    {
        public const int Level = 0x01;
        public const int CurHp = 0x09;
        public const int MaxHp = 0x0A;
        public const int CurMp = 0x0B;
        public const int MaxMp = 0x0C;
        public const int CurExp = 0x0D;
        public const int SP = 0x0F;
        public const int CurCp = 0x21;
        public const int MaxCp = 0x22;
    }
}
