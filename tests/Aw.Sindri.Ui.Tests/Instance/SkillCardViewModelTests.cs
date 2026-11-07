using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Instance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>The skill card in the expanded state (skill flows spec §5.2, §5.4): the block's sentence, a state word, the question or the stopped text, the outcome in words, outputs as paths, the loop round and the cost.</summary>
[Lane(TestLane.Unit)]
public sealed class SkillCardViewModelTests
{
    private const string Address = "T-1";
    private const string Plan = "/work/plans/rate-limits";

    private readonly FakeClipboard _clipboard = new();
    private readonly FakeLauncher _launcher = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SkillBlockView Block() =>
        new("office-hours", "officeHours", "step.skill.office-hours", 955718892, "Shapes an idea into a plan folder with requirements and tasks.",
            [new SkillInputView("idea", "text", "What do you want built?", Required: true)], ["fact:officeHoursFeatureDir"], ["done"], Pushes: false, "234a44b2", "<skills>/officeHours/block.yaml");

    private static StateCell Cell(StateGlyph glyph = StateGlyph.Active, Round? round = null, string? outcome = null) =>
        new("shape", Order: 1, BranchOf: null, glyph, Badge: null, round, [], Desk: false, Child: false, Children: null, outcome, Terminal: false, Block: "office-hours");

    private static StepRun Run(string state = "running", string? outcome = null, int attempt = 1) =>
        new("shape.1", "shape", attempt, MaxAttempts: null, state, "agent", A.Noon, EndedAt: state == "running" ? null : A.Noon, DurationMs: null, outcome, Tokens: null, Actor: null);

    // The needs-you row the server serves for a waiting skill block (kind skillWaiting).
    private static NeedsYouRowView Waiting(string reason = "question", string? text = "Which repo should this plan cover?", string? detail = null, int? attempt = 1, string state = "shape", string instance = Address) =>
        new("skill:" + instance + ":" + state, "skillWaiting", "skill", Subject: null, Item: null, "Shape the idea", "waiting", Repo: null, "calm", A.Noon, RecordedOnly: false, Notice: null, AnswerKind: null, PrimaryAction: null, [], "sindri workflow status " + instance,
            OwnerWaiting: new OwnerWaitingView("LCH-1", reason, text, "/work/.sindri/shape/result.json", detail, instance, state, attempt));

    private static FactRow PlanFact() =>
        new("fact:officeHoursFeatureDir", "Plan folder", FactType.File, FactDirection.Output, From: null, FactStatus.Produced, Plan, A.Noon, By: null, new FactRef(Name: null, Size: null, Hash: null, Plan, ItemId: null, Url: null, Host: null, Title: null, FactKey: null), Progress: null, Pending: null);

    private SkillCardViewModel Card(StateCell? cell = null, StepRun? run = null, IReadOnlyList<NeedsYouRowView>? waiting = null, IReadOnlyList<FactRow>? facts = null, decimal? cost = null) =>
        new(new SkillCardInput(new SkillsView([Block()]), Address, "Shape the idea", cell ?? Cell(), run ?? Run(), waiting ?? [], facts ?? [], cost, _launcher, _clipboard, _clock));

    [Fact]
    public void SkillCard_shows_the_blocks_sentence() =>
        Assert.Equal("Shapes an idea into a plan folder with requirements and tasks.", Card().Sentence);

    [Fact]
    public void SkillCard_knows_a_skill_step_by_its_descriptor()
    {
        Assert.True(SkillCardViewModel.IsSkillStep(new StepDeclaration("step", "step.skill.office-hours", "use")));
        Assert.False(SkillCardViewModel.IsSkillStep(new StepDeclaration("step", "step.build.scope-desk", "use")));
        Assert.False(SkillCardViewModel.IsSkillStep(null));
    }

