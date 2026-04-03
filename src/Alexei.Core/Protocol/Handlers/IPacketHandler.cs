using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

public interface IPacketHandler
{
    byte BaseOpcode { get; }
    void Handle(byte[] payload, GameWorld world);
}
