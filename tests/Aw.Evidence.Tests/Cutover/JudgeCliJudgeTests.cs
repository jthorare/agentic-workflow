using System.Text.Json;
using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Judging;
using Aw.Evidence.Host;
using Aw.Evidence.Infrastructure.Judging;
using Aw.Evidence.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Evidence.Tests.Cutover;

/// <summary>
/// Cutover gap G-c (spec §3.3): the judge through the C# judge CLI. The contract is docs/csharp/judge.md 'Questions': one
/// command per question, the input record on stdin, one envelope on stdout; ui-element-repair (input
/// {brokenSelector, step, candidates:[{index, role, accessibleName, testId, text}]}, outputs repaired + chosenIndex or
/// no-good-candidate) and visual-critique (input {afterScreenshot, evidenceDir, baselineScreenshot?} with each screenshot a
/// path relative to the absolute evidence directory, outputs looks-right|looks-off|sloppy + reasons). Exit 2 is an escalation
/// (below threshold, invalid input, ...): the adapter fails closed with null. Red until unit 1.3 builds the adapter over ICliRunner.
/// </summary>
public sealed class JudgeCliJudgeTests
{
    private const string Judge = "/opt/aw/judge";

    private static readonly SelectorRepairRequest _repair = new(
        "open-panel", "click open-panel; expected: text \"Panel\" visible",
        [new Candidate(0, "button", "Open", TestId.From("open"), "Open"), new Candidate(1, "link", AccessibleName: null, TestId: null, Text: "Panel")]);

    private static JudgeCliJudge Create(FakeCliRunner runner, bool judgeFound = true)
    {
        var locator = new FakeExecutableLocator();
        _ = judgeFound ? locator.Executable(Judge) : locator;
        return new JudgeCliJudge(runner, locator, name => name == "AW_JUDGE_BIN" ? Judge : null);
    }

    private static CliOutcome Decided(string json) => new(0, json, string.Empty);

