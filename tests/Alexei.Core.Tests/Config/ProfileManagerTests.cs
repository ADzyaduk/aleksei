using Alexei.Core.Config;
using Xunit;

namespace Alexei.Core.Tests.Config;

public sealed class ProfileManagerTests
{
    [Fact]
    public void ApplyServerDefaults_Bartz_EnablesTargetEnterAnd39Dcb()
    {
        string dir = Path.Combine(Path.GetTempPath(), "alexei-profile-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            var manager = new ProfileManager(dir);
            manager.LoadForCharacter("changolist", "bartz");

            bool changed = manager.ApplyServerDefaults("bartz");

            Assert.True(changed);
            Assert.True(manager.Current.Combat.Enabled);
            Assert.True(manager.Current.Combat.UseTargetEnter);
            Assert.Equal("39dcb", manager.Current.Combat.CombatSkillPacket);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ApplyServerDefaults_Bartz_DoesNotOverrideUserRadii()
    {
        string dir = Path.Combine(Path.GetTempPath(), "alexei-profile-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            var manager = new ProfileManager(dir);
            manager.LoadForCharacter("changolist", "bartz");
            manager.Current.Combat.AggroRadius = 900;
            manager.Current.Combat.AnchorLeash = 1800;
            manager.Current.Combat.RetainTargetMaxDist = 700;
            manager.Current.Loot.Radius = 650;

            manager.ApplyServerDefaults("bartz");

            Assert.Equal(900, manager.Current.Combat.AggroRadius);
            Assert.Equal(1800, manager.Current.Combat.AnchorLeash);
            Assert.Equal(700, manager.Current.Combat.RetainTargetMaxDist);
            Assert.Equal(650, manager.Current.Loot.Radius);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
