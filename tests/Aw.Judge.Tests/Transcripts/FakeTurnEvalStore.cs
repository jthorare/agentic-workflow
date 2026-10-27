using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Ports;
using Aw.Kernel;

namespace Aw.Judge.Tests.Transcripts;

/// <summary>An IEvalStore for the transcript verbs' tests: it keeps the items and labels written and serves the outcome candidates set; the reads these verbs don't use throw.</summary>
internal sealed class FakeTurnEvalStore : IEvalStore
{
    private static readonly Error _unavailable = new("AW-DATA-002", ErrorKind.Unavailable, "the fake eval store is set unavailable");

    public List<EvalItem> Items { get; } = [];

    public List<(EvalItemId Item, LabelSource Source, string Label, DateTimeOffset At)> Labels { get; } = [];

    public List<OutcomeCandidate> Candidates { get; } = [];

    public bool Unavailable { get; init; }

    public ValueTask<Result<bool>> AddItem(EvalItem item, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            return Done<bool>(_unavailable);
        }

        if (Items.Exists(i => i.Source == item.Source))
        {
            return Done(value: false);
        }

        Items.Add(item);
        return Done(value: true);
    }

    public ValueTask<Result<Unit>> RecordLabel(EvalItemId item, LabelSource source, string label, DateTimeOffset at, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            return Done<Unit>(_unavailable);
        }

        Labels.Add((item, source, label, at));
        return Done(Unit.Value);
    }

    public ValueTask<Result<IReadOnlyList<OutcomeCandidate>>> OutcomeCandidates(CancellationToken cancellationToken) =>
        Unavailable ? Done<IReadOnlyList<OutcomeCandidate>>(_unavailable) : Done<IReadOnlyList<OutcomeCandidate>>([.. Candidates]);

    public ValueTask<Result<EvalItem>> FindItem(EvalItemId id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<EvalItem?>> NextUnlabeled(string? question, LabelSource? source, IReadOnlySet<EvalItemId> exclude, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<LabeledItem>>> Labeled(string question, LabelSource? source, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<LabelCount>>> Counts(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<LabelAgreement>> Agreement(string question, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<DecidedInput>>> DecidedSince(string? question, DateTimeOffset since, CancellationToken cancellationToken) => throw new NotSupportedException();

    private static ValueTask<Result<T>> Done<T>(T value) => ValueTask.FromResult(Results.Success(value));

    private static ValueTask<Result<T>> Done<T>(Error error) => ValueTask.FromResult(Results.Failure<T>(error));
}