    [Fact]
    public void SkillCard_shows_the_question_when_the_block_waits_on_you()
    {
        var card = Card(waiting: [Waiting()]);

        Assert.Equal(("waiting on you", "Which repo should this plan cover?", "Shape the idea is waiting on you: Which repo should this plan cover?"),
            (card.StateWord, card.Question, card.Headline));
    }

    [Fact]
    public void SkillCard_reads_the_latest_question_of_its_own_state_and_run()
    {
        var card = Card(run: Run(attempt: 2), waiting: [Waiting(text: "Old question?", attempt: 1), Waiting(text: "New question?", attempt: 2), Waiting(text: "Other state?", attempt: 2, state: "critique")]);

        Assert.Equal("New question?", card.Question);
    }

    [Fact]
    public void SkillCard_ignores_a_question_from_an_earlier_run()
    {
        var card = Card(run: Run(attempt: 2), waiting: [Waiting(attempt: 1)]);

        Assert.Equal(("running", string.Empty), (card.StateWord, card.Question));
    }

    [Fact]
    public void SkillCard_noResult_reads_stopped_without_an_answer()
    {
        var card = Card(waiting: [Waiting("noResult", text: null)]);

        Assert.Equal(("waiting on you", "Shape the idea stopped without giving an answer. Open the session to see why."), (card.StateWord, card.Headline));
    }

