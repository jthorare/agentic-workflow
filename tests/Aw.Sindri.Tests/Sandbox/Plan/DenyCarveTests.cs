using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The carve stops at a writable path: nothing under it is denied, even when a deeper writable path sits inside it (#2148, bug 19).</summary>
public sealed class DenyCarveTests
{
    private const string State = "/home/u/.agentic-workflow";
    private const string AwDir = State + "/owner-repo";
    private const string RunDirectory = AwDir + "/proof/x/run";
    private const string SindriRuns = State + "/sindri-app/workflow/runs";

    private static readonly Dictionary<string, string[]> _tree = new(StringComparer.Ordinal)
    {
        [State] = ["owner-repo", "sindri-app", "locks", "other-repo"],
        [AwDir] = ["investigations", "plans", "reviews", "scratch", "proof"],
        [AwDir + "/proof"] = ["x", "y"],
        [AwDir + "/proof/x"] = ["run", "home"],
        [State + "/sindri-app"] = ["ledger.db", "workflow", "auth"],
        [State + "/sindri-app/workflow"] = ["runs", "areas"],
        [SindriRuns] = ["a", "b"],
    };

    private static IReadOnlyList<string> Entries(string dir) => _tree.GetValueOrDefault(dir) ?? [];

    private static bool Covers(IReadOnlyList<string> denied, string path) =>
        denied.Any(d => string.Equals(path, d, StringComparison.Ordinal) || path.StartsWith(d + "/", StringComparison.Ordinal));

    [Fact, Trait("Lane", "unit")]
    public void Nothing_under_a_writable_path_is_denied_when_a_deeper_writable_path_sits_inside_it()
    {
        var denied = DenyCarve.Around(State, [AwDir, RunDirectory], Entries);

        Assert.DoesNotContain(denied, d => Covers([AwDir], d));
        Assert.False(Covers(denied, AwDir + "/reviews/2148.json"));
        Assert.False(Covers(denied, RunDirectory + "/.sindri-out/result-1.json"));
        Assert.True(Covers(denied, State + "/locks"));
        Assert.True(Covers(denied, State + "/other-repo/plans"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_sibling_that_is_not_writable_stays_denied_beside_nested_writable_paths()
    {
        var denied = DenyCarve.Around(State, [AwDir, RunDirectory, SindriRuns + "/a"], Entries);

        Assert.True(Covers(denied, State + "/sindri-app/auth/human.token"));
        Assert.True(Covers(denied, State + "/sindri-app/ledger.db"));
        Assert.True(Covers(denied, SindriRuns + "/b"));
        Assert.True(Covers(denied, State + "/other-repo"));
        Assert.False(Covers(denied, SindriRuns + "/a/result.json"));
        Assert.False(Covers(denied, AwDir + "/plans/p.md"));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(AwDir + "/")]
    [InlineData(AwDir + "//")]
    public void A_writable_path_with_a_trailing_slash_is_carved_like_one_without(string writable)
    {
        Assert.Equal(DenyCarve.Around(State, [AwDir, RunDirectory], Entries), DenyCarve.Around(State, [writable, RunDirectory + "/"], Entries));
        Assert.Equal(DenyCarve.Around(State, [AwDir], Entries), DenyCarve.Around(State, [writable], Entries));
    }
}
