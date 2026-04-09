using Alexei.Core.Config;
using Alexei.Core.GameState;
using Alexei.Core.Protocol;
using Alexei.Core.Proxy;

namespace Alexei.Core.Engine.Tasks;

public sealed class RecoveryTask : IBotTask
{
    public string Name => "Recovery";
    public bool IsEnabled => true;

    private DateTime _lastAction = DateTime.MinValue;

    public void ResetState(GameWorld world)
    {
        _lastAction = DateTime.MinValue;
    }

    public async Task<bool> ExecuteAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        if (DateTime.UtcNow < world.ActionLockUntilUtc)
            return false;

        var recovery = profile.Recovery;
        if (!recovery.Enabled) return false;
        if (world.Me.IsDead) return false;
        if (DateTime.UtcNow < _lastAction.AddSeconds(2)) return false;

        bool needSit = (world.Me.HpPct < recovery.SitBelowHpPct || world.Me.MpPct < recovery.SitBelowMpPct);
        bool canStand = (world.Me.HpPct >= recovery.StandAboveHpPct && world.Me.MpPct >= recovery.StandAboveMpPct);

        if (needSit && !world.Me.IsSitting && world.Me.TargetId == 0)
        {
            // No nearby mobs — sit to regen
            bool hasMobs = false;
            foreach (var npc in world.Npcs.Values)
            {
                if (npc.IsAttackable && !npc.IsDead && npc.DistanceTo(world.Me) < 500)
                {
                    hasMobs = true;
                    break;
                }
            }

            if (!hasMobs)
            {
                await sender.SendAsync(GamePackets.ActionUse(0)); // sit/stand toggle
                _lastAction = DateTime.UtcNow;
                world.ActionLockUntilUtc = DateTime.UtcNow.AddMilliseconds(500);
                return true;
            }
        }
        else if (world.Me.IsSitting && canStand)
        {
            await sender.SendAsync(GamePackets.ActionUse(0)); // stand up
            _lastAction = DateTime.UtcNow;
            world.ActionLockUntilUtc = DateTime.UtcNow.AddMilliseconds(500);
            return true;
        }

        return false;
    }
}
