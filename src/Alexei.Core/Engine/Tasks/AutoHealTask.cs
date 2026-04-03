using Alexei.Core.Config;
using Alexei.Core.GameState;
using Alexei.Core.Protocol;
using Alexei.Core.Proxy;

namespace Alexei.Core.Engine.Tasks;

public sealed class AutoHealTask : IBotTask
{
    public string Name => "AutoHeal";
    public bool IsEnabled => true;

    private DateTime _lastHeal = DateTime.MinValue;

    public async Task ExecuteAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        if (!profile.Party.Enabled) return; // heal rules are in party config
        if (world.Me.IsDead) return;

        // Self-heal: check party heal rules that target self
        foreach (var rule in profile.Party.HealRules)
        {
            if (!rule.Enabled || rule.SkillId == 0) continue;
            if (world.Me.HpPct >= rule.HpThreshold) continue;
            if (world.Me.MpPct < rule.MpMinPct) continue;

            if (DateTime.UtcNow < _lastHeal.AddSeconds(2)) continue;

            var pkt = (profile.Combat.CombatSkillPacket ?? "2f").ToLower() == "2f"
                ? GamePackets.ShortcutSkillUse(rule.SkillId)
                : GamePackets.UseSkill(rule.SkillId, "ddd");
            await sender.SendAsync(pkt);
            _lastHeal = DateTime.UtcNow;
            return;
        }
    }
}
