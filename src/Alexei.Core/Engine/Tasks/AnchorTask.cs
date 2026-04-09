using Alexei.Core.Config;
using Alexei.Core.GameState;
using Alexei.Core.Protocol;
using Alexei.Core.Proxy;

namespace Alexei.Core.Engine.Tasks;

public sealed class AnchorTask : IBotTask
{
    public string Name => "Anchor";
    public bool IsEnabled => true;

    private DateTime _lastMove = DateTime.MinValue;

    public void ResetState(GameWorld world)
    {
        _lastMove = DateTime.MinValue;
    }

    public async Task<bool> ExecuteAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        if (DateTime.UtcNow < world.ActionLockUntilUtc)
            return false;

        var combat = profile.Combat;
        if (!combat.Enabled || combat.AnchorLeash <= 0) return false;
        if (world.Me.IsDead || !world.Me.AnchorSet) return false;
        if (DateTime.UtcNow < _lastMove.AddSeconds(3)) return false;

        var me = world.Me;
        double dist = Math.Sqrt(
            Math.Pow(me.X - me.AnchorX, 2) +
            Math.Pow(me.Y - me.AnchorY, 2));

        if (dist > combat.AnchorLeash)
        {
            if (world.Me.TargetId != 0 && world.Me.PendingTargetId == 0)
            {
                await sender.SendAsync(GamePackets.CancelTarget());
                world.Me.TargetId = 0;
                world.Me.PendingTargetId = 0;
                world.ActionLockUntilUtc = DateTime.UtcNow.AddMilliseconds(150);
                return true;
            }

            await sender.SendAsync(GamePackets.Move(me.AnchorX, me.AnchorY, me.AnchorZ, me.X, me.Y, me.Z));
            _lastMove = DateTime.UtcNow;
            world.ActionLockUntilUtc = DateTime.UtcNow.AddMilliseconds(500); // Wait a bit after move
            return true;
        }

        return false;
    }
}
