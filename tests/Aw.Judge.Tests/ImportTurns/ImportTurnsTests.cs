using System.Text.Json.Nodes;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Transcripts;
using Aw.Judge.Features.ImportTurns;
using Aw.Judge.Host;
using Aw.Judge.Testing;
using Aw.Judge.Tests.Transcripts;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.ImportTurns;

// 'judge eval import-turns <transcript.jsonl>...': per-turn eval items from the edits in a transcript.
public sealed class ImportTurnsTests
{
    private const string Path = "/p/sess-1.jsonl";
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private static readonly DateTimeOffset _start = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly SessionId _session = SessionId.From("sess-1");

    [Fact, Trait("Lane", "unit")]
    public async Task ImportTurns_counts_a_file_it_cannot_read_as_unreadable_and_imports_nothing()
    {
        var harness = new Harness(transcript: null);

        var result = await harness.Service.Handle(A.ImportTurnsRequest().Files(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "no-such-transcript.jsonl")).Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ImportTurnsResponse(Imported: 0, Skipped: 0, Unreadable: 1), result.Value);
        Assert.Empty(harness.Store.Items);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ImportTurns_imports_a_turn_with_edits_as_a_turn_progress_item_and_skips_one_without()
    {
        var harness = new Harness(Transcript([Turn(index: 0, "fix it", Edit("a.cs", "old", "new")), Turn(index: 1, "and then?")]));

        var result = await harness.Run();

        Assert.Equal(new ImportTurnsResponse(Imported: 1, Skipped: 1, Unreadable: 0), result.Value);
        var item = Assert.Single(harness.Store.Items);
        Assert.Equal(("turn-progress", "transcript:sess-1:0", null), (item.Question, item.Source, item.ModelDecision));
        var input = JsonNode.Parse(item.InputJson)!;
        Assert.Equal("fix it", (string?)input["problem"]);
        Assert.Equal("--- a.cs\n- old\n+ new", (string?)input["turnDiff"]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ImportTurns_adds_a_turn_once()
    {
        var harness = new Harness(Transcript([Turn(index: 0, "fix it", Edit("a.cs", "old", "new"))]));

        _ = await harness.Run();
        var again = await harness.Run();

        Assert.Equal(new ImportTurnsResponse(Imported: 0, Skipped: 1, Unreadable: 0), again.Value);
        _ = Assert.Single(harness.Store.Items);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ImportTurns_labels_a_turn_whose_lines_survived_progressing_and_one_that_was_corrected_stalled()
    {
        var kept = new Harness(Transcript([Turn(index: 0, "fix it", Edit("a.cs", "", "int x = 1;\nint y = 2;")), Turn(index: 1, "thanks, now the docs")]));
        var corrected = new Harness(Transcript([Turn(index: 0, "fix it", Edit("a.cs", "", "int x = 1;\nint y = 2;")), Turn(index: 1, "no, revert that")]));

        _ = await kept.Run();
        _ = await corrected.Run();

        var (_, source, label, _) = Assert.Single(kept.Store.Labels);
        Assert.Equal((TurnOutcomes.Progressing, LabelSource.Outcome), (label, source));
        Assert.Equal(TurnOutcomes.Stalled, Assert.Single(corrected.Store.Labels).Label);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ImportTurns_labels_the_last_turn_only_once_its_session_has_closed()
    {
        var turns = new[] { Turn(index: 0, "fix it", Edit("a.cs", "", "int x = 1;")) };
        var open = new Harness(Transcript(turns, end: _start.AddHours(1)), now: _start.AddHours(2));
        var closed = new Harness(Transcript(turns, end: _start.AddHours(1)), now: _start.AddHours(8));

        _ = await open.Run();
        _ = await closed.Run();

        Assert.Empty(open.Store.Labels);
        Assert.Equal(TurnOutcomes.Progressing, Assert.Single(closed.Store.Labels).Label);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ImportTurns_skips_a_file_older_than_since_without_counting_it()
    {
        var harness = new Harness(Transcript([Turn(index: 0, "fix it", Edit("a.cs", "old", "new"))], modifiedAt: _start.AddDays(-30)), now: _start);

        var result = await harness.Service.Handle(A.ImportTurnsRequest().Files(Path).Since(TimeSpan.FromDays(14)).Build(), _ct);

        Assert.Equal(new ImportTurnsResponse(Imported: 0, Skipped: 0, Unreadable: 0), result.Value);
        Assert.Empty(harness.Store.Items);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ImportTurns_puts_the_goal_of_the_sessions_task_brief_into_the_problem()
    {
        var harness = new Harness(Transcript([Turn(index: 0, "fix it", Edit("a.cs", "old", "new"))]));
        _ = await harness.Briefs.Save(A.Brief().WithToolUseId("task:sess-1").Build(), _ct);

        _ = await harness.Run();

        var input = JsonNode.Parse(Assert.Single(harness.Store.Items).InputJson)!;
        Assert.Equal("Goal: Fix the failing test\n\nfix it", (string?)input["problem"]);
        Assert.Equal("The test passes", (string?)input["acceptanceCriteria"]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ImportTurns_never_stores_a_secret_from_a_transcript()
    {
        var secret = SampleSecrets.AnthropicKey;
        var harness = new Harness(Transcript([Turn(index: 0, $"use {secret} to log in", Edit($"{secret}.cs", $"old {secret}", $"new {secret}"))]));

        _ = await harness.Run();

        var stored = Assert.Single(harness.Store.Items).InputJson;
        Assert.DoesNotContain(secret, stored, StringComparison.Ordinal);
        Assert.Contains(FakeInputRedactor.Placeholder, stored, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ImportTurns_returns_the_store_error_instead_of_counting_the_file_unreadable()
    {
        var harness = new Harness(Transcript([Turn(index: 0, "fix it", Edit("a.cs", "old", "new"))]), unavailable: true);

        var result = await harness.Run();

        Assert.True(result.IsError);
    }

    private static TranscriptTurn Turn(int index, string prompt, params TranscriptEdit[] edits) => new(_session, index, prompt, edits);

    private static TranscriptEdit Edit(string file, string before, string after) => new(file, before, after);

    private static Transcript Transcript(IReadOnlyList<TranscriptTurn> turns, DateTimeOffset? end = null, DateTimeOffset? modifiedAt = null) =>
        new(modifiedAt ?? _start, [], turns, new Dictionary<SessionId, DateTimeOffset> { [_session] = end ?? _start.AddMinutes(30) });

    private sealed class Harness
    {
        public Harness(Transcript? transcript, DateTimeOffset? now = null, bool unavailable = false)
        {
            var reader = new InMemoryTranscriptReader();
            if (transcript is not null)
            {
                _ = reader.Add(_session, Path, transcript);
            }

            Store = new FakeTurnEvalStore { Unavailable = unavailable };
            Service = new ImportTurnsService(Store, reader, Briefs, new FakeInputRedactor(), new SequentialIdGenerator(), new FakeTimeProvider(now ?? _start.AddDays(1)), AwJson.CreateOptions(JudgeJsonContext.Default));
        }

        public FakeTurnEvalStore Store { get; }

        public InMemoryBriefStore Briefs { get; } = new();

        public ImportTurnsService Service { get; }

        public ValueTask<Result<ImportTurnsResponse>> Run() => Service.Handle(A.ImportTurnsRequest().Files(Path).Build(), _ct);
    }
}
