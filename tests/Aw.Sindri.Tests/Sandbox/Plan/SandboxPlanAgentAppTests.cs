using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>SB-7 (sandbox spec §3.3, R1-14): the plan of a Step with the agent GitHub App binds the token's folder read-only and points git at the credential helper; no other launch does.</summary>
[Lane(TestLane.Unit)]
public sealed class SandboxPlanAgentAppTests
{
    private static readonly SandboxSettings _agentApp = new(Credentials: new SandboxCredentials(Github: GithubCredentials.AgentApp));

    private static string Session => $"{Req.SindriHome}-sessions/{Req.WriteScope.Launch.Value}";

    private static string Helper => Req.WriteScope.Paths.RunArea + "/shims/git-credential-sindri";

    private static int At(SandboxPlan plan, string path, PlanMountKind kind) =>
        plan.Mounts.ToList().FindLastIndex(m => string.Equals(m.Path, path, StringComparison.Ordinal) && m.Kind == kind);

    private static IEnumerable<string> Helpers(SandboxPlan plan)
    {
        var env = plan.Environment.ToDictionary(e => e.Name, e => e.Value, StringComparer.Ordinal);
        var count = int.Parse(env["GIT_CONFIG_COUNT"]!, System.Globalization.CultureInfo.InvariantCulture);
        for (var i = 0; i < count; i++)
        {
            var n = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (env["GIT_CONFIG_KEY_" + n] == "credential.helper")
            {
                yield return env["GIT_CONFIG_VALUE_" + n]!;
            }
        }
    }

    [Fact]
    public void An_agent_app_Step_binds_the_token_folder_read_only_after_the_session_home()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope with { Settings = _agentApp });

        var token = At(plan, Session + "/gh-token", PlanMountKind.ReadOnly);
        Assert.True(token > At(plan, Session, PlanMountKind.ReadOnly), "the folder is bound after the session home, depth order");
        Assert.Null(plan.Mounts[token].Source);   // at its own path, as a directory
    }

    [Fact]
    public void An_agent_app_Step_resets_the_credential_helper_then_names_the_shim()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope with { Settings = _agentApp });

        Assert.Equal([string.Empty, Helper], Helpers(plan), StringComparer.Ordinal);
    }

    [Fact]
    public void A_none_Step_has_neither_the_token_folder_nor_the_helper()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope);

        Assert.DoesNotContain(plan.Mounts, m => m.Path.EndsWith("/gh-token", StringComparison.Ordinal));
        Assert.Equal([string.Empty], Helpers(plan), StringComparer.Ordinal);
    }

    [Fact]
    public void An_attended_open_with_agent_app_in_the_profile_has_neither()
    {
        var plan = SandboxPlannerTests.Build(Req.Attended with { Settings = _agentApp });

        Assert.DoesNotContain(plan.Mounts, m => m.Path.EndsWith("/gh-token", StringComparison.Ordinal));
        Assert.Equal([string.Empty], Helpers(plan), StringComparer.Ordinal);
    }
}
