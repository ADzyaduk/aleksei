namespace Alexei.Core.GameState;

public sealed class GroundItem
{
    public int ObjectId { get; set; }
    public int ItemId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public long Count { get; set; }
    public int? DropperObjectId { get; set; }
    public DateTime SpawnedAtUtc { get; set; } = DateTime.UtcNow;
    public int PickupAttempts { get; set; }
    public DateTime? LastPickupAttemptUtc { get; set; }

    public double DistanceTo(MyCharacter me)
    {
        double dx = X - me.X;
        double dy = Y - me.Y;
        double dz = Z - me.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
