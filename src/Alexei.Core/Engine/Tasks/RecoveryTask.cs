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

    public async Task ExecuteAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        if (DateTime.UtcNow < world.ActionLockUntilUtc)
            return;

        var recovery = profile.Recovery;
        if (!recovery.Enabled) return;
        if (world.Me.IsDead) return;
        if (DateTime.UtcNow < _lastAction.AddSeconds(2)) return;

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
            }
        }
        else if (world.Me.IsSitting && canStand)
        {
            await sender.SendAsync(GamePackets.ActionUse(0)); // stand up
            _lastAction = DateTime.UtcNow;
        }
    }
}
