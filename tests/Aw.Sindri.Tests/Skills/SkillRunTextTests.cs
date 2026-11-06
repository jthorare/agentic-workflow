using Aw.Kernel.Testing;
using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.Skills;

/// <summary>What 'sindri skill run &lt;id&gt; --follow' prints and exits with (skill flows spec §7): the manifest hash at the top, then the outcome, each fact, the result path and the SND-SKILL line; 0 on the success outcome, 3 on another mapped one, 4 on step.failed.</summary>
[Lane(TestLane.Unit)]
public sealed class SkillRunTextTests
{
    private const string Hash = "9f2c4be1a07d58a3c1e6b4d9027f83aa5c61d0e4b7f29a8c3d5e1f60b2a47c98";

    private static readonly SkillRunReport _passed = new(
        "review", Hash, Outcome: "pass", Succeeded: true,
        [new("fact:reviewFindings", "/home/u/.agentic-workflow/acme-api/reviews/42.json")],
        ResultPath: "/home/u/.agentic-workflow/sindri-app/workflow/runs/WFI-1/review/LCH-1/.sindri-out/result-LCH-1.json", ErrorLine: null);

    private static void Match(SkillRunReport report, string name) =>
        Golden.Match(SkillRunText.Render(report), typeof(SkillRunTextTests).Assembly, $"Skills/Golden/{name}.txt", Normalization.None);

    [Fact]
    public void The_success_outcome_prints_the_hash_outcome_facts_and_result_path() => Match(_passed, "skill-run-pass");

    [Fact]
    public void Another_mapped_outcome_prints_the_same_shape() => Match(_passed with { Outcome = "needsChanges", Succeeded = false }, "skill-run-needs-changes");

    [Fact]
    public void A_failed_Step_ends_with_the_SND_SKILL_line() =>
        Match(
            new SkillRunReport("autoplan", Hash, Outcome: null, Succeeded: false, [], ResultPath: "/home/u/.agentic-workflow/sindri-app/workflow/runs/WFI-1/review/LCH-2/.sindri-out/result-LCH-2.json",
                ErrorLine: "SND-SKILL-003 autoplan said 'MAYBE', which its block.yaml doesn't map"),
            "skill-run-failed");

    [Fact]
    public void The_exit_code_is_0_on_success_3_on_another_outcome_and_4_on_step_failed()
    {
        Assert.Equal(0, SkillRunText.ExitCode(_passed));
        Assert.Equal(3, SkillRunText.ExitCode(_passed with { Outcome = "needsChanges", Succeeded = false }));
        Assert.Equal(4, SkillRunText.ExitCode(_passed with { Outcome = null, Succeeded = false, ErrorLine = "SND-SKILL-004 autoplan's result has no artifact 'consolidated-review.md'" }));
    }
}
