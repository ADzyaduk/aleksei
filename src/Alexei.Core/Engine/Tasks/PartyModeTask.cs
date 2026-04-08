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

    public PartyModeTask(PacketEvidenceCollector? collector = null)
    {
        _collector = collector;
    }

    public async Task ExecuteAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        var party = profile.Party;
        if (!party.Enabled || party.Mode == PartyMode.None)
            return;

        if (!IsSelfPositionKnown(world))
        {
            Trace("self-position-unknown", $"skip mode={party.Mode} reason=self-position-unknown");
            return;
        }

        var actor = ResolveActor(world, party, _lastResolvedActorId);
        if (actor == null)
        {
            Trace(
                $"actor-unresolved:{party.Mode}",
                $"skip mode={party.Mode} reason=actor-unresolved leader={party.LeaderName} assist={party.AssistName} partyLeaderId={world.PartyLeaderObjectId} members={world.Party.Count}");
            return;
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
                return;
            }
        }

        double distance = actor.DistanceTo(world.Me.X, world.Me.Y, world.Me.Z);
        double followDistance = Math.Max(0, party.FollowDistance);
        if (distance > followDistance)
        {
            await FollowActorAsync(actor, world, sender, party, ct);
            Trace($"move:{actor.ObjectId}", $"move actor={DescribeActor(actor)} distance={distance:F1} follow={followDistance:F1} repath={party.RepathDistance:F1}");
        }
        else
        {
            Trace($"hold:{actor.ObjectId}", $"hold actor={DescribeActor(actor)} distance={distance:F1} follow={followDistance:F1} repath={party.RepathDistance:F1}");
        }

        if (party.Mode != PartyMode.Assist)
            return;

        if (actor.TargetId == 0)
        {
            Trace($"assist-no-target:{actor.ObjectId}", $"skip assist actor={DescribeActor(actor)} reason=no-target");
            return;
        }

        if (!world.Npcs.TryGetValue(actor.TargetId, out var npc) || !npc.IsAttackable || npc.IsDead)
        {
            Trace($"assist-invalid:{actor.ObjectId}:{actor.TargetId}", $"skip assist actor={DescribeActor(actor)} target={actor.TargetId} reason=invalid-target");
            return;
        }

        if (world.Me.TargetId != npc.ObjectId)
        {
            await sender.SendAsync(GamePackets.TargetEnter(npc.ObjectId, world.Me.X, world.Me.Y, world.Me.Z), ct);
            world.Me.TargetId = npc.ObjectId;
            world.Me.PendingTargetId = 0;
        }

        await sender.SendAsync(GamePackets.ForceAttack(), ct);
        Trace($"assist-attack:{npc.ObjectId}", $"attack assistTarget={npc.ObjectId} actor={DescribeActor(actor)}");
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

    private static async Task FollowActorAsync(PartyMember actor, GameWorld world, PacketSender sender, PartyConfig party, CancellationToken ct)
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

        await sender.SendAsync(GamePackets.Move(destX, destY, destZ, world.Me.X, world.Me.Y, world.Me.Z), ct);
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



