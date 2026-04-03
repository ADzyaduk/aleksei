using Alexei.Core.Data;
using Xunit;

namespace Alexei.Core.Tests.Data;

public sealed class SkillNamesTests
{
    [Fact]
    public void Get_FallsBackToSkillId_WhenResourceEntryIsBlank()
    {
        var display = SkillNames.Get(6035);
        Assert.Equal("Skill 6035", display);
    }
}
