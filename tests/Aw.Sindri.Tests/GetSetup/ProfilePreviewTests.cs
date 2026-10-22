using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Tests.GetSetup;

/// <summary>The plain-words preview of a profile: its repos, services, allowed effects and agent limits.</summary>
public sealed class ProfilePreviewTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_full_profile_says_repos_services_effects_and_limits_in_that_order()
    {
        var profile = new SindriProfile(
            1,
            "thor",
            Repos: [new RepoEntry("demo", "/work/demo")],
            Services: new Dictionary<string, ServiceSettings>(StringComparer.Ordinal) { ["observe"] = new ServiceSettings("1h"), ["heartbeat"] = new ServiceSettings("30s") },
            Workflow: new WorkflowSettings(Effects: ["pr.open", "pr.comment"]),
            Agents: new AgentSettings(0.8m, new Dictionary<string, AgentLimits>(StringComparer.Ordinal) { ["claude"] = new AgentLimits(DailyBudget: 5m) }));

        var lines = ProfilePreview.Lines(profile);

        Assert.Equal(
            [
                new SetupPreviewLine("repo", "Works in demo (/work/demo)."),
                new SetupPreviewLine("service", "Runs the heartbeat service every 30s."),
                new SetupPreviewLine("service", "Runs the observe service every 1h."),
                new SetupPreviewLine("allows", "Workflows may: pr.open, pr.comment."),
                new SetupPreviewLine("limit", "Warns you when an agent has used 80% of its daily budget."),
                new SetupPreviewLine("limit", "claude may spend up to 5 a day."),
            ],
            lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bare_profile_still_says_what_workflows_may_do()
    {
        var lines = ProfilePreview.Lines(new SindriProfile(1, "thor"));

        Assert.Equal([new SetupPreviewLine("allows", "Workflows may not change anything outside Sindri.")], lines);
    }
}
