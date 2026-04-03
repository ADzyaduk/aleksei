using Alexei.App.Infrastructure;
using Alexei.Core.Config;

namespace Alexei.App.ViewModels;

public sealed class RecoveryTabVM : ViewModelBase
{
    private readonly ProfileManager _pm;

    private bool _enabled; public bool Enabled { get => _enabled; set => SetField(ref _enabled, value); }
    private double _sitBelowHp; public double SitBelowHp { get => _sitBelowHp; set => SetField(ref _sitBelowHp, value); }
    private double _sitBelowMp; public double SitBelowMp { get => _sitBelowMp; set => SetField(ref _sitBelowMp, value); }
    private double _standAboveHp; public double StandAboveHp { get => _standAboveHp; set => SetField(ref _standAboveHp, value); }
    private double _standAboveMp; public double StandAboveMp { get => _standAboveMp; set => SetField(ref _standAboveMp, value); }

    public RecoveryTabVM(ProfileManager pm) { _pm = pm; }

    public void Refresh()
    {
        var r = _pm.Current.Recovery;
        Enabled = r.Enabled;
        SitBelowHp = r.SitBelowHpPct;
        SitBelowMp = r.SitBelowMpPct;
        StandAboveHp = r.StandAboveHpPct;
        StandAboveMp = r.StandAboveMpPct;
    }

    public void ApplyToProfile()
    {
        var r = _pm.Current.Recovery;
        r.Enabled = Enabled;
        r.SitBelowHpPct = SitBelowHp;
        r.SitBelowMpPct = SitBelowMp;
        r.StandAboveHpPct = StandAboveHp;
        r.StandAboveMpPct = StandAboveMp;
    }
}