    [Fact]
    public void SkillCard_unreadable_keeps_the_parse_error_in_details()
    {
        var card = Card(waiting: [Waiting("unreadable", text: null, detail: "SND-SKILL-001: unexpected token at line 3")]);

        Assert.Equal("Shape the idea finished, but its answer file is broken. Open the session and ask it to write the answer again.", card.Headline);
        Assert.DoesNotContain("SND-SKILL", card.Headline, StringComparison.Ordinal);
        Assert.Contains("unexpected token at line 3", card.Details, StringComparison.Ordinal);
        Assert.Contains("/work/.sindri/shape/result.json", card.Details, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Pending", null, "waiting to start")]
    [InlineData("Active", "running", "running")]
    [InlineData("Done", "passed", "done")]
    [InlineData("Active", "failed", "failed")]
    [InlineData("Active", "cancelled", "failed")]
    [InlineData("Active", "refused", "failed")]
    [InlineData("Done", "abandoned", "failed")]
    public void SkillCard_names_its_state_in_a_word(string glyph, string? run, string word) =>
        Assert.Equal(word, new SkillCardViewModel(new SkillCardInput(new SkillsView([Block()]), Address, "Shape the idea", Cell(Enum.Parse<StateGlyph>(glyph)), run is null ? null : Run(run), [], [], Cost: null, _launcher, _clipboard, _clock)).StateWord);

    [Fact]
    public void SkillCard_ignores_a_waiting_row_of_another_instance()
    {
        var card = Card(waiting: [Waiting(instance: "T-2")]);

        Assert.Equal(("running", string.Empty), (card.StateWord, card.Question));
    }

    [Fact]
    public void SkillCard_has_no_sentence_for_a_block_the_server_does_not_list() =>
        Assert.Equal(string.Empty, new SkillCardViewModel(new SkillCardInput(new SkillsView([]), Address, "Shape the idea", Cell(), Run(), [], [], Cost: null, _launcher, _clipboard, _clock)).Sentence);

    [Theory]
    [InlineData("needsWork", "needs work")]
    [InlineData("needsChanges", "needs changes")]
    [InlineData("pass", "pass")]
    [InlineData("done", "done")]
    public void SkillCard_names_the_outcome_in_words(string id, string words)
    {
        Assert.Equal(words, SkillWords.Outcome(id));
        Assert.Equal(words, Card(cell: Cell(outcome: id)).Outcome);
    }

    [Fact]
    public void SkillCard_says_nothing_of_the_outcome_before_the_run_reports_one() =>
        Assert.Equal(string.Empty, Card().Outcome);

    [Fact]
    public async Task SkillCard_lists_outputs_as_paths_that_open_and_copy()
    {
        _launcher.AddFile(Plan);
        var card = Card(facts: [PlanFact()]);

        var output = Assert.Single(card.Outputs);
        var opened = await card.Open(output, Token);
        var copied = await card.CopyPath(output, Token);

        Assert.Equal((new SkillOutput("Plan folder", Plan), true, true), (output, opened, copied));
        Assert.Equal([Plan], _launcher.Opened);
        Assert.Equal(Plan, _clipboard.Text);
    }

    [Fact]
    public void SkillCard_reads_an_outputs_path_from_its_summary_when_the_ref_has_none()
    {
        var fact = PlanFact() with { Type = FactType.Text, Ref = null };

        Assert.Equal([new SkillOutput("Plan folder", Plan)], Card(facts: [fact]).Outputs);
        Assert.Empty(Card(facts: [fact with { Summary = "plans/x" }]).Outputs);
        Assert.Empty(Card(facts: [fact with { Summary = Plan + "\nmore" }]).Outputs);
        Assert.Empty(Card(facts: [fact with { Status = FactStatus.InProgress }]).Outputs);
    }

    [Fact]
    public void SkillCard_lists_only_the_outputs_its_block_names() =>
        Assert.Empty(Card(facts: [PlanFact() with { Key = "fact:somethingElse" }]).Outputs);

    [Fact]
    public void SkillCard_says_cost_not_reported_unless_the_launch_reports_one()
    {
        Assert.Equal("cost: not reported", Card().CostLine);
        Assert.Equal("cost: $0.42", Card(cost: 0.42m).CostLine);
    }

    [Fact]
    public void SkillCard_shows_the_round_of_its_loop()
    {
        Assert.Equal("round 2 of 2", Card(cell: Cell(round: new Round(2, 2))).RoundLine);
        Assert.Equal(string.Empty, Card().RoundLine);
    }

    [Fact]
    public void SkillCard_skillUpdated_asks_to_approve_again()
    {
        var card = Card(waiting: [Waiting("skillUpdated", text: null)]);

        Assert.Equal(("waiting on you", "The office hours skill changed since this run started. Approve again to continue."), (card.StateWord, card.Headline));
    }

    [Theory]
    [InlineData("Pending", null, false)]
    [InlineData("Active", "running", true)]
    [InlineData("Done", "passed", false)]
    [InlineData("Active", "failed", false)]
    public void SkillCard_offers_Take_over_while_the_block_runs(string glyph, string? run, bool offered) =>
        Assert.Equal(offered, new SkillCardViewModel(new SkillCardInput(new SkillsView([Block()]), Address, "Shape the idea", Cell(Enum.Parse<StateGlyph>(glyph)), run is null ? null : Run(run), [], [], Cost: null, _launcher, _clipboard, _clock)).CanTakeOver);

    [Fact]
    public void SkillCard_offers_Take_over_while_it_waits_on_you() =>
        Assert.True(Card(waiting: [Waiting()]).CanTakeOver);

    [Fact]
    public void SkillCard_says_nothing_until_the_view_announces_and_then_says_the_question_once()
    {
        var card = Card(waiting: [Waiting()]);
        var before = (card.Voice.IsAnnouncing, card.Voice.Announcement);

        card.Announce();
        var said = (card.Voice.IsAnnouncing, card.Voice.Announcement);
        card.Voice.Quiet();
        card.Announce();

        Assert.Equal((false, "Skill status"), before);
        Assert.Equal((true, "Shape the idea is waiting on you: Which repo should this plan cover?"), said);
        Assert.False(card.Voice.IsAnnouncing);
    }

    [Fact]
    public void SkillCard_that_waits_on_nothing_announces_nothing()
    {
        var card = Card();

        card.Announce();

        Assert.False(card.Voice.IsAnnouncing);
        Assert.NotEmpty(card.Voice.Announcement);
    }
}
