using Alexei.Core.Config;
using Alexei.Core.Diagnostics;
using Alexei.Core.GameState;
using Alexei.Core.Protocol;
using Alexei.Core.Proxy;

namespace Alexei.Core.Engine.Tasks;

public sealed class AutoLootTask : IBotTask
{
    private const int MaxPickupAttemptsPerItem = 3;
    public string Name => "AutoLoot";
    public bool IsEnabled => true;

    private readonly PacketEvidenceCollector? _collector;
    private DateTime _lastPickup = DateTime.MinValue;
    private DateTime _lastDebug = DateTime.MinValue;

    public AutoLootTask(PacketEvidenceCollector? collector = null)
    {
        _collector = collector;
    }

    public void ResetState(GameWorld world)
    {
        _lastPickup = DateTime.MinValue;
        _lastDebug = DateTime.MinValue;
    }

    public async Task<bool> ExecuteAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        if (DateTime.UtcNow < world.ActionLockUntilUtc)
            return false;

        if (!profile.Loot.Enabled) return false;
        if (world.Me.IsDead || world.Me.IsSitting) return false;
        if (DateTime.UtcNow < _lastPickup.AddMilliseconds(300)) return false;

        if (profile.Combat.Enabled)
        {
            if (world.CurrentCombatPhase != CombatPhase.Idle)
            {
                Debug($"loot skipped because phase={world.CurrentCombatPhase}");
                return false;
            }

            if (world.Me.PendingTargetId != 0)
            {
                Debug($"loot skipped because pending target={world.Me.PendingTargetId}");
                return false;
            }

            if (world.Me.TargetId != 0 &&
                world.Npcs.TryGetValue(world.Me.TargetId, out var currentTarget) &&
                currentTarget.IsAttackable &&
                !currentTarget.IsDead)
            {
                Debug($"loot skipped because target alive target={world.Me.TargetId}");
                return false;
            }
        }

        GroundItem? nearest = null;
        double nearestDist = double.MaxValue;

        foreach (var item in world.Items.Values)
        {
            if (item.PickupAttempts >= MaxPickupAttemptsPerItem) continue;
            double dist = item.DistanceTo(world.Me);
            if (dist > profile.Loot.Radius) continue;
            if (dist < nearestDist)
            {
                nearest = item;
                nearestDist = dist;
            }
        }

        if (nearest == null) return false;

        Debug($"pickup item={nearest.ObjectId} dist={nearestDist:F0}");
        _collector?.MarkScenario("pickup-item", $"bot item={nearest.ObjectId} dist={nearestDist:F0}");

        if (profile.Combat.UseTargetEnter)
        {
            if (world.Me.TargetId != nearest.ObjectId)
            {
                await sender.SendAsync(GamePackets.TargetEnter(nearest.ObjectId, world.Me.X, world.Me.Y, world.Me.Z));
                world.Me.TargetId = nearest.ObjectId;
                world.Me.PendingTargetId = 0;
                world.ActionLockUntilUtc = DateTime.UtcNow.AddMilliseconds(200);
                return true;
            }
            await sender.SendAsync(GamePackets.PickupItemShort());
        }
        else
        {
            await sender.SendAsync(GamePackets.PickupItem(nearest.ObjectId, nearest.X, nearest.Y, nearest.Z));
        }
        
        _lastPickup = DateTime.UtcNow;
        nearest.PickupAttempts++;
        nearest.LastPickupAttemptUtc = DateTime.UtcNow;
        world.ActionLockUntilUtc = DateTime.UtcNow.AddMilliseconds(500);
        return true;
    }

    private void Debug(string message)
    {
        if (DateTime.UtcNow < _lastDebug.AddSeconds(2)) return;
        _lastDebug = DateTime.UtcNow;
        _collector?.RecordBehavior("AutoLoot", message);
    }
}


