using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>'sindri sandbox show' and 'explain' (sandbox spec §3.6): text pinned by goldens, pure over the plan.</summary>
public sealed class SandboxPlanRendererTests
{
    private static readonly string[] _bannedOwnerWords = ["scope", "plan", "canary", "confined"];

    private static void Match(string text, string golden) => Golden.Match(text, typeof(SandboxPlanRendererTests).Assembly, "Sandbox/Plan/Golden/" + golden, Normalization.None);

    [Fact, Trait("Lane", "unit")]
    public void Show_of_a_read_only_step_matches_its_golden() => Match(SandboxPlanRenderer.Show(SandboxPlannerTests.Build(Req.ReadOnly)), "show-readonly.txt");

    [Fact, Trait("Lane", "unit")]
    public void Show_of_a_write_scope_step_matches_its_golden() => Match(SandboxPlanRenderer.Show(SandboxPlannerTests.Build(Req.WriteScope)), "show-writescope.txt");

    [Fact, Trait("Lane", "unit")]
    public void Show_of_an_attended_session_matches_its_golden() => Match(SandboxPlanRenderer.Show(SandboxPlannerTests.Build(Req.Attended)), "show-attended.txt");

    [Fact, Trait("Lane", "unit")]
    public void Show_lists_names_never_values()
    {
        var text = SandboxPlanRenderer.Show(SandboxPlannerTests.Build(Req.WriteScope));

        Assert.DoesNotContain("ghp_planted", text, StringComparison.Ordinal);
        Assert.DoesNotContain("xterm-256color", text, StringComparison.Ordinal);
        Assert.Contains("AW_SINDRI_LAUNCH_SECRET", text, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("/home/owner/.agentic-workflow/ledger.db", "explain-path-denied.txt")]
    [InlineData("/work/web/.claude/worktrees/sindri-x-1/src/a.cs", "explain-path-writable.txt")]
    [InlineData("/home/owner/.cache/tool", "explain-path-not-bound.txt")]
    [InlineData("api.anthropic.com", "explain-host-allowed.txt")]
    [InlineData("evil.example", "explain-host-denied.txt")]
    [InlineData("git push origin main", "explain-command-denied.txt")]
    [InlineData("dotnet build", "explain-command-not-listed.txt")]
    [InlineData("", "explain-empty.txt")]
    public void Explain_answers_each_kind_of_question_and_matches_its_golden(string query, string golden)
    {
        var text = SandboxPlanRenderer.Explain(SandboxPlannerTests.Build(Req.WriteScope), query);

        Match(text, golden);
        Assert.All(_bannedOwnerWords, w => Assert.DoesNotContain(w, text, StringComparison.OrdinalIgnoreCase));
    }

    [Fact, Trait("Lane", "unit")]
    public void Explain_answers_from_the_plan_alone()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope);

        Assert.Equal(SandboxPlanRenderer.Explain(plan, "evil.example"), SandboxPlanRenderer.Explain(plan with { }, "evil.example"));
    }

    private static SandboxPlan LearningPlan()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope);
        return plan with { Network = plan.Network with { Mode = NetworkMode.Learn, LearnUntil = new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero) } };
    }

    [Fact, Trait("Lane", "unit")]
    public void Show_of_a_learning_plan_names_the_expiry() => Match(SandboxPlanRenderer.Show(LearningPlan()), "show-learning.txt");

    [Fact, Trait("Lane", "unit")]
    public void Explain_of_an_unlisted_host_while_learning_names_the_expiry() => Match(SandboxPlanRenderer.Explain(LearningPlan(), "evil.example"), "explain-host-learning.txt");
}
