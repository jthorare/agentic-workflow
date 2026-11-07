using Aw.Sindri.Core.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The inner policy leaves a Step's run area writable under the denied Sindri home (#2148, bug 4).</summary>
public sealed class SandboxPlanDenyCarveTests
{
    private const string Runs = Req.SindriHome + "/workflow/runs";

    private static readonly Dictionary<string, string[]> _tree = new(StringComparer.Ordinal)
    {
        [Req.StateDir] = ["sindri-app", "locks", "judge"],
        [Req.SindriHome] = ["ledger.db", "workflow", "auth"],
        [Req.SindriHome + "/workflow"] = ["runs", "areas"],
        [Runs] = ["x", "y"],
        [Runs + "/x"] = ["s", "t"],
        [Runs + "/x/s"] = ["1", "2"],
    };

    private static SandboxRequest WithListing(SandboxRequest request) =>
        request with { Host = request.Host with { DirectoryEntries = dir => _tree.GetValueOrDefault(dir) ?? [] } };

    private static bool Covers(SandboxPlan plan, string path) =>
        plan.Policy.Rules.Any(r =>
            r.Effect == InnerRuleEffect.Deny
            && ((string.Equals(r.Tool, "Read", StringComparison.Ordinal) && r.Pattern.EndsWith("/**", StringComparison.Ordinal) && IsInside(path, r.Pattern[..^3]))
                || (string.Equals(r.Tool, "Grep", StringComparison.Ordinal) && IsInside(path, r.Pattern))
                || (string.Equals(r.Tool, "Bash", StringComparison.Ordinal) && r.Pattern.StartsWith("cat ", StringComparison.Ordinal) && IsInside(path, r.Pattern[4..^1]))));

    private static bool IsInside(string path, string denied) => string.Equals(path, denied, StringComparison.Ordinal) || path.StartsWith(denied + "/", StringComparison.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void The_run_directory_is_not_read_denied_under_the_denied_home()
    {
        var plan = SandboxPlannerTests.Build(WithListing(Req.WriteScope));

        Assert.False(Covers(plan, Req.RunDirectory));
        Assert.False(Covers(plan, Req.RunDirectory + "/.sindri-out/result-1.json"));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_rest_of_the_home_and_other_runs_stay_denied()
    {
        var plan = SandboxPlannerTests.Build(WithListing(Req.WriteScope));

        Assert.True(Covers(plan, Req.SindriHome + "/ledger.db"));
        Assert.True(Covers(plan, Req.SindriHome + "/auth/human.token"));
        Assert.True(Covers(plan, Req.StateDir + "/locks/heavy"));
        Assert.True(Covers(plan, Req.SindriHome + "/workflow/areas/run-1"));
        Assert.True(Covers(plan, Runs + "/y/s/1"));
        Assert.True(Covers(plan, Runs + "/x/t"));
        Assert.True(Covers(plan, Runs + "/x/s/2"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_denied_path_with_no_writable_path_inside_is_rendered_whole()
    {
        var plan = SandboxPlannerTests.Build(WithListing(Req.WriteScope));
        var rules = plan.Policy.Rules.Where(r => r.Pattern.Contains("/etc/ssh", StringComparison.Ordinal)).Select(r => (r.Effect, r.Tool, r.Pattern)).ToList();

        Assert.Contains((InnerRuleEffect.Deny, "Read", "/etc/ssh/**"), rules);
        Assert.Contains((InnerRuleEffect.Deny, "Grep", "/etc/ssh"), rules);
        Assert.Contains((InnerRuleEffect.Deny, "Bash", "cat /etc/ssh*"), rules);
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_a_listing_or_a_writable_path_inside_the_home_stays_denied_whole()
    {
        var unlisted = SandboxPlannerTests.Build(Req.WriteScope);
        var outside = SandboxPlannerTests.Build(WithListing(Req.WriteScope with { Paths = Req.WriteScope.Paths with { RunDirectory = "/work/out/run" } }));

        Assert.Contains(unlisted.Policy.Rules, r => r.Tool == "Read" && r.Pattern == Req.SindriHome + "/**");
        Assert.Contains(outside.Policy.Rules, r => r.Tool == "Read" && r.Pattern == Req.SindriHome + "/**");
    }
}
