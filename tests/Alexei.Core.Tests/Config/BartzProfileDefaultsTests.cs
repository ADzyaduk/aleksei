using Alexei.Core.Config;
using Xunit;

namespace Alexei.Core.Tests.Config;

public sealed class BartzProfileDefaultsTests
{
    [Fact]
    public void ApplyServerDefaults_Bartz_DoesNotForcePreferAggroTargets()
    {
        string dir = Path.Combine(Path.GetTempPath(), "alexei-profile-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            var manager = new ProfileManager(dir);
            manager.LoadForCharacter("changolist", "bartz");

            bool changed = manager.ApplyServerDefaults("bartz");

            Assert.True(changed);
            Assert.False(manager.Current.Combat.PreferAggroTargets);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}

