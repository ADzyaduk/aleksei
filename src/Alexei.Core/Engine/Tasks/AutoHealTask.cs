using Alexei.Core.Config;
using Alexei.Core.GameState;
using Alexei.Core.Protocol;
using Alexei.Core.Proxy;

namespace Alexei.Core.Engine.Tasks;

public sealed class AutoHealTask : IBotTask
{
    public string Name => "AutoHeal";
    public bool IsEnabled => true;

    private readonly Dictionary<int, DateTime> _lastHealBySkill = new();

    public void ResetState(GameWorld world)
    {
        _lastHealBySkill.Clear();
    }

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
            if (!world.Skills.TryGetValue(rule.SkillId, out var skillInfo)) continue;
            if (!skillInfo.IsReady) continue;

            if (_lastHealBySkill.TryGetValue(rule.SkillId, out var lastHeal) &&
                DateTime.UtcNow < lastHeal.AddMilliseconds(Math.Max(0, rule.CooldownMs)))
            {
                continue;
            }

            var pkt = BuildSkillPacket(rule.SkillId, profile.Combat.CombatSkillPacket);
            await sender.SendAsync(pkt);
            _lastHealBySkill[rule.SkillId] = DateTime.UtcNow;
            world.ActionLockUntilUtc = DateTime.UtcNow.AddMilliseconds(2000);
            return;
        }
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
