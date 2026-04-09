using Alexei.Core.Config;
using Alexei.Core.Diagnostics;
using Alexei.Core.GameState;
using Alexei.Core.Protocol;
using Alexei.Core.Proxy;

namespace Alexei.Core.Engine.Tasks;

public sealed class PartyHealTask : IBotTask
{
    public string Name => "PartyHeal";
    public bool IsEnabled => true;

    private readonly PacketEvidenceCollector? _collector;
    private readonly Dictionary<int, DateTime> _lastHealBySkill = new();
    private string? _lastTraceKey;
    private DateTime _lastTraceUtc = DateTime.MinValue;

    public PartyHealTask(PacketEvidenceCollector? collector = null)
    {
        _collector = collector;
    }

    public void ResetState(GameWorld world)
    {
        _lastHealBySkill.Clear();
        _lastTraceKey = null;
        _lastTraceUtc = DateTime.MinValue;
    }

    public async Task<bool> ExecuteAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        if (!profile.Party.Enabled) return false;
        if (world.Me.IsDead || world.Party.IsEmpty) return false;
        if (DateTime.UtcNow < world.ActionLockUntilUtc) return false;

        PartyMember? target = null;
        HealRule? bestRule = null;
        double bestPriority = double.MinValue;

        foreach (var member in world.Party.Values)
        {
            if (!member.HasVitals)
                continue;

            foreach (var rule in profile.Party.HealRules)
            {
                if (!rule.Enabled || rule.SkillId == 0) continue;
                if (world.Me.MpPct < rule.MpMinPct) continue;
                if (!world.Skills.TryGetValue(rule.SkillId, out var skillInfo)) continue;
                if (!skillInfo.IsReady) continue;
                if (_lastHealBySkill.TryGetValue(rule.SkillId, out var lastHeal) &&
                    DateTime.UtcNow < lastHeal.AddMilliseconds(Math.Max(0, rule.CooldownMs)))
                {
                    continue;
                }

                bool hpTriggered = rule.HpThreshold > 0 && member.HpPct < rule.HpThreshold;
                bool mpTriggered = rule.MpThreshold > 0 && member.MaxMp > 0 && member.MpPct < rule.MpThreshold;
                if (!hpTriggered && !mpTriggered)
                    continue;

                double hpPriority = hpTriggered ? rule.HpThreshold - member.HpPct : double.MinValue;
                double mpPriority = mpTriggered ? rule.MpThreshold - member.MpPct : double.MinValue;
                double priority = Math.Max(hpPriority, mpPriority);
                if (priority <= bestPriority)
                    continue;

                target = member;
                bestRule = rule;
                bestPriority = priority;
            }
        }

        if (target == null || bestRule == null)
        {
            Trace("no-eligible-target", "skip reason=no-eligible-target");
            return false;
        }

        if (world.Me.TargetId != target.ObjectId)
        {
            var targetPacket = profile.Combat.UseTargetEnter
                ? GamePackets.TargetEnter(target.ObjectId, world.Me.X, world.Me.Y, world.Me.Z)
                : GamePackets.Action(target.ObjectId, world.Me.X, world.Me.Y, world.Me.Z, 1);

            await sender.SendAsync(targetPacket, ct);
            world.Me.TargetId = target.ObjectId;
            world.Me.PendingTargetId = 0;
            world.ActionLockUntilUtc = DateTime.UtcNow.AddMilliseconds(200);
            return true;
        }

        var pkt = BuildSkillPacket(bestRule.SkillId, profile.Combat.CombatSkillPacket);
        await sender.SendAsync(pkt, ct);
        _lastHealBySkill[bestRule.SkillId] = DateTime.UtcNow;
        world.ActionLockUntilUtc = DateTime.UtcNow.AddMilliseconds(1500);
        Trace($"cast:{bestRule.SkillId}:{target.ObjectId}", $"cast skill={bestRule.SkillId} target={target.ObjectId} hpPct={target.HpPct:F1}");
        return true;
    }

    private static (byte opcode, byte[] payload) BuildSkillPacket(int skillId, string? packetType) =>
        (packetType ?? "2f").ToLowerInvariant() switch
        {
            "2f" => GamePackets.ShortcutSkillUse(skillId),
            "39dcb" or "dcb" => GamePackets.UseSkill(skillId, "dcb"),
            "39dcc" or "dcc" => GamePackets.UseSkill(skillId, "dcc"),
            _ => GamePackets.UseSkill(skillId, "ddd")
        };

    private void Trace(string key, string message)
    {
        var now = DateTime.UtcNow;
        if (string.Equals(_lastTraceKey, key, StringComparison.Ordinal) && now - _lastTraceUtc < TimeSpan.FromSeconds(1))
            return;

        _lastTraceKey = key;
        _lastTraceUtc = now;
        _collector?.RecordBehavior("PartyHeal", message);
    }
}

