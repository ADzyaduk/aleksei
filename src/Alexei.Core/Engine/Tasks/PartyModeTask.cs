using Alexei.Core.Config;
using Alexei.Core.Diagnostics;
using Alexei.Core.GameState;
using Alexei.Core.Protocol;
using Alexei.Core.Proxy;

namespace Alexei.Core.Engine.Tasks;

public sealed class PartyModeTask : IBotTask
{
    public string Name => "PartyMode";
    public bool IsEnabled => true;

    private readonly PacketEvidenceCollector? _collector;
    private string? _lastTraceKey;
    private DateTime _lastTraceUtc = DateTime.MinValue;
    private int _lastResolvedActorId;
    private DateTime _lastMoveTime = DateTime.MinValue;
    private int _lastMoveDestX;
    private int _lastMoveDestY;
    private int _lastMoveDestZ;

    public void ResetState(GameWorld world)
    {
        _lastTraceKey = null;
        _lastTraceUtc = DateTime.MinValue;
        _lastResolvedActorId = 0;
        _lastMoveTime = DateTime.MinValue;
        _lastMoveDestX = 0;
        _lastMoveDestY = 0;
        _lastMoveDestZ = 0;
    }

    public PartyModeTask(PacketEvidenceCollector? collector = null)
    {
        _collector = collector;
    }

    public async Task<bool> ExecuteAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        if (DateTime.UtcNow < world.ActionLockUntilUtc)
            return false;

        var party = profile.Party;
        if (!party.Enabled || party.Mode == PartyMode.None)
            return false;

        if (!IsSelfPositionKnown(world))
        {
            Trace("self-position-unknown", $"skip mode={party.Mode} reason=self-position-unknown");
            return false;
        }

        var actor = ResolveActor(world, party, _lastResolvedActorId);
        if (actor == null)
        {
            Trace(
                $"actor-unresolved:{party.Mode}",
                $"skip mode={party.Mode} reason=actor-unresolved leader={party.LeaderName} assist={party.AssistName} partyLeaderId={world.PartyLeaderObjectId} members={world.Party.Count}");
            return false;
        }

        _lastResolvedActorId = actor.ObjectId;

        bool isFresh = IsFresh(actor, party.PositionTimeoutMs);
        if (!isFresh)
        {
            if (party.Mode == PartyMode.Follow && IsFollowPositionUsable(actor, party.PositionTimeoutMs))
            {
                Trace($"actor-stale-soft:{actor.ObjectId}", $"follow mode using last-known-position actor={DescribeActor(actor)} timeoutMs={party.PositionTimeoutMs}");
            }
            else
            {
                Trace($"actor-stale:{actor.ObjectId}", $"skip mode={party.Mode} actor={DescribeActor(actor)} reason=stale-position");
                return false;
            }
        }

        double distance = actor.DistanceTo(world.Me.X, world.Me.Y, world.Me.Z);
        double followDistance = Math.Max(0, party.FollowDistance);
        bool followed = false;
        if (distance > followDistance)
        {
            followed = await FollowActorAsync(actor, world, sender, party, ct);
            Trace($"move:{actor.ObjectId}", $"move actor={DescribeActor(actor)} distance={distance:F1} follow={followDistance:F1} repath={party.RepathDistance:F1}");
        }
        else
        {
            Trace($"hold:{actor.ObjectId}", $"hold actor={DescribeActor(actor)} distance={distance:F1} follow={followDistance:F1} repath={party.RepathDistance:F1}");
        }

        if (party.Mode != PartyMode.Assist)
            return followed;

        if (actor.TargetId == 0)
        {
            Trace($"assist-no-target:{actor.ObjectId}", $"skip assist actor={DescribeActor(actor)} reason=no-target");
            return followed;
        }

        if (!world.Npcs.TryGetValue(actor.TargetId, out var npc) || !npc.IsAttackable || npc.IsDead)
        {
            Trace($"assist-invalid:{actor.ObjectId}:{actor.TargetId}", $"skip assist actor={DescribeActor(actor)} target={actor.TargetId} reason=invalid-target");
            return followed;
        }

        if (world.Me.TargetId != npc.ObjectId)
        {
            await sender.SendAsync(GamePackets.TargetEnter(npc.ObjectId, world.Me.X, world.Me.Y, world.Me.Z), ct);
            world.Me.TargetId = npc.ObjectId;
            world.Me.PendingTargetId = 0;
            world.ActionLockUntilUtc = DateTime.UtcNow.AddMilliseconds(200);
            return true;
        }

