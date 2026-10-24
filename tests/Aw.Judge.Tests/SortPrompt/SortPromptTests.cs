#pragma warning disable xUnit1004 // red tests, written ahead of the cutover unit that makes them pass (spec §12 P0)
using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Providers;
using Aw.Judge.Features.SortPrompt;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.SortPrompt;

// The prompt sorter (cutover spec §5.2, unit 6.1): one prompt in, one 'prompt-sort' decision with its axes and run out.
// Red until 6.1: the stub takes no ports, so each test builds the fakes the real service will be given.
public sealed class SortPromptTests
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact(Skip = RedUntil.Sorter), Trait("Lane", "unit")]
    public async Task SortPrompt_records_the_decision_its_axes_and_its_run_and_adds_no_context_while_every_scaffold_is_off()
    {
        var log = new InMemoryDecisionLog();
        var store = new InMemoryPromptSortStore();
        var service = new SortPromptService();

        var result = await service.Handle(A.SortPromptRequest().Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Context);
        var decision = Assert.Single(log.Decisions);
        Assert.Equal("prompt-sort", decision.Question);
        var saved = Assert.Single(store.Records);
        Assert.Equal(decision.Id, saved.Decision);
        Assert.Equal(11, saved.Axes.Count);
    }

    [Theory(Skip = RedUntil.Sorter), Trait("Lane", "unit")]
    [InlineData("<system-reminder>machine text</system-reminder>")]
    [InlineData("/clear")]
    [InlineData("This session is being continued from a previous conversation")]
    [InlineData("yes")]
    public async Task SortPrompt_skips_machine_text_slash_commands_and_confirmations_without_recording(string prompt)
    {
        var log = new InMemoryDecisionLog();
        var service = new SortPromptService();

        var result = await service.Handle(A.SortPromptRequest().Prompt(prompt).Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Context);
        Assert.Empty(log.Decisions);
    }

    [Fact(Skip = RedUntil.Sorter), Trait("Lane", "unit")]
    public async Task SortPrompt_asks_jev_once_for_all_eleven_axes()
    {
        var jev = FakeBatchClassifier.Unavailable();
        var service = new SortPromptService();

        _ = await service.Handle(A.SortPromptRequest().Build(), _ct);

        var batch = Assert.Single(jev.Asked);
        Assert.Equal(11, batch.Questions.Count);
        Assert.Equal(9, batch.Questions.OfType<NoulBatchQuestion>().Count());
        Assert.Equal(2, batch.Questions.OfType<ScoreBatchQuestion>().Count());
    }

    [Fact(Skip = RedUntil.Sorter), Trait("Lane", "unit")]
    public async Task SortPrompt_with_jev_excluded_by_the_allowlist_sorts_by_heuristics_alone()
    {
        var jev = FakeBatchClassifier.Unavailable();
        var store = new InMemoryPromptSortStore();
        var service = new SortPromptService();

        var result = await service.Handle(A.SortPromptRequest().Allowing(ProviderName.Rules).Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("heuristic-only", Assert.Single(store.Records).Run.Mode);
        Assert.Empty(jev.Asked);
    }

    [Fact(Skip = RedUntil.Sorter), Trait("Lane", "unit")]
    public async Task SortPrompt_redacts_a_secret_before_it_reaches_jev_the_decision_details_and_the_session_file()
    {
        var jev = FakeBatchClassifier.Unavailable();
        var log = new InMemoryDecisionLog();
        var sessions = new InMemorySortSessionStore();
        var service = new SortPromptService();

        _ = await service.Handle(A.SortPromptRequest().Prompt("deploy with " + SampleSecrets.GitHubToken).Build(), _ct);

        Assert.DoesNotContain(SampleSecrets.GitHubToken, Assert.Single(jev.Asked).State?.ToJsonString(), StringComparison.Ordinal);
        Assert.DoesNotContain(SampleSecrets.GitHubToken, Assert.Single(log.Decisions).Details?.InputJson, StringComparison.Ordinal);
        Assert.DoesNotContain(SampleSecrets.GitHubToken, string.Join(' ', sessions.Sessions.Values), StringComparison.Ordinal);
    }

    [Fact(Skip = RedUntil.Sorter), Trait("Lane", "unit")]
    public async Task SortPrompt_counts_the_session_s_prompts_and_keeps_the_keys_of_its_file_it_does_not_know()
    {
        var sessions = new InMemorySortSessionStore();
        var service = new SortPromptService();

        _ = await service.Handle(A.SortPromptRequest().Build(), _ct);
        _ = await service.Handle(A.SortPromptRequest().Build(), _ct);

        Assert.Equal(2, sessions.Sessions["sess-1"].Prompts);
    }
}
