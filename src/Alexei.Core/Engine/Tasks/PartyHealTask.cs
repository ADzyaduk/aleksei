using Alexei.Core.Config;
using Alexei.Core.GameState;
using Alexei.Core.Protocol;
using Alexei.Core.Proxy;

namespace Alexei.Core.Engine.Tasks;

public sealed class PartyHealTask : IBotTask
{
    public string Name => "PartyHeal";
    public bool IsEnabled => true;

    private readonly Dictionary<int, DateTime> _lastHealBySkill = new();

    public async Task ExecuteAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        if (!profile.Party.Enabled) return;
        if (world.Me.IsDead || world.Party.IsEmpty) return;

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
                if (!world.Skills.TryGetValue(rule.SkillId, out var skillInfo)) continue;
                if (!skillInfo.IsReady) continue;
                if (_lastHealBySkill.TryGetValue(rule.SkillId, out var lastHeal) &&
                    DateTime.UtcNow < lastHeal.AddMilliseconds(Math.Max(0, rule.CooldownMs)))
                {
                    continue;
                }

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
        var pkt = BuildSkillPacket(bestRule.SkillId, profile.Combat.CombatSkillPacket);
        await sender.SendAsync(pkt);
        _lastHealBySkill[bestRule.SkillId] = DateTime.UtcNow;
    }

    private static (byte opcode, byte[] payload) BuildSkillPacket(int skillId, string? packetType) =>
        (packetType ?? "2f").ToLowerInvariant() switch
        {
            "2f" => GamePackets.ShortcutSkillUse(skillId),
            "39dcb" or "dcb" => GamePackets.UseSkill(skillId, "dcb"),
            "39dcc" or "dcc" => GamePackets.UseSkill(skillId, "dcc"),
            _ => GamePackets.UseSkill(skillId, "ddd")
        };
}
