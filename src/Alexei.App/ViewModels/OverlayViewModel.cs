using Alexei.App.Infrastructure;
using Alexei.Core.GameState;

namespace Alexei.App.ViewModels;

public sealed class OverlayViewModel : ViewModelBase
{
    private double _hpPct; public double HpPct { get => _hpPct; set => SetField(ref _hpPct, value); }
    private double _mpPct; public double MpPct { get => _mpPct; set => SetField(ref _mpPct, value); }
    private double _cpPct; public double CpPct { get => _cpPct; set => SetField(ref _cpPct, value); }
    private string _charName = ""; public string CharName { get => _charName; set => SetField(ref _charName, value); }
    private string _targetInfo = "No target"; public string TargetInfo { get => _targetInfo; set => SetField(ref _targetInfo, value); }
    private string _botStatus = "Idle"; public string BotStatus { get => _botStatus; set => SetField(ref _botStatus, value); }
    private bool _isVisible; public bool IsVisible { get => _isVisible; set => SetField(ref _isVisible, value); }

    public void UpdateFromWorld(GameWorld world)
    {
        var me = world.Me;
        HpPct = me.HpPct;
        MpPct = me.MpPct;
        CpPct = me.CpPct;
        CharName = $"{me.Name} Lv.{me.Level}";

        if (me.TargetId != 0 && world.Npcs.TryGetValue(me.TargetId, out var npc))
            TargetInfo = $"{npc.Name} HP:{npc.HpPercent}%";
        else
            TargetInfo = "No target";
    }
}
