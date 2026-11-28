using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The launcher.json problem row says which scope is in force: a typo'd or bad sessions section that holds 'own' never reads as the defaults (#4252).</summary>
public sealed class LauncherProblemRowTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_row_says_own_is_kept_when_own_is_in_force_and_says_the_defaults_otherwise()
    {
        static string Row(SessionScope scope) =>
            Assert.Single(new CollectorHarness(SessionSettings.Defaults with { ConfigProblem = "bad", Scope = scope }).Discovery.StaticProblems()).Text;

        var own = Row(SessionScope.Own);
        var all = Row(SessionScope.All);

        Assert.Contains("'own'", own, StringComparison.Ordinal);
        Assert.DoesNotContain("default settings are used", own, StringComparison.Ordinal);
        Assert.Equal("launcher.json isn't valid; the built-in agents and default settings are used", all);
    }
}