    [Fact, Trait("Lane", "unit")]
    public async Task Selector_repair_runs_ui_element_repair_with_the_broken_selector_the_step_and_the_candidates_on_stdin()
    {
        var runner = new FakeCliRunner().Program(Judge, Decided("""{"decision":"no-good-candidate","confidence":0.8,"model":"claude-cli","reasonCode":"model","id":"d1"}"""));

        _ = await Create(runner).RepairSelectorAsync(_repair, CancellationToken.None);

        var call = Assert.Single(runner.Calls);
        Assert.Equal(Judge, call.Executable);
        Assert.Equal(["ui-element-repair"], call.Args);
        using var input = JsonDocument.Parse(call.Stdin!);
        var root = input.RootElement;
        Assert.Equal("open-panel", root.GetProperty("brokenSelector").GetString());
        Assert.Equal(_repair.Step, root.GetProperty("step").GetString());
        var second = root.GetProperty("candidates")[1];
        Assert.Equal((1, "link", JsonValueKind.Null, JsonValueKind.Null, "Panel"),
            (second.GetProperty("index").GetInt32(), second.GetProperty("role").GetString(), second.GetProperty("accessibleName").ValueKind, second.GetProperty("testId").ValueKind, second.GetProperty("text").GetString()));
        Assert.Equal("open", root.GetProperty("candidates")[0].GetProperty("testId").GetString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repaired_envelope_is_the_chosen_candidate_with_the_model_that_decided()
    {
        var runner = new FakeCliRunner().Program(Judge, Decided("""{"decision":"repaired","chosenIndex":1,"confidence":0.9,"model":"claude-cli","reasonCode":"model","id":"d1"}"""));

        var repair = await Create(runner).RepairSelectorAsync(_repair, CancellationToken.None);

        Assert.NotNull(repair);
        Assert.Equal((RepairDecision.Repaired, 1, "claude-cli"), (repair.Decision, repair.ChosenIndex, repair.Usage?.Model));
        Assert.Null(repair.Usage?.InputTokens);   // the envelope carries no token counts: unknown, never zero
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_good_candidate_is_an_answer_with_no_chosen_index()
    {
        var runner = new FakeCliRunner().Program(Judge, Decided("""{"decision":"no-good-candidate","confidence":0.8,"model":"rules","reasonCode":"rule","id":"d1"}"""));

        var repair = await Create(runner).RepairSelectorAsync(_repair, CancellationToken.None);

        Assert.NotNull(repair);
        Assert.Equal((RepairDecision.NoGoodCandidate, null), (repair.Decision, repair.ChosenIndex));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(2, """{"escalated":true,"reasonCode":"below-threshold"}""")]                  // unsure: fail closed
    [InlineData(2, """{"escalated":true,"reasonCode":"no-provider-decided"}""")]              // AW_JUDGE_PROVIDERS=rules, offline
    [InlineData(1, "")]
    [InlineData(64, "")]                                                                      // unknown question or a usage error
    [InlineData(0, "not json")]
    [InlineData(0, """{"decision":"maybe","confidence":0.9,"model":"x","reasonCode":"model","id":"d"}""")]   // out of enum
    [InlineData(0, """{"decision":"repaired","confidence":0.9,"model":"x","reasonCode":"model","id":"d"}""")] // repaired without chosenIndex
    [InlineData(0, """{"decision":"repaired","chosenIndex":-1,"confidence":0.9,"model":"x","reasonCode":"model","id":"d"}""")]
    public async Task A_judge_that_escalates_fails_or_answers_out_of_contract_gives_no_repair(int exit, string stdout)
    {
        var runner = new FakeCliRunner().Program(Judge, new CliOutcome(exit, stdout, string.Empty));

        Assert.Null(await Create(runner).RepairSelectorAsync(_repair, CancellationToken.None));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_judge_that_would_not_start_gives_no_repair_and_no_critique()
    {
        var runner = new FakeCliRunner();
        _ = runner.WontStart.Add(Judge);
        var judge = Create(runner);

        Assert.Null(await judge.RepairSelectorAsync(_repair, CancellationToken.None));
        Assert.Null(await judge.CritiqueAsync(new VisualCritiqueRequest("/runs/r1/after.png", BaselineScreenshot: null, "/runs/r1"), CancellationToken.None));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Visual_critique_sends_paths_relative_to_the_evidence_directory_and_leaves_out_a_missing_baseline()
    {
        var runner = new FakeCliRunner().Program(Judge, Decided("""{"decision":"looks-right","confidence":0.9,"model":"claude-cli","reasonCode":"model","id":"d1","reasons":[]}"""));

        _ = await Create(runner).CritiqueAsync(new VisualCritiqueRequest("/runs/r1/shots/after.png", "/runs/r1/baseline.png", "/runs/r1"), CancellationToken.None);
        _ = await Create(runner).CritiqueAsync(new VisualCritiqueRequest("/runs/r1/after.png", BaselineScreenshot: null, "/runs/r1"), CancellationToken.None);

        Assert.Equal(["visual-critique"], runner.Calls[0].Args);
        using var withBaseline = JsonDocument.Parse(runner.Calls[0].Stdin!);
        Assert.Equal(("shots/after.png", "baseline.png", "/runs/r1"),
            (withBaseline.RootElement.GetProperty("afterScreenshot").GetString(), withBaseline.RootElement.GetProperty("baselineScreenshot").GetString(), withBaseline.RootElement.GetProperty("evidenceDir").GetString()));
        using var withoutBaseline = JsonDocument.Parse(runner.Calls[1].Stdin!);
        Assert.False(withoutBaseline.RootElement.TryGetProperty("baselineScreenshot", out _));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("looks-right", VisualDecision.LooksRight)]
    [InlineData("looks-off", VisualDecision.LooksOff)]
    [InlineData("sloppy", VisualDecision.Sloppy)]
    public async Task A_decided_critique_is_the_verdict_with_its_reasons_and_the_model(string decision, VisualDecision expected)
    {
        var runner = new FakeCliRunner().Program(Judge, Decided($$"""{"decision":"{{decision}}","confidence":0.7,"model":"codex-cli","reasonCode":"model","id":"d1","reasons":["the header overlaps the table"]}"""));

        var verdict = await Create(runner).CritiqueAsync(new VisualCritiqueRequest("/runs/r1/after.png", BaselineScreenshot: null, "/runs/r1"), CancellationToken.None);

        Assert.NotNull(verdict);
        Assert.Equal((expected, "codex-cli"), (verdict.Decision, verdict.Usage?.Model));
        Assert.Equal(["the header overlaps the table"], verdict.Reasons);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(2, """{"escalated":true,"reasonCode":"below-threshold"}""")]
    [InlineData(2, """{"escalated":true,"reasonCode":"invalid-input"}""")]
    [InlineData(1, "")]
    [InlineData(0, "")]
    [InlineData(0, """{"decision":"pretty","confidence":0.9,"model":"x","reasonCode":"model","id":"d"}""")]
    public async Task A_critique_that_escalates_fails_or_answers_out_of_contract_is_null_so_the_verdict_stays_unchecked(int exit, string stdout)
    {
        var runner = new FakeCliRunner().Program(Judge, new CliOutcome(exit, stdout, string.Empty));

        Assert.Null(await Create(runner).CritiqueAsync(new VisualCritiqueRequest("/runs/r1/after.png", BaselineScreenshot: null, "/runs/r1"), CancellationToken.None));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_screenshot_outside_the_evidence_directory_is_never_sent_to_the_judge()
    {
        var runner = new FakeCliRunner().Program(Judge, Decided("""{"decision":"looks-right","confidence":0.9,"model":"x","reasonCode":"model","id":"d","reasons":[]}"""));

        var verdict = await Create(runner).CritiqueAsync(new VisualCritiqueRequest("/elsewhere/after.png", BaselineScreenshot: null, "/runs/r1"), CancellationToken.None);

        Assert.Null(verdict);
        Assert.Empty(runner.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_judge_is_available_when_the_judge_binary_is_found_and_not_when_it_is_missing()
    {
        var runner = new FakeCliRunner();

        Assert.True(Create(runner, judgeFound: true).IsAvailable);
        Assert.False(Create(runner, judgeFound: false).IsAvailable);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_composition_registers_the_judge_cli_adapter_in_place_of_the_unavailable_judge()
    {
        using var provider = Composition.Compose(new ServiceCollection(), name => name == "AW_STATE_DIR" ? "/nonexistent/aw-evidence-di-test" : null, new FakeTimeProvider())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        _ = Assert.IsType<JudgeCliJudge>(provider.GetRequiredService<IJudge>());
    }
}
