using Aw.CodeIndex.Core;
using Aw.Context.Core.Engine;

namespace Aw.Sindri.Tests.Profile;

/// <summary>The code index and the context engine read the same role and deny globs; one table runs through both so a fix to one can't leave the other behind.</summary>
public sealed class GlobAgreementTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("tests/fixtures/data.ts", "**/*fixture*/**", true)]
    [InlineData("tests/fixtures/data.ts", "**/*fixture*", true)]
    [InlineData("src/crons/run.ts", "**/*cron*", true)]
    [InlineData("src/nightly-cron.ts", "**/*cron*", true)]
    [InlineData("src/orders/place.ts", "**/*cron*", false)]
    [InlineData("src/Test_a.py", "prefix:test_", true)]
    [InlineData("src/a.generated.ts", "suffix:.generated.ts", true)]
    [InlineData("src/v2/a.ts", "regex:^v\\d+$", true)]
    [InlineData("src/v2b/a.ts", "regex:^v\\d+$", false)]
    public void Both_matchers_agree(string path, string glob, bool expected)
    {
        Assert.Equal(expected, InputRules.MatchesAny(path, [glob]));
        Assert.Equal(expected, PathDenylist.Matches(path, [glob]));
    }
}
