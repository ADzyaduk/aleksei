using Alexei.Core.Config;
using Alexei.Core.Diagnostics;
using Alexei.Core.GameState;
using Alexei.Core.Protocol;
using Alexei.Core.Proxy;
using Microsoft.Extensions.Logging;

namespace Alexei.Core.Engine.Tasks;

public sealed class AutoCombatTask : IBotTask
{
    private const int MaxPickupAttemptsPerItem = 3;
    public string Name => "AutoCombat";
    public bool IsEnabled => true;

    private readonly ILogger? _logger;
    private readonly PacketEvidenceCollector? _collector;
    private DateTime _lastAttack = DateTime.MinValue;
    private DateTime _lastSkillUse = DateTime.MinValue;
    private DateTime _lastTargetAction = DateTime.MinValue;
    private DateTime _lastReattack = DateTime.MinValue;
    private DateTime _lastPickup = DateTime.MinValue;
    private int _currentSkillIndex;
    private int _pendingTargetId;
    private int _lastSweepTargetId;
    private DateTime _lastSweepTime = DateTime.MinValue;
    private DateTime _lastDebug = DateTime.MinValue;
    private CombatPhase _phase = CombatPhase.Idle;
    private DateTime _phaseSince = DateTime.UtcNow;
    private int _retainedTargetId;
    private int _postKillTargetId;
    private bool _openingSkillDone;
    private bool _postKillCancelledTarget;
    private bool _postKillSweepDone;
    private DateTime _lootWindowEndsAt = DateTime.MinValue;
    private int _lootWindowEmptyPolls;
    private DateTime _lastMovementStaleTrace = DateTime.MinValue;
    private DateTime _lastEngageNoProgressTrace = DateTime.MinValue;

    public AutoCombatTask(ILogger? logger = null, PacketEvidenceCollector? collector = null)
    {
        _logger = logger;
        _collector = collector;
    }

    public async Task ExecuteAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        var combat = profile.Combat;
        var me = world.Me;

        if (!combat.Enabled)
        {
            ResetCombatState(world, "combat disabled");
            return;
        }

        if (me.IsDead)
        {
            SetPhase(world, CombatPhase.Recovering, "me dead");
            return;
        }

        if (me.IsSitting)
        {
            SetPhase(world, CombatPhase.Recovering, "me sitting");
            return;
        }

        if (_phase == CombatPhase.Recovering)
            SetPhase(world, CombatPhase.Idle, "recovery ended");

        ClearPendingTargetIfNeeded(world, combat);

        if (_retainedTargetId != 0 && IsTargetEliminated(world, _retainedTargetId))
            BeginPostKill(world, $"target eliminated={_retainedTargetId}");

