using System.Text.RegularExpressions;
using Aw.Context.Core.Engine;

namespace Aw.Context.Tests.Core;

// Which regex engine each kind of profile pattern compiles to (#1160): globs, the built-in secret globs and role and recognizer regexes run NonBacktracking; a look-ahead keeps the backtracking engine and its limit.
public sealed class PathDenylistEngineTests
{
    // '(?=.)' is a look-ahead NonBacktracking can't run, so this role entry keeps the backtracking engine and its limit; 40 'a's then '!' take it exponential.
    [Fact, Trait("Lane", "unit")]
    public void A_role_regex_that_must_backtrack_and_runs_out_of_time_carries_its_pattern()
    {
        const string pattern = "^(?=.)(a+)+$";
        var segment = new string('a', 40) + "!";

        var timeout = Assert.Throws<RegexMatchTimeoutException>(() => PathDenylist.Matches("src/" + segment + "/x.ts", ["regex:" + pattern]));

        Assert.Equal(pattern, timeout.Pattern);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("**/x/**")]
    [InlineData("*.test.*")]
    [InlineData("src/?.ts")]
    [InlineData("**/__tests__/**")]
    [InlineData("**/*cron*")]
    [InlineData("legacy/**")]
    [InlineData("src/a+b(1).ts")]
    public void A_sample_profile_glob_compiles_NonBacktracking(string glob) => Assert.True(PathDenylist.GlobRegex(glob).Options.HasFlag(RegexOptions.NonBacktracking));

    [Fact, Trait("Lane", "unit")]
    public void Every_built_in_secret_glob_compiles_NonBacktracking() =>
        Assert.All(PathDenylist.Defaults, g => Assert.True(PathDenylist.GlobRegex(g).Options.HasFlag(RegexOptions.NonBacktracking), g));

    [Theory, Trait("Lane", "unit")]
    [InlineData("^v\\d+$")]
    [InlineData("\\.(snap|golden)$")]
    [InlineData("^old_.*")]
    public void A_sample_role_regex_compiles_NonBacktracking(string pattern) =>
        Assert.True(PathDenylist.SegmentRegex(pattern, RegexOptions.IgnoreCase).Options.HasFlag(RegexOptions.NonBacktracking));

    [Theory, Trait("Lane", "unit")]
    [InlineData("(?<name>[A-Z][A-Za-z]+)Command")]
    [InlineData("\\bid:\\s*\"(?<name>[\\w.-]+)\"")]
    public void A_sample_recognizer_pattern_runs_NonBacktracking(string pattern) =>
        Assert.True(LinearRegex.Create(pattern, RegexOptions.CultureInvariant).Options.HasFlag(RegexOptions.NonBacktracking));
}
