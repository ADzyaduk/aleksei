using Alexei.Core.Config;
using Alexei.Core.GameState;
using Alexei.Core.Proxy;

namespace Alexei.Core.Engine.Tasks;

public interface IBotTask
{
    string Name { get; }
    bool IsEnabled { get; }
    Task<bool> ExecuteAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct);
    void ResetState(GameWorld world);
}
