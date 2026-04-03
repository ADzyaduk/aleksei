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
    private static readonly TimeSpan MissingEffectRecastFloor = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MissingEffectRecastCeiling = TimeSpan.FromSeconds(15);

    public async Task ExecuteAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        if (!profile.Buffs.Enabled) return;
        if (world.Me.IsDead) return;
        var now = DateTime.UtcNow;

        foreach (var buff in profile.Buffs.List)
        {
            if (!buff.Enabled || buff.SkillId == 0) continue;
            if (!world.Skills.TryGetValue(buff.SkillId, out var skillInfo)) continue;
            if (!skillInfo.IsReady) continue;

            bool effectMissing = !world.Buffs.TryGetValue(buff.SkillId, out var active) || !active.IsActive;
            bool intervalElapsed = !_lastCast.TryGetValue(buff.SkillId, out var lastTime) ||
                                   now >= lastTime.AddSeconds(buff.IntervalSec);
            bool missingEffectGraceElapsed = !_lastCast.TryGetValue(buff.SkillId, out lastTime) ||
                                             now >= lastTime.Add(GetMissingEffectRecastDelay(buff.IntervalSec));

            if (!intervalElapsed && !(buff.RebuffOnMissing && effectMissing && missingEffectGraceElapsed))
                continue;

            // Cast buff
            if (buff.Target == "self")
            {
                // Cancel current target, cast on self
                await sender.SendAsync(GamePackets.CancelTarget());
                await Task.Delay(100, ct);
            }

            var pkt = BuildSkillPacket(buff.SkillId, profile.Combat.CombatSkillPacket);
            await sender.SendAsync(pkt);
            _lastCast[buff.SkillId] = now;
            await Task.Delay(200, ct);
            return; // One buff per tick
        }
    }

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
}
