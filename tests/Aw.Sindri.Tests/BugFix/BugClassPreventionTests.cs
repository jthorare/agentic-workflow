using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.BugFix;

/// <summary>The prevent-the-class rule (#1682, skills/_shared/prevent-the-class.md) reaches every bug-fix and repair agent Step's brief, and the handoff block and the report's three lines are named where they are written.</summary>
[Lane(TestLane.Unit)]
public sealed class BugClassPreventionTests
{
    private static readonly InstanceAddress _address = InstanceAddress.From("ITM-00000000000000000000000000000001/bugfix/1");

    public static TheoryData<string> BugFixSteps =>
    [
        "step.rootcause.investigate", "step.reproduce.test-check", "step.reproduce.ui-check", "step.implement.fix", "step.report.write",
        "step.pr.ci-fix", "step.pr.fix-comment", "step.review.fix",
    ];

    public static TheoryData<string> AskingSteps =>
    [
        "step.rootcause.investigate", "step.implement.fix", "step.pr.ci-fix", "step.pr.fix-comment", "step.review.fix",
    ];

    private static IReadOnlyList<string> Told(string step) =>
        ShippedComposition.Shared.GetServices<IAgentStepInstructions>().FirstOrDefault(i => i.IsFor(BlockId.From(step)))?.Lines ?? [];   // why: the launch takes the first match (AgentLaunching.Told)

    private static string ToldText(string step) => string.Join('\n', Told(step));

    [Fact]
    public void The_question_is_the_shared_files_quoted_line_word_for_word()
    {
        var shared = File.ReadAllLines(Path.Combine(RepoRoot(), "skills", "_shared", "prevent-the-class.md"));
        var quoted = Assert.Single(shared, static l => l.StartsWith("> How can we prevent", StringComparison.Ordinal));

        Assert.Equal(BugClassPrevention.Question, quoted[2..]);
    }

    [Theory]
    [MemberData(nameof(BugFixSteps))]
    public void Every_bug_fix_and_repair_Step_is_told_the_question(string step) =>
        Assert.Contains(BugClassPrevention.Question, ToldText(step), StringComparison.Ordinal);

    [Theory]
    [MemberData(nameof(BugFixSteps))]
    public void Every_bug_fix_Step_points_at_the_full_rule(string step) =>
        Assert.Contains("prevent-the-class.md", ToldText(step), StringComparison.Ordinal);

    [Fact]
    public void The_shared_lines_ask_before_the_fix_and_keep_mitigation_apart()
    {
        var all = string.Join('\n', BugClassPrevention.SharedLines);

        Assert.StartsWith("Prevent the class", all, StringComparison.Ordinal);
        Assert.Contains("before you choose a fix", all, StringComparison.Ordinal);
        Assert.Contains("invariant", all, StringComparison.Ordinal);
        Assert.Contains("other instances", all, StringComparison.Ordinal);
        Assert.Contains("mitigation", all, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("follow-up", all, StringComparison.Ordinal);
        Assert.Contains("never claim the class is gone", all, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("## Failure class")]
    [InlineData("## Invariant")]
    [InlineData("## Affected scope")]
    [InlineData("## Prevention decision")]
    [InlineData("## Mitigation")]
    [InlineData("## Proof")]
    [InlineData("Prevention: pending")]
    public void The_investigation_hands_on_the_block_with_all_six_headings(string heading) =>
        Assert.Contains(heading, ToldText("step.rootcause.investigate"), StringComparison.Ordinal);

    [Fact]
    public void The_investigation_confirms_the_cause_first_and_says_what_was_ruled_out_when_unfixed()
    {
        var all = ToldText("step.rootcause.investigate");

        Assert.True(all.IndexOf("reproduce", StringComparison.OrdinalIgnoreCase) < all.IndexOf(BugClassPrevention.Question, StringComparison.Ordinal));
        Assert.Contains("fact:rootCause", all, StringComparison.Ordinal);
        Assert.Contains("fact:findings", all, StringComparison.Ordinal);
        Assert.Contains("ruled out", all, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("step.reproduce.test-check")]
    [InlineData("step.reproduce.ui-check")]
    public void The_reproduction_checks_the_original_failure_only_and_keeps_the_block_as_read(string step)
    {
        var all = ToldText(step);

        Assert.Contains("original failure", all, StringComparison.Ordinal);
        Assert.Contains("reported reason", all, StringComparison.Ordinal);
        Assert.Contains("don't broaden", all, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("as you read it", all, StringComparison.Ordinal);
    }

    [Fact]
    public void The_fix_builds_the_instance_test_and_the_decided_mechanism_and_never_touches_the_reproduction()
    {
        var all = ToldText("step.implement.fix");

        Assert.Contains("fact:rootCause", all, StringComparison.Ordinal);
        Assert.Contains("Prevention decision", all, StringComparison.Ordinal);
        Assert.Contains("deferred", all, StringComparison.Ordinal);
        Assert.Contains("own proof", all, StringComparison.Ordinal);
        Assert.Contains("never change the reproduction check", all, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fact:output", all, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Original bug:")]
    [InlineData("Class prevention:")]
    [InlineData("Deferred:")]
    [InlineData("fact:resolution")]
    [InlineData("fact:rootCause")]
    public void The_report_writes_the_three_lines_apart_and_copies_the_block(string phrase) =>
        Assert.Contains(phrase, ToldText("step.report.write"), StringComparison.Ordinal);

    [Theory]
    [MemberData(nameof(AskingSteps))]
    public void The_Steps_that_choose_a_fix_name_the_class_and_the_prevention_or_follow_up_they_leave(string step)
    {
        var all = ToldText(step);

        Assert.Contains("class", all, StringComparison.Ordinal);
        Assert.Contains("follow-up", all, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(BugFixSteps))]
    public void The_rendered_brief_is_the_golden(string step)
    {
        var run = new AgentStepRun(WorkflowInstanceId.Of(_address), _address, RunToken.From("1.1"), new StepRef("s", step), WriteScope: false);
        var brief = AgentStepBrief.Render(run, FixtureCatalog.Shipped().Find(BlockId.From(step)), Told(step), ["fact-issue.json"]);

        Golden.Match(brief + "\n", typeof(BugClassPreventionTests).Assembly, $"Golden/Brief/{step}.txt", Normalization.None);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AgenticWorkflow.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("the repo root was not found");
    }
}