        await sender.SendAsync(GamePackets.ForceAttack(), ct);
        Trace($"assist-attack:{npc.ObjectId}", $"attack assistTarget={npc.ObjectId} actor={DescribeActor(actor)}");
        world.ActionLockUntilUtc = DateTime.UtcNow.AddMilliseconds(1500);
        return true;
    }

    private static PartyMember? ResolveActor(GameWorld world, PartyConfig party, int lastResolvedActorId)
    {
        if (party.Mode == PartyMode.Follow)
        {
            return PartyMemberResolver.ResolveConfiguredMember(world, party.LeaderName)
                   ?? PartyMemberResolver.ResolveLeaderByObjectId(world)
                   ?? ResolveLastResolved(world, lastResolvedActorId)
                   ?? PartyMemberResolver.ResolveSoleMember(world);
        }

        return PartyMemberResolver.ResolveConfiguredMember(world, party.AssistName)
               ?? PartyMemberResolver.ResolveConfiguredMember(world, party.LeaderName)
               ?? PartyMemberResolver.ResolveLeaderByObjectId(world)
               ?? ResolveLastResolved(world, lastResolvedActorId)
               ?? PartyMemberResolver.ResolveSoleMember(world);
    }

    private static PartyMember? ResolveLastResolved(GameWorld world, int lastResolvedActorId)
    {
        return PartyMemberResolver.TryGetKnownActorById(world, lastResolvedActorId, out var lastResolved)
            ? lastResolved
            : null;
    }

    private static bool IsSelfPositionKnown(GameWorld world)
    {
        if (world.PositionConfidence != PositionConfidence.Unknown)
            return true;

        return world.Me.X != 0 || world.Me.Y != 0 || world.Me.Z != 0;
    }

    private static bool IsFresh(PartyMember member, int timeoutMs)
    {
        if (member.LastPositionUpdateUtc == DateTime.MinValue)
            return false;

        int effectiveTimeoutMs = timeoutMs > 0 ? timeoutMs : 2000;
        return DateTime.UtcNow <= member.LastPositionUpdateUtc.AddMilliseconds(effectiveTimeoutMs);
    }

    private static bool IsFollowPositionUsable(PartyMember member, int timeoutMs)
    {
        if (member.LastPositionUpdateUtc == DateTime.MinValue)
            return false;

        int effectiveTimeoutMs = Math.Max(timeoutMs > 0 ? timeoutMs : 2000, 10000);
        return DateTime.UtcNow <= member.LastPositionUpdateUtc.AddMilliseconds(effectiveTimeoutMs);
    }

    private async Task<bool> FollowActorAsync(PartyMember actor, GameWorld world, PacketSender sender, PartyConfig party, CancellationToken ct)
    {
        double distance = actor.DistanceTo(world.Me.X, world.Me.Y, world.Me.Z);
        double followDistance = Math.Max(0, party.FollowDistance);
        double dx = actor.X - world.Me.X;
        double dy = actor.Y - world.Me.Y;
        double dz = actor.Z - world.Me.Z;
        double safeDistance = Math.Max(distance, 1d);
        double desiredTravel = Math.Max(0d, distance - followDistance);

        int destX = world.Me.X + (int)Math.Round(dx / safeDistance * desiredTravel);
        int destY = world.Me.Y + (int)Math.Round(dy / safeDistance * desiredTravel);
        int destZ = world.Me.Z + (int)Math.Round(dz / safeDistance * desiredTravel);

        double repathDistance = party.RepathDistance > 0 ? party.RepathDistance : 150d;
        double dxDest = destX - _lastMoveDestX;
        double dyDest = destY - _lastMoveDestY;
        double dzDest = destZ - _lastMoveDestZ;
        double distFromLastDest = Math.Sqrt(dxDest * dxDest + dyDest * dyDest + dzDest * dzDest);

        if (DateTime.UtcNow < _lastMoveTime.AddSeconds(1) && distFromLastDest < repathDistance)
        {
            return false;
        }

        _lastMoveTime = DateTime.UtcNow;
        _lastMoveDestX = destX;
        _lastMoveDestY = destY;
        _lastMoveDestZ = destZ;

        await sender.SendAsync(GamePackets.Move(destX, destY, destZ, world.Me.X, world.Me.Y, world.Me.Z), ct);
        return true;
    }

    private static string DescribeActor(PartyMember actor) => PartyMemberResolver.DescribeMember(actor);

    private void Trace(string key, string message)
    {
        var now = DateTime.UtcNow;
        if (string.Equals(_lastTraceKey, key, StringComparison.Ordinal) && now - _lastTraceUtc < TimeSpan.FromSeconds(1))
            return;

        _lastTraceKey = key;
        _lastTraceUtc = now;
        _collector?.RecordBehavior("PartyMode", message);
    }
}



