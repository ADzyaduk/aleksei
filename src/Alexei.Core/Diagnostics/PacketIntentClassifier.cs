using Alexei.Core.Protocol;

namespace Alexei.Core.Diagnostics;

public static class PacketIntentClassifier
{
    public static string DescribeOutgoing(byte opcode, int payloadLength) => opcode switch
    {
        Opcodes.GameC2S.MoveBackwardToLocation => "MoveBackwardToLocation",
        Opcodes.GameC2S.Action => "Action",
        Opcodes.GameC2S.TargetEnter => "TargetEnter",
        Opcodes.GameC2S.AttackRequest => "AttackRequest",
        Opcodes.GameC2S.RequestItemUse => "RequestItemUse",
        Opcodes.GameC2S.RequestActionAttack when payloadLength == 9 => "RequestActionAttack/ShortcutSkillUse",
        Opcodes.GameC2S.RequestTargetCancel => "RequestTargetCancel",
        Opcodes.GameC2S.RequestMagicSkillUse => "RequestMagicSkillUse",
        Opcodes.GameC2S.RequestActionUse => "RequestActionUse",
        Opcodes.GameC2S.RequestGetItem => "RequestGetItem",
        Opcodes.GameC2S.RequestAttackUse59 => "RequestAttackUse59",
        Opcodes.GameC2S.RequestEnterWorld => "RequestEnterWorld",
        Opcodes.GameC2S.RequestPing => "RequestPing",
        _ => "OutgoingUnknown"
    };
}
