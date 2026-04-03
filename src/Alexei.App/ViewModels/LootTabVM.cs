using Alexei.App.Infrastructure;
using Alexei.Core.Config;

namespace Alexei.App.ViewModels;

public sealed class LootTabVM : ViewModelBase
{
    private readonly ProfileManager _pm;

    private bool _enabled; public bool Enabled { get => _enabled; set => SetField(ref _enabled, value); }
    private double _radius; public double Radius { get => _radius; set => SetField(ref _radius, value); }

    public LootTabVM(ProfileManager pm) { _pm = pm; }

    public void Refresh()
    {
        Enabled = _pm.Current.Loot.Enabled;
        Radius = _pm.Current.Loot.Radius;
    }

    public void ApplyToProfile()
    {
        _pm.Current.Loot.Enabled = Enabled;
        _pm.Current.Loot.Radius = Radius;
    }
}
