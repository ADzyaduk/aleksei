using System.Collections.ObjectModel;
using Alexei.App.Infrastructure;
using Alexei.Core.GameState;

namespace Alexei.App.ViewModels;

public sealed class MonsterInfo : ViewModelBase
{
    public int ObjectId { get; set; }
    public string Name { get; set; } = "";
    public int NpcId { get; set; }
    public double Distance { get; set; }
    public int HpPercent { get; set; }
    public bool IsAttackable { get; set; }
    public bool IsDead { get; set; }
}

public sealed class MonsterTabVM : ViewModelBase
{
    public ObservableCollection<MonsterInfo> Monsters { get; } = new();

    private int _totalCount;
    public int TotalCount { get => _totalCount; set => SetField(ref _totalCount, value); }

    public void UpdateFromWorld(GameWorld world)
    {
        var me = world.Me;
        Monsters.Clear();

        var sorted = world.Npcs.Values
            .OrderBy(n => n.DistanceTo(me))
            .Take(50);

        foreach (var npc in sorted)
        {
            Monsters.Add(new MonsterInfo
            {
                ObjectId = npc.ObjectId,
                Name = npc.Name,
                NpcId = npc.NpcId,
                Distance = Math.Round(npc.DistanceTo(me)),
                HpPercent = npc.HpPercent,
                IsAttackable = npc.IsAttackable,
                IsDead = npc.IsDead
            });
        }

        TotalCount = world.Npcs.Count;
    }
}
