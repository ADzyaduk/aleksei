using Alexei.Core.Protocol;
using Xunit;

namespace Alexei.Core.Tests.Protocol;

public sealed class GamePacketsTests
{
    [Fact]
    public void AttackUse59_WritesHeadingAsAttackParam()
    {
        var (opcode, payload) = GamePackets.AttackUse59(-1277, 97498, -3699, attackParam: 0x563B);

        Assert.Equal(Opcodes.GameC2S.RequestAttackUse59, opcode);
        Assert.Equal(20, payload.Length);
        Assert.Equal(-1277, BitConverter.ToInt32(payload, 0));
        Assert.Equal(97498, BitConverter.ToInt32(payload, 4));
        Assert.Equal(-3699, BitConverter.ToInt32(payload, 8));
        Assert.Equal(0x563B, BitConverter.ToInt32(payload, 12));
        Assert.Equal(0, BitConverter.ToInt32(payload, 16));
    }
}
