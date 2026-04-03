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

    public async Task ExecuteAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        if (!profile.Loot.Enabled) return;
        if (world.Me.IsDead || world.Me.IsSitting) return;
        if (DateTime.UtcNow < _lastPickup.AddMilliseconds(300)) return;

        if (profile.Combat.Enabled)
        {
            if (world.CurrentCombatPhase != CombatPhase.Idle)
            {
                Debug($"loot skipped because phase={world.CurrentCombatPhase}");
                return;
            }

            if (world.Me.PendingTargetId != 0)
            {
                Debug($"loot skipped because pending target={world.Me.PendingTargetId}");
                return;
            }

            if (world.Me.TargetId != 0 &&
                world.Npcs.TryGetValue(world.Me.TargetId, out var currentTarget) &&
                currentTarget.IsAttackable &&
                !currentTarget.IsDead)
            {
                Debug($"loot skipped because target alive target={world.Me.TargetId}");
                return;
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

        if (nearest == null) return;

        Debug($"pickup item={nearest.ObjectId} dist={nearestDist:F0}");
        _collector?.MarkScenario("pickup-item", $"bot item={nearest.ObjectId} dist={nearestDist:F0}");

        if (profile.Combat.UseTargetEnter)
        {
            await sender.SendAsync(GamePackets.TargetEnter(nearest.ObjectId, world.Me.X, world.Me.Y, world.Me.Z));
            await Task.Delay(100, ct);
            await sender.SendAsync(GamePackets.PickupItemShort());
        }
        else
        {
            await sender.SendAsync(GamePackets.PickupItem(nearest.ObjectId, nearest.X, nearest.Y, nearest.Z));
        }
        _lastPickup = DateTime.UtcNow;
        nearest.PickupAttempts++;
        nearest.LastPickupAttemptUtc = DateTime.UtcNow;
    }

    private void Debug(string message)
    {
        if (DateTime.UtcNow < _lastDebug.AddSeconds(2)) return;
        _lastDebug = DateTime.UtcNow;
        _collector?.RecordBehavior("AutoLoot", message);
    }
}