        switch (_phase)
        {
            case CombatPhase.Idle:
                await HandleIdleAsync(world, sender, profile, ct);
                return;
            case CombatPhase.SelectingTarget:
                await HandleSelectingTargetAsync(world, sender, profile, ct);
                return;
            case CombatPhase.Opening:
                await HandleOpeningAsync(world, sender, profile, ct);
                return;
            case CombatPhase.Engaging:
                await HandleEngagingAsync(world, sender, profile, ct);
                return;
            case CombatPhase.KillLoop:
                await HandleKillLoopAsync(world, sender, profile, ct);
                return;
            case CombatPhase.PostKill:
                await HandlePostKillAsync(world, sender, profile, ct);
                return;
            case CombatPhase.Looting:
                await HandleLootingAsync(world, sender, profile, ct);
                return;
            default:
                SetPhase(world, CombatPhase.Idle, "fallback");
                return;
        }
    }

    private async Task HandleIdleAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        var combat = profile.Combat;
        var me = world.Me;

        var target = PickTarget(world, combat);
        if (target == null)
        {
            int total = world.Npcs.Count;
            int attackable = world.Npcs.Values.Count(n => n.IsAttackable && !n.IsDead);
            Debug($"no target picked totalNpcs={total} attackableAlive={attackable} me=({me.X},{me.Y},{me.Z}) reasons={DescribeNoTarget(world, combat)}");
            world.LastEngagedTargetId = 0;
            return;
        }

        if (!me.AnchorSet)
        {
            me.AnchorX = me.X;
            me.AnchorY = me.Y;
            me.AnchorZ = me.Z;
            me.AnchorSet = true;
        }

        _retainedTargetId = target.ObjectId;
        world.LastEngagedTargetId = target.ObjectId;
        _pendingTargetId = target.ObjectId;
        me.PendingTargetId = target.ObjectId;
        _openingSkillDone = false;
        _postKillCancelledTarget = false;
        _postKillSweepDone = false;
        _lastTargetAction = DateTime.UtcNow;

        Debug($"picked target={target.ObjectId} npcId={target.NpcId} pos=({target.X},{target.Y},{target.Z}) dist={target.DistanceTo(me):F0}");
        await sender.SendAsync(BuildTargetPacket(target, combat, world), ct);
        SetPhase(world, CombatPhase.SelectingTarget, $"select target={target.ObjectId}");
    }

    private async Task HandleSelectingTargetAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        var combat = profile.Combat;
        var me = world.Me;
        if (!world.Npcs.TryGetValue(_retainedTargetId, out var target) || !IsValidTarget(world, _retainedTargetId, combat))
        {
            ResetCombatState(world, "target invalid during selection");
            return;
        }

        if (me.TargetId == _retainedTargetId)
        {
            me.PendingTargetId = 0;
            _pendingTargetId = 0;
            SetPhase(world, CombatPhase.Opening, $"target confirmed={_retainedTargetId}");
            return;
        }

        if (DateTime.UtcNow > _lastTargetAction.AddSeconds(1))
        {
            Debug($"reselect target={_retainedTargetId}");
            _lastTargetAction = DateTime.UtcNow;
            me.PendingTargetId = _retainedTargetId;
            await sender.SendAsync(BuildTargetPacket(target, combat, world), ct);
            return;
        }

        if (DateTime.UtcNow >= _phaseSince.AddMilliseconds(300))
        {
            me.TargetId = _retainedTargetId;
            me.PendingTargetId = 0;
            _pendingTargetId = 0;
            SetPhase(world, CombatPhase.Opening, $"selection timeout adopt target={_retainedTargetId}");
        }
    }

    private async Task HandleOpeningAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        var combat = profile.Combat;
        if (!IsValidTarget(world, _retainedTargetId, combat))
        {
            ResetCombatState(world, "opening target invalid");
            return;
        }

        if (await TrySweepCorpseAsync(world, sender, profile, ct))
            return;

        if (await TrySpoilAsync(world, sender, profile, ct))
            return;

        if (!_openingSkillDone && await TryCastSkillAsync(world, sender, profile, openingOnly: true, ct))
        {
            _openingSkillDone = true;
            return;
        }

        _openingSkillDone = true;
        SetPhase(world, CombatPhase.Engaging, $"opening done target={_retainedTargetId}");
    }

    private async Task HandleEngagingAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        var combat = profile.Combat;
        if (!world.Npcs.TryGetValue(_retainedTargetId, out var target) || !IsValidTarget(world, _retainedTargetId, combat))
        {
            ResetCombatState(world, "engage target invalid");
            return;
        }

        if (world.Me.TargetId != _retainedTargetId)
        {
            world.Me.TargetId = _retainedTargetId;
            await sender.SendAsync(BuildTargetPacket(target, combat, world), ct);
            _lastTargetAction = DateTime.UtcNow;
            return;
        }

        await SendAttackAsync(target, combat, sender, world, ct);
        _lastReattack = DateTime.UtcNow;
        SetPhase(world, CombatPhase.KillLoop, $"engage sent target={_retainedTargetId}");
    }

    private async Task HandleKillLoopAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        var combat = profile.Combat;
        if (IsTargetEliminated(world, _retainedTargetId))
        {
            BeginPostKill(world, $"kill-loop death target={_retainedTargetId}");
            return;
        }

        if (!world.Npcs.TryGetValue(_retainedTargetId, out var target) || !IsValidTarget(world, _retainedTargetId, combat))
        {
            ResetCombatState(world, "kill-loop target invalid");
            return;
        }

        if (world.PositionConfidence == PositionConfidence.Unknown && DateTime.UtcNow > _phaseSince.AddMilliseconds(500))
        {
            TraceOnce(ref _lastMovementStaleTrace, "position-confidence-low", $"target={_retainedTargetId} me=({world.Me.X},{world.Me.Y},{world.Me.Z})");
        }

        if ((!world.LastCombatProgressUtc.HasValue || world.LastCombatProgressUtc.Value < _phaseSince) &&
            DateTime.UtcNow > _phaseSince.AddSeconds(2))
        {
            TraceOnce(ref _lastEngageNoProgressTrace, "engage-no-progress", $"target={_retainedTargetId} phaseSince={_phaseSince:O}");
        }

        if (world.LastSelfMoveEvidenceUtc.HasValue &&
            DateTime.UtcNow > world.LastSelfMoveEvidenceUtc.Value.AddSeconds(2) &&
            DateTime.UtcNow > _phaseSince.AddSeconds(2))
        {
            TraceOnce(ref _lastMovementStaleTrace, "movement-stale", $"target={_retainedTargetId} lastSelfMove={world.LastSelfMoveEvidenceUtc.Value:O}");
        }

        if (world.Me.TargetId != _retainedTargetId && DateTime.UtcNow > _lastTargetAction.AddMilliseconds(500))
        {
            Debug($"retain target={_retainedTargetId}");
            world.Me.PendingTargetId = _retainedTargetId;
            _pendingTargetId = _retainedTargetId;
            _lastTargetAction = DateTime.UtcNow;
            await sender.SendAsync(BuildTargetPacket(target, combat, world), ct);
            return;
        }

        if (await TryCastSkillAsync(world, sender, profile, openingOnly: false, ct))
            return;


        if (DateTime.UtcNow > _lastReattack.AddMilliseconds(Math.Max(250, combat.ReattackIntervalMs)))
        {
            await SendAttackAsync(target, combat, sender, world, ct);
            _lastReattack = DateTime.UtcNow;
        }
    }

    private async Task HandlePostKillAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        var spoilCfg = profile.Spoil;
        if (!_postKillSweepDone &&
            spoilCfg.SweepEnabled &&
            spoilCfg.SweepSkillId != 0 &&
            _postKillTargetId != 0 &&
            world.SpoiledNpcs.TryGetValue(_postKillTargetId, out var spoil) &&
            spoil.Succeeded &&
            DateTime.UtcNow > _lastSweepTime.AddSeconds(2))
        {
            await sender.SendAsync(GamePackets.Action(_postKillTargetId, world.Me.X, world.Me.Y, world.Me.Z), ct);
            await Task.Delay(150, ct);
            await sender.SendAsync(BuildSkillPacket(spoilCfg.SweepSkillId, profile.Combat.CombatSkillPacket), ct);
            _lastSweepTargetId = _postKillTargetId;
            _lastSweepTime = DateTime.UtcNow;
            _postKillSweepDone = true;
            world.SpoiledNpcs.TryRemove(_postKillTargetId, out _);
            Debug($"sweep corpse target={_postKillTargetId}");
            return;
        }

        _postKillSweepDone = true;

        if (!_postKillCancelledTarget)
        {
            await sender.SendAsync(GamePackets.CancelTarget(), ct);
            world.Me.TargetId = 0;
            world.Me.PendingTargetId = 0;
            _pendingTargetId = 0;
            _postKillCancelledTarget = true;
            _phaseSince = DateTime.UtcNow;
            Debug($"cancel target after kill target={_postKillTargetId}");
            return;
        }

        if (DateTime.UtcNow < _phaseSince.AddMilliseconds(Math.Max(100, profile.Combat.PostKillSpawnWaitMs)))
            return;

        _lootWindowEndsAt = DateTime.UtcNow.AddMilliseconds(Math.Max(500, profile.Combat.PostKillLootWindowMs));
        _lootWindowEmptyPolls = 0;
        Trace("loot-window-opened", $"target={_postKillTargetId} until={_lootWindowEndsAt:O}");
        SetPhase(world, CombatPhase.Looting, $"loot window opened target={_postKillTargetId}");
    }

    private async Task HandleLootingAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        var item = PickLootItem(world, profile);
        if (item == null)
        {
            _lootWindowEmptyPolls++;
            if (_lootWindowEmptyPolls >= 3 || DateTime.UtcNow >= _lootWindowEndsAt)
            {
                FinishPostKill(world, "loot window closed");
            }
            return;
        }

        _lootWindowEmptyPolls = 0;
        if (DateTime.UtcNow < _lastPickup.AddMilliseconds(300))
            return;

        Debug($"pickup item={item.ObjectId} dropper={item.DropperObjectId} dist={item.DistanceTo(world.Me):F0}");
        item.PickupAttempts++;
        item.LastPickupAttemptUtc = DateTime.UtcNow;
        _collector?.MarkScenario("pickup-item", $"bot item={item.ObjectId} dist={item.DistanceTo(world.Me):F0}");

        if (profile.Combat.UseTargetEnter)
        {
            await sender.SendAsync(GamePackets.TargetEnter(item.ObjectId, world.Me.X, world.Me.Y, world.Me.Z), ct);
            await Task.Delay(100, ct);
            await sender.SendAsync(GamePackets.PickupItemShort(), ct);
        }
        else
        {
            await sender.SendAsync(GamePackets.PickupItem(item.ObjectId, item.X, item.Y, item.Z), ct);
        }

        _lastPickup = DateTime.UtcNow;
    }

    private async Task<bool> TrySpoilAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        var combat = profile.Combat;
        var spoilCfg = profile.Spoil;
        if (!spoilCfg.Enabled || spoilCfg.SkillId == 0 || _retainedTargetId == 0)
            return false;

        var spoil = world.SpoiledNpcs.GetOrAdd(_retainedTargetId, _ => new SpoilStatus());
        if (spoil.Succeeded || spoil.IsPendingConfirmation || spoil.Attempts >= spoilCfg.MaxAttempts)
            return false;

        spoil.Attempts++;
        spoil.LastCastTime = DateTime.UtcNow;
        await sender.SendAsync(BuildSkillPacket(spoilCfg.SkillId, combat.CombatSkillPacket), ct);
        _lastSkillUse = DateTime.UtcNow;
        Debug($"cast spoil target={_retainedTargetId} attempt={spoil.Attempts}");
        return true;
    }

    private async Task<bool> TrySweepCorpseAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        var spoilCfg = profile.Spoil;
        if (!spoilCfg.SweepEnabled || spoilCfg.SweepSkillId == 0)
            return false;

        foreach (var npc in world.Npcs.Values)
        {
            if (!npc.IsDead) continue;
            if (npc.ObjectId == _lastSweepTargetId) continue;
            if (!world.SpoiledNpcs.TryGetValue(npc.ObjectId, out var spoilSt) || !spoilSt.Succeeded) continue;
            if (DateTime.UtcNow < _lastSweepTime.AddSeconds(2)) continue;
            if (npc.DistanceTo(world.Me) > 400) continue;

            if (world.Me.TargetId != npc.ObjectId)
            {
                await sender.SendAsync(GamePackets.Action(npc.ObjectId, npc.X, npc.Y, npc.Z), ct);
                await Task.Delay(300, ct);
            }

            await sender.SendAsync(BuildSkillPacket(spoilCfg.SweepSkillId, profile.Combat.CombatSkillPacket), ct);
            _lastSweepTargetId = npc.ObjectId;
            _lastSweepTime = DateTime.UtcNow;
            world.SpoiledNpcs.TryRemove(npc.ObjectId, out _);
            Debug($"pre-combat sweep corpse={npc.ObjectId}");
            return true;
        }

        return false;
    }

    private async Task<bool> TryCastSkillAsync(GameWorld world, PacketSender sender, CharacterProfile profile, bool openingOnly, CancellationToken ct)
    {
        var combat = profile.Combat;
        if (combat.SkillRotation.Count == 0)
            return false;

        if (!openingOnly && DateTime.UtcNow <= _lastSkillUse.AddMilliseconds(combat.PostSkillDelayMs))
            return false;

        int attempts = combat.SkillRotation.Count;
        while (attempts-- > 0)
        {
            var entry = combat.SkillRotation[_currentSkillIndex % combat.SkillRotation.Count];
            _currentSkillIndex++;

            if (!entry.Enabled || entry.SkillId == 0) continue;
            if (world.Me.MpPct < entry.MinMpPct) continue;
            if (world.Skills.TryGetValue(entry.SkillId, out var skillInfo) && !skillInfo.IsReady) continue;

            await sender.SendAsync(BuildSkillPacket(entry.SkillId, combat.CombatSkillPacket), ct);
            _lastSkillUse = DateTime.UtcNow;
            Debug($"{(openingOnly ? "opening" : "combat")} skill={entry.SkillId} target={_retainedTargetId}");
            return true;
        }

        return false;
    }

    private async Task SendAttackAsync(Npc target, CombatConfig combat, PacketSender sender, GameWorld world, CancellationToken ct)
    {
        var packet = combat.UseTargetEnter
            ? GamePackets.TargetEnter(target.ObjectId, world.Me.X, world.Me.Y, world.Me.Z)
            : GamePackets.ForceAttack();
        await sender.SendAsync(packet, ct);
        _lastAttack = DateTime.UtcNow;
        world.LastEngagedTargetId = target.ObjectId;
        world.Me.TargetId = target.ObjectId;
        world.Me.PendingTargetId = 0;
        _pendingTargetId = 0;
        Debug($"attack sent target={target.ObjectId} pos=({target.X},{target.Y},{target.Z}) dist={target.DistanceTo(world.Me):F0}");
    }

    private (byte opcode, byte[] payload) BuildTargetPacket(Npc target, CombatConfig combat, GameWorld world) =>
        combat.UseTargetEnter
            ? GamePackets.TargetEnter(target.ObjectId, world.Me.X, world.Me.Y, world.Me.Z)
            : GamePackets.Action(target.ObjectId, target.X, target.Y, target.Z);

    private void ClearPendingTargetIfNeeded(GameWorld world, CombatConfig combat)
    {
        var me = world.Me;
        if (me.PendingTargetId == 0)
            return;

        bool pendingMatchesCurrent = me.TargetId == me.PendingTargetId && IsValidTarget(world, me.TargetId, combat);
        bool pendingExpired = DateTime.UtcNow > _lastTargetAction.AddSeconds(2);
        bool pendingMissing = !world.Npcs.ContainsKey(me.PendingTargetId);
        if (pendingMatchesCurrent || pendingExpired || pendingMissing)
        {
            Debug($"clearing pending target={me.PendingTargetId} confirmed={pendingMatchesCurrent} expired={pendingExpired} missing={pendingMissing}");
            me.PendingTargetId = 0;
            _pendingTargetId = 0;
        }
    }

    private Npc? PickTarget(GameWorld world, CombatConfig combat)
    {
        var me = world.Me;

        if (_retainedTargetId != 0 &&
            world.Npcs.TryGetValue(_retainedTargetId, out var retained) &&
            IsValidTarget(world, retained.ObjectId, combat) &&
            retained.DistanceTo(me) <= Math.Max(100, combat.RetainTargetMaxDist))
        {
            Trace("target-retained", $"target={retained.ObjectId} dist={retained.DistanceTo(me):F0}");
            return retained;
        }

        var candidates = new List<(Npc Npc, double Dist)>();

        foreach (var npc in world.Npcs.Values)
        {
            if (!npc.IsAttackable || IsNpcEliminated(npc)) continue;
            if (npc.ZDelta(me) > combat.ZHeightLimit) continue;

            double dist = npc.DistanceTo(me);
            if (dist > combat.AggroRadius) continue;

            if (combat.TargetNpcIds.Count > 0 && !combat.TargetNpcIds.Contains(npc.NpcId))
                continue;

            if (me.AnchorSet && combat.AnchorLeash > 0)
            {
                double anchorDist = Math.Sqrt(
                    Math.Pow(npc.X - me.AnchorX, 2) +
                    Math.Pow(npc.Y - me.AnchorY, 2));
                if (anchorDist > combat.AnchorLeash) continue;
            }

            candidates.Add((npc, dist));
        }

        if (candidates.Count == 0)
            return null;

        double localRadius = Math.Min(combat.AggroRadius, Math.Max(450, combat.RetainTargetMaxDist * 2.0));
        var pool = candidates.Where(c => c.Dist <= localRadius).ToList();
        if (pool.Count == 0)
            pool = candidates;

        if (combat.PreferAggroTargets)
        {
            var aggroPool = pool
                .Where(c => c.Npc.WasAttackingMeRecent(TimeSpan.FromSeconds(18)))
                .ToList();
            if (aggroPool.Count > 0)
                pool = aggroPool;
        }

        return SelectBestCandidate(pool, combat.TargetPriority);
    }

    private static Npc SelectBestCandidate(List<(Npc Npc, double Dist)> candidates, string targetPriority)
    {
        var best = candidates[0];
        for (int i = 1; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (string.Equals(targetPriority, "lowest_hp", StringComparison.OrdinalIgnoreCase))
            {
                if (candidate.Npc.HpPercent < best.Npc.HpPercent ||
                    (candidate.Npc.HpPercent == best.Npc.HpPercent && candidate.Dist < best.Dist))
                {
                    best = candidate;
                }
            }
            else if (candidate.Dist < best.Dist ||
                     (Math.Abs(candidate.Dist - best.Dist) < 0.001 && candidate.Npc.HpPercent < best.Npc.HpPercent))
            {
                best = candidate;
            }
        }

        return best.Npc;
    }

    private static string DescribeNoTarget(GameWorld world, CombatConfig combat)
    {
        var me = world.Me;
        int eliminated = 0;
        int zRejected = 0;
        int distRejected = 0;
        int npcIdRejected = 0;
        int anchorRejected = 0;
        int passed = 0;

        foreach (var npc in world.Npcs.Values)
        {
            if (!npc.IsAttackable || IsNpcEliminated(npc))
            {
                eliminated++;
                continue;
            }

            if (npc.ZDelta(me) > combat.ZHeightLimit)
            {
                zRejected++;
                continue;
            }

            double dist = npc.DistanceTo(me);
            if (dist > combat.AggroRadius)
            {
                distRejected++;
                continue;
            }

            if (combat.TargetNpcIds.Count > 0 && !combat.TargetNpcIds.Contains(npc.NpcId))
            {
                npcIdRejected++;
                continue;
            }

            if (me.AnchorSet && combat.AnchorLeash > 0)
            {
                double anchorDist = Math.Sqrt(
                    Math.Pow(npc.X - me.AnchorX, 2) +
                    Math.Pow(npc.Y - me.AnchorY, 2));
                if (anchorDist > combat.AnchorLeash)
                {
                    anchorRejected++;
                    continue;
                }
            }

            passed++;
        }

        return $"passed={passed},deadOrNonAttackable={eliminated},z={zRejected},dist={distRejected},npcId={npcIdRejected},anchor={anchorRejected}";
    }

    private GroundItem? PickLootItem(GameWorld world, CharacterProfile profile)
    {
        var me = world.Me;

        var dropperMatches = world.Items.Values
            .Where(item => item.PickupAttempts < MaxPickupAttemptsPerItem)
            .Where(item => item.DropperObjectId == _postKillTargetId)
            .OrderBy(item => item.PickupAttempts)
            .ThenBy(item => item.SpawnedAtUtc)
            .ToList();
        if (dropperMatches.Count > 0)
            return dropperMatches[0];

        var candidates = world.Items.Values
            .Where(item => item.PickupAttempts < MaxPickupAttemptsPerItem)
            .Where(item => item.DistanceTo(me) <= profile.Loot.Radius)
            .ToList();

        if (candidates.Count == 0)
            return null;

        var recent = candidates
            .Where(item => item.SpawnedAtUtc >= _phaseSince.AddMilliseconds(-250))
            .OrderBy(item => item.PickupAttempts)
            .ThenBy(item => item.DistanceTo(me))
            .ToList();
        if (recent.Count > 0)
            return recent[0];

        return candidates
            .OrderBy(item => item.PickupAttempts)
            .ThenBy(item => item.DistanceTo(me))
            .FirstOrDefault();
    }

    private bool IsValidTarget(GameWorld world, int targetId, CombatConfig combat)
    {
        if (!world.Npcs.TryGetValue(targetId, out var npc)) return false;
        if (!npc.IsAttackable) return false;
        if (IsNpcEliminated(npc)) return false;
        if (npc.ZDelta(world.Me) > combat.ZHeightLimit) return false;
        return npc.DistanceTo(world.Me) <= Math.Max(combat.AggroRadius * 1.5, combat.RetainTargetMaxDist);
    }

    private bool IsTargetEliminated(GameWorld world, int targetId)
    {
        if (targetId == 0)
            return true;

        if (!world.Npcs.TryGetValue(targetId, out var npc))
        {
            Trace("death-confirm-source", $"target={targetId} source=missing-from-world");
            return true;
        }

        if (IsNpcEliminated(npc))
        {
            string source = npc.LastDropEvidenceUtc.HasValue ? "drop-evidence"
                : npc.LastDeathEvidenceUtc.HasValue ? "death-evidence"
                : "dead-flag";
            Trace("death-confirm-source", $"target={targetId} source={source}");
            return true;
        }

        return false;
    }

    private static bool IsNpcEliminated(Npc npc) =>
        npc.IsDead ||
        npc.LastDeathEvidenceUtc.HasValue ||
        npc.LastDropEvidenceUtc.HasValue ||
        (npc.MaxHp > 0 && npc.CurHp <= 0);

    private void BeginPostKill(GameWorld world, string reason)
    {
        _postKillTargetId = _retainedTargetId != 0 ? _retainedTargetId : world.LastEngagedTargetId;
        _retainedTargetId = 0;
        world.LastEngagedTargetId = 0;
        _postKillCancelledTarget = false;
        _postKillSweepDone = false;
        _lootWindowEndsAt = DateTime.MinValue;
        _lootWindowEmptyPolls = 0;
        SetPhase(world, CombatPhase.PostKill, reason);
    }

    private void FinishPostKill(GameWorld world, string reason)
    {
        Trace("loot-window-closed", $"target={_postKillTargetId} reason={reason}");
        _postKillTargetId = 0;
        _postKillCancelledTarget = false;
        _postKillSweepDone = false;
        _lootWindowEndsAt = DateTime.MinValue;
        _lootWindowEmptyPolls = 0;
        world.Me.TargetId = 0;
        world.Me.PendingTargetId = 0;
        Trace("target-cleared", $"reason={reason}");
        world.LastEngagedTargetId = 0;
        SetPhase(world, CombatPhase.Idle, reason);
    }

    private void ResetCombatState(GameWorld world, string reason)
    {
        _retainedTargetId = 0;
        _postKillTargetId = 0;
        _pendingTargetId = 0;
        _postKillCancelledTarget = false;
        _postKillSweepDone = false;
        _lootWindowEndsAt = DateTime.MinValue;
        _lootWindowEmptyPolls = 0;
        world.Me.TargetId = 0;
        world.Me.PendingTargetId = 0;
        Trace("target-cleared", $"reason={reason}");
        world.LastEngagedTargetId = 0;
        SetPhase(world, CombatPhase.Idle, reason);
    }

    private void SetPhase(GameWorld world, CombatPhase phase, string reason)
    {
        if (_phase == phase)
            return;

        var previous = _phase;
        _phase = phase;
        _phaseSince = DateTime.UtcNow;
        world.SetCombatPhase(phase);
        _collector?.RecordBehavior("phase-transition", $"{previous}->{phase} :: {reason}");
        _logger?.LogInformation("[CombatPhase] {Previous}->{Phase} {Reason}", previous, phase, reason);
    }

    private void Trace(string label, string details)
    {
        _collector?.RecordBehavior(label, details);
    }

    private void TraceOnce(ref DateTime gate, string label, string details)
    {
        if (DateTime.UtcNow < gate.AddSeconds(2))
            return;

        gate = DateTime.UtcNow;
        Trace(label, details);
    }

    private static (byte opcode, byte[] payload) BuildSkillPacket(int skillId, string packetType) =>
        (packetType ?? "2f").ToLower() switch
        {
            "2f" => GamePackets.ShortcutSkillUse(skillId),
            "39dcb" or "dcb" => GamePackets.UseSkill(skillId, "dcb"),
            "39dcc" or "dcc" => GamePackets.UseSkill(skillId, "dcc"),
            _ => GamePackets.UseSkill(skillId, "ddd")
        };

    private void Debug(string message)
    {
        if (DateTime.UtcNow < _lastDebug.AddSeconds(2)) return;
        _lastDebug = DateTime.UtcNow;
        _collector?.RecordBehavior("AutoCombat", message);
        _logger?.LogInformation("[AutoCombat] {Message}", message);
    }
}



















