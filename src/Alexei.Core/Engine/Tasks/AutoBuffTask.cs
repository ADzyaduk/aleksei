using Alexei.Core.Config;
using Alexei.Core.GameState;
using Alexei.Core.Protocol;
using Alexei.Core.Proxy;

namespace Alexei.Core.Engine.Tasks;

public sealed class AutoBuffTask : IBotTask
{
    public string Name => "AutoBuff";
    public bool IsEnabled => true;

    private readonly Dictionary<int, DateTime> _lastCast = new();

    public async Task ExecuteAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        if (!profile.Buffs.Enabled) return;
        if (world.Me.IsDead) return;

        foreach (var buff in profile.Buffs.List)
        {
            if (!buff.Enabled || buff.SkillId == 0) continue;

            // Check if buff is active
            if (buff.RebuffOnMissing && world.Buffs.TryGetValue(buff.SkillId, out var active) && active.IsActive)
                continue;

            // Check interval
            if (_lastCast.TryGetValue(buff.SkillId, out var lastTime))
            {
                if (DateTime.UtcNow < lastTime.AddSeconds(buff.IntervalSec))
                    continue;
            }

            // Cast buff
            if (buff.Target == "self")
            {
                // Cancel current target, cast on self
                await sender.SendAsync(GamePackets.CancelTarget());
                await Task.Delay(100, ct);
            }

            var pkt = (profile.Combat.CombatSkillPacket ?? "2f").ToLower() == "2f"
                ? GamePackets.ShortcutSkillUse(buff.SkillId)
                : GamePackets.UseSkill(buff.SkillId, "ddd");
            await sender.SendAsync(pkt);
            _lastCast[buff.SkillId] = DateTime.UtcNow;
            await Task.Delay(200, ct);
            return; // One buff per tick
        }
    }
}
