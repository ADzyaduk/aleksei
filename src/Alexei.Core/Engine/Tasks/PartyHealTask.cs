using Alexei.Core.Config;
using Alexei.Core.GameState;
using Alexei.Core.Protocol;
using Alexei.Core.Proxy;

namespace Alexei.Core.Engine.Tasks;

public sealed class PartyHealTask : IBotTask
{
    public string Name => "PartyHeal";
    public bool IsEnabled => true;

    private DateTime _lastHeal = DateTime.MinValue;

    public async Task ExecuteAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        if (!profile.Party.Enabled) return;
        if (world.Me.IsDead || world.Party.IsEmpty) return;
        if (DateTime.UtcNow < _lastHeal.AddSeconds(1)) return;

        // Find party member most in need of heal
        PartyMember? target = null;
        HealRule? bestRule = null;

        foreach (var member in world.Party.Values)
        {
            foreach (var rule in profile.Party.HealRules)
            {
                if (!rule.Enabled || rule.SkillId == 0) continue;
                if (member.HpPct >= rule.HpThreshold) continue;
                if (world.Me.MpPct < rule.MpMinPct) continue;

                if (target == null || member.HpPct < target.HpPct)
                {
                    target = member;
                    bestRule = rule;
                }
            }
        }

        if (target == null || bestRule == null) return;

        // Target the party member (shift-click = no attack)
        await sender.SendAsync(GamePackets.Action(target.ObjectId, world.Me.X, world.Me.Y, world.Me.Z, 1));
        await Task.Delay(150, ct);

        // Cast heal
        var pkt = (profile.Combat.CombatSkillPacket ?? "2f").ToLower() == "2f"
            ? GamePackets.ShortcutSkillUse(bestRule.SkillId)
            : GamePackets.UseSkill(bestRule.SkillId, "ddd");
        await sender.SendAsync(pkt);
        _lastHeal = DateTime.UtcNow;
    }
}
