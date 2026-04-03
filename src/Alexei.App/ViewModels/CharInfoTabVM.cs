using Alexei.App.Infrastructure;
using Alexei.Core.GameState;

namespace Alexei.App.ViewModels;

public sealed class CharInfoTabVM : ViewModelBase
{
    private string _name = ""; public string Name { get => _name; set => SetField(ref _name, value); }
    private int _level; public int Level { get => _level; set => SetField(ref _level, value); }
    private int _curHp; public int CurHp { get => _curHp; set => SetField(ref _curHp, value); }
    private int _maxHp; public int MaxHp { get => _maxHp; set => SetField(ref _maxHp, value); }
    private int _curMp; public int CurMp { get => _curMp; set => SetField(ref _curMp, value); }
    private int _maxMp; public int MaxMp { get => _maxMp; set => SetField(ref _maxMp, value); }
    private int _curCp; public int CurCp { get => _curCp; set => SetField(ref _curCp, value); }
    private int _maxCp; public int MaxCp { get => _maxCp; set => SetField(ref _maxCp, value); }
    private double _hpPct; public double HpPct { get => _hpPct; set => SetField(ref _hpPct, value); }
    private double _mpPct; public double MpPct { get => _mpPct; set => SetField(ref _mpPct, value); }
    private double _cpPct; public double CpPct { get => _cpPct; set => SetField(ref _cpPct, value); }
    private string _position = ""; public string Position { get => _position; set => SetField(ref _position, value); }
    private int _targetId; public int TargetId { get => _targetId; set => SetField(ref _targetId, value); }
    private bool _isSitting; public bool IsSitting { get => _isSitting; set => SetField(ref _isSitting, value); }
    private long _exp; public long Exp { get => _exp; set => SetField(ref _exp, value); }

    public void UpdateFromWorld(GameWorld world)
    {
        var me = world.Me;
        Name = me.Name;
        Level = me.Level;
        CurHp = me.CurHp; MaxHp = me.MaxHp;
        CurMp = me.CurMp; MaxMp = me.MaxMp;
        CurCp = me.CurCp; MaxCp = me.MaxCp;
        HpPct = me.HpPct; MpPct = me.MpPct; CpPct = me.CpPct;
        Position = $"{me.X}, {me.Y}, {me.Z}";
        TargetId = me.TargetId;
        IsSitting = me.IsSitting;
        Exp = me.Exp;
    }
}
