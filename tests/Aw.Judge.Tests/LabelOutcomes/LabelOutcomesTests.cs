using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Transcripts;
using Aw.Judge.Features.LabelOutcomes;
using Aw.Judge.Testing;
using Aw.Judge.Tests.Transcripts;
using Aw.Kernel;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.LabelOutcomes;

// 'judge label outcomes': an 'ask-check' item is labelled from the next real prompt in the session's transcript.
public sealed class LabelOutcomesTests
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private static readonly DateTimeOffset _decidedAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly SessionId _session = SessionId.From("sess-1");

    [Fact, Trait("Lane", "unit")]
    public async Task LabelOutcomes_labels_nothing_when_there_are_no_ask_check_items()
    {
        var result = await Service(new FakeTurnEvalStore(), new InMemoryTranscriptReader()).Handle(A.LabelOutcomesRequest().Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(new LabelOutcomesResponse(Labeled: 0, NoSignal: 0), result.Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("continue", "no, revert that", "ask")]
    [InlineData("continue", "[Request interrupted by user]", "ask")]
    [InlineData("continue", "great, now add the docs", "continue")]
    [InlineData("ask", "yes", "continue")]
    [InlineData("ask", "use the second option", "ask")]
    public async Task LabelOutcomes_labels_an_item_from_the_next_real_prompt(string decision, string reply, string expected)
    {
        var (store, reader) = With(decision, [new TranscriptPrompt(_decidedAt.AddMinutes(-5), "earlier prompt"), new TranscriptPrompt(_decidedAt.AddMinutes(2), reply)]);

        var result = await Service(store, reader).Handle(A.LabelOutcomesRequest().Build(), _ct);

        Assert.Equal(new LabelOutcomesResponse(Labeled: 1, NoSignal: 0), result.Value);
        var (item, source, label, _) = Assert.Single(store.Labels);
        Assert.Equal((store.Candidates[0].Item, LabelSource.Outcome, expected), (item, source, label));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task LabelOutcomes_gives_no_label_when_the_next_prompt_came_after_six_hours()
    {
        var (store, reader) = With("continue", [new TranscriptPrompt(_decidedAt.AddHours(6).AddSeconds(1), "no")]);

        var result = await Service(store, reader).Handle(A.LabelOutcomesRequest().Build(), _ct);

        Assert.Equal(new LabelOutcomesResponse(Labeled: 0, NoSignal: 1), result.Value);
        Assert.Empty(store.Labels);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task LabelOutcomes_gives_no_label_when_the_session_has_no_transcript_or_the_decision_no_session()
    {
        var store = new FakeTurnEvalStore();
        store.Candidates.Add(new OutcomeCandidate(EvalItemId.From("item-1"), _decidedAt, "continue", _session));
        store.Candidates.Add(new OutcomeCandidate(EvalItemId.From("item-2"), _decidedAt, "continue", Session: null));

        var result = await Service(store, new InMemoryTranscriptReader()).Handle(A.LabelOutcomesRequest().Build(), _ct);

        Assert.Equal(new LabelOutcomesResponse(Labeled: 0, NoSignal: 2), result.Value);
        Assert.Empty(store.Labels);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task LabelOutcomes_reads_each_sessions_transcript_once()
    {
        var (store, inner) = With("ask", [new TranscriptPrompt(_decidedAt.AddMinutes(1), "ok")]);
        store.Candidates.Add(new OutcomeCandidate(EvalItemId.From("item-2"), _decidedAt, "continue", _session));
        var reader = new CountingReader(inner);

        var result = await Service(store, reader).Handle(A.LabelOutcomesRequest().Build(), _ct);

        Assert.Equal(new LabelOutcomesResponse(Labeled: 2, NoSignal: 0), result.Value);
        Assert.Equal(1, reader.Finds);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task LabelOutcomes_returns_the_store_error_when_a_label_cannot_be_written()
    {
        var (store, reader) = With("continue", [new TranscriptPrompt(_decidedAt.AddMinutes(1), "fine")]);
        var failing = new FailingWrites(store);

        var result = await Service(failing, reader).Handle(A.LabelOutcomesRequest().Build(), _ct);

        Assert.True(result.IsError);
    }

    private static LabelOutcomesService Service(IEvalStore store, ITranscriptReader reader) =>
        new(store, reader, new FakeTimeProvider(_decidedAt.AddDays(1)));

    private static (FakeTurnEvalStore Store, InMemoryTranscriptReader Reader) With(string decision, IReadOnlyList<TranscriptPrompt> prompts)
    {
        var store = new FakeTurnEvalStore();
        store.Candidates.Add(new OutcomeCandidate(EvalItemId.From("item-1"), _decidedAt, decision, _session));
        var reader = new InMemoryTranscriptReader().Add(_session, "/p/sess-1.jsonl", new Transcript(_decidedAt, prompts, [], new Dictionary<SessionId, DateTimeOffset>()));
        return (store, reader);
    }

    private sealed class CountingReader(ITranscriptReader inner) : ITranscriptReader
    {
        public int Finds { get; private set; }

        public ValueTask<Result<Transcript>> FindSession(SessionId session, CancellationToken cancellationToken)
        {
            Finds++;
            return inner.FindSession(session, cancellationToken);
        }

        public ValueTask<Result<Transcript>> Read(string path, CancellationToken cancellationToken) => inner.Read(path, cancellationToken);
    }

    // Reads pass through to the wrapped store; every label write fails.
    private sealed class FailingWrites(FakeTurnEvalStore inner) : IEvalStore
    {
        public ValueTask<Result<bool>> AddItem(EvalItem item, CancellationToken cancellationToken) => inner.AddItem(item, cancellationToken);

        public ValueTask<Result<EvalItem>> FindItem(EvalItemId id, CancellationToken cancellationToken) => inner.FindItem(id, cancellationToken);

        public ValueTask<Result<Unit>> RecordLabel(EvalItemId item, LabelSource source, string label, DateTimeOffset at, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Failure<Unit>(new Error("AW-DATA-002", ErrorKind.Unavailable, "the store can't be written")));

        public ValueTask<Result<EvalItem?>> NextUnlabeled(string? question, LabelSource? source, IReadOnlySet<EvalItemId> exclude, CancellationToken cancellationToken) =>
            inner.NextUnlabeled(question, source, exclude, cancellationToken);

        public ValueTask<Result<IReadOnlyList<LabeledItem>>> Labeled(string question, LabelSource? source, CancellationToken cancellationToken) => inner.Labeled(question, source, cancellationToken);

        public ValueTask<Result<IReadOnlyList<LabelCount>>> Counts(CancellationToken cancellationToken) => inner.Counts(cancellationToken);

        public ValueTask<Result<LabelAgreement>> Agreement(string question, CancellationToken cancellationToken) => inner.Agreement(question, cancellationToken);

        public ValueTask<Result<IReadOnlyList<DecidedInput>>> DecidedSince(string? question, DateTimeOffset since, CancellationToken cancellationToken) => inner.DecidedSince(question, since, cancellationToken);

        public ValueTask<Result<IReadOnlyList<OutcomeCandidate>>> OutcomeCandidates(CancellationToken cancellationToken) => inner.OutcomeCandidates(cancellationToken);
    }
}
