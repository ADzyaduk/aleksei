using Alexei.Core.Config;
using Alexei.Core.Diagnostics;
using Alexei.Core.GameState;
using Alexei.Core.Protocol;
using Alexei.Core.Proxy;

namespace Alexei.Core.Engine.Tasks;

public sealed class PartyBuffTask : IBotTask
{
    public string Name => "PartyBuff";
    public bool IsEnabled => true;

    private readonly PacketEvidenceCollector? _collector;
    private readonly Dictionary<(int SkillId, int TargetId), DateTime> _lastCast = new();
    private string? _lastTraceKey;
    private DateTime _lastTraceUtc = DateTime.MinValue;
    private static readonly TimeSpan MissingEffectRecastFloor = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MissingEffectRecastCeiling = TimeSpan.FromSeconds(15);

    public PartyBuffTask(PacketEvidenceCollector? collector = null)
    {
        _collector = collector;
    }

    public void ResetState(GameWorld world)
    {
        _lastCast.Clear();
        _lastTraceKey = null;
        _lastTraceUtc = DateTime.MinValue;
    }

    public async Task ExecuteAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        if (!profile.Party.Enabled || world.Me.IsDead)
            return;

        var now = DateTime.UtcNow;
        foreach (var rule in profile.Party.BuffRules)
        {
            if (!rule.Enabled || rule.SkillId == 0)
                continue;

            if (!world.Skills.TryGetValue(rule.SkillId, out var skillInfo))
            {
                Trace($"missing-skill:{rule.SkillId}", $"skip skill={rule.SkillId} reason=skill-missing");
                continue;
            }

            if (!skillInfo.IsReady)
            {
                Trace($"cooldown:{rule.SkillId}", $"skip skill={rule.SkillId} reason=server-cooldown");
                continue;
            }

            if (!TryResolveTarget(rule, world, profile.Party, out var target, out var targetLabel, out var reason))
            {
                Trace($"no-target:{rule.SkillId}:{rule.Target}", $"skip skill={rule.SkillId} reason={reason} target={rule.Target}");
                continue;
            }

            var key = (rule.SkillId, target.ObjectId);
            bool hasBuffEvidence = HasBuffEvidence(target, world);
            bool effectMissing = hasBuffEvidence && (!target.Buffs.TryGetValue(rule.SkillId, out var active) || !active.IsActive);
            bool intervalElapsed = !_lastCast.TryGetValue(key, out var lastTime) || now >= lastTime.AddSeconds(rule.IntervalSec);
            bool missingEffectGraceElapsed = !_lastCast.TryGetValue(key, out lastTime) ||
                                             now >= lastTime.Add(GetMissingEffectRecastDelay(rule.IntervalSec));
            bool allowMissingEffectRecast = rule.RebuffOnMissing && hasBuffEvidence && effectMissing && missingEffectGraceElapsed;

            if (!intervalElapsed && !allowMissingEffectRecast)
            {
                Trace($"interval:{rule.SkillId}:{target.ObjectId}", $"skip skill={rule.SkillId} target={targetLabel} reason=interval");
                continue;
            }

            if (target.ObjectId == world.Me.ObjectId || string.Equals(rule.Target, "self", StringComparison.OrdinalIgnoreCase))
            {
                await sender.SendAsync(GamePackets.CancelTarget(), ct);
                await Task.Delay(100, ct);
            }
            else
            {
                var targetPacket = profile.Combat.UseTargetEnter
                    ? GamePackets.TargetEnter(target.ObjectId, world.Me.X, world.Me.Y, world.Me.Z)
                    : GamePackets.Action(target.ObjectId, world.Me.X, world.Me.Y, world.Me.Z, 1);

                await sender.SendAsync(targetPacket, ct);
                await Task.Delay(150, ct);
                world.Me.TargetId = target.ObjectId;
                world.Me.PendingTargetId = 0;
            }

            await sender.SendAsync(BuildSkillPacket(rule.SkillId, profile.Combat.CombatSkillPacket), ct);
            _lastCast[key] = now;
            world.ActionLockUntilUtc = now.AddMilliseconds(2000);
            Trace($"cast:{rule.SkillId}:{target.ObjectId}", $"cast skill={rule.SkillId} target={targetLabel}");
            await Task.Delay(200, ct);
            return;
        }
    }

    private static bool HasBuffEvidence(PartyMember target, GameWorld world) =>
        target.ObjectId == world.Me.ObjectId || target.Buffs.Count > 0;

    private static bool TryResolveTarget(BuffEntry rule, GameWorld world, PartyConfig party, out PartyMember target, out string targetLabel, out string reason)
    {
        string mode = (rule.Target ?? "party").Trim().ToLowerInvariant();

        if (mode == "self")
        {
            target = new PartyMember
            {
                ObjectId = world.Me.ObjectId,
                Name = world.Me.Name,
                X = world.Me.X,
                Y = world.Me.Y,
                Z = world.Me.Z,
                LastUpdateUtc = DateTime.UtcNow,
                LastPositionUpdateUtc = DateTime.UtcNow,
                CurHp = world.Me.CurHp,
                MaxHp = world.Me.MaxHp
            };
            targetLabel = "self";
            reason = "ok";
            return true;
        }

        if (mode == "leader")
        {
            var leader = PartyMemberResolver.ResolveConfiguredMember(world, party.LeaderName)
                         ?? PartyMemberResolver.ResolveLeaderByObjectId(world)
                         ?? PartyMemberResolver.ResolveSoleMember(world);
            if (leader != null)
            {
                target = leader;
                targetLabel = DescribeMember(leader);
                reason = "ok";
                return true;
            }

            target = default!;
            targetLabel = string.Empty;
            reason = "leader-unresolved";
            return false;
        }

        if (mode == "assist")
        {
            var assist = PartyMemberResolver.ResolveConfiguredMember(world, party.AssistName)
                         ?? PartyMemberResolver.ResolveConfiguredMember(world, party.LeaderName)
                         ?? PartyMemberResolver.ResolveLeaderByObjectId(world)
                         ?? PartyMemberResolver.ResolveSoleMember(world);
            if (assist != null)
            {
                target = assist;
                targetLabel = DescribeMember(assist);
                reason = "ok";
                return true;
            }

            target = default!;
            targetLabel = string.Empty;
            reason = "assist-unresolved";
            return false;
        }

        if (world.Party.Count == 1)
        {
            var member = PartyMemberResolver.ResolveSoleMember(world)!;
            target = member;
            targetLabel = DescribeMember(member);
            reason = "ok";
            return true;
        }

        var leaderFallback = PartyMemberResolver.ResolveConfiguredMember(world, party.LeaderName)
                             ?? PartyMemberResolver.ResolveLeaderByObjectId(world);
        if (leaderFallback != null)
        {
            target = leaderFallback;
            targetLabel = DescribeMember(leaderFallback);
            reason = "ok";
            return true;
        }

        target = default!;
        targetLabel = string.Empty;
        reason = "ambiguous-party-target";
        return false;
    }

    private static string DescribeMember(PartyMember member) => PartyMemberResolver.DescribeMember(member);

    private static TimeSpan GetMissingEffectRecastDelay(double intervalSec)
    {
        if (intervalSec <= 0)
            return MissingEffectRecastFloor;

        var scaled = TimeSpan.FromSeconds(intervalSec / 8d);
        if (scaled < MissingEffectRecastFloor) return MissingEffectRecastFloor;
        if (scaled > MissingEffectRecastCeiling) return MissingEffectRecastCeiling;
        return scaled;
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
        _collector?.RecordBehavior("PartyBuff", message);
    }
}

