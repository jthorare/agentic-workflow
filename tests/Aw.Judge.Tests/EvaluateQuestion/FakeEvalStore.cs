using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Ports;
using Aw.Kernel;

namespace Aw.Judge.Tests.EvaluateQuestion;

/// <summary>An IEvalStore for the eval tests: only the labelled-items read is real, the rest of the port is not used by 'judge eval'.</summary>
internal sealed class FakeEvalStore : IEvalStore
{
    // The strongest label wins: override, then outcome, then adjudicator.
    private static readonly LabelSource[] _strength = [LabelSource.Override, LabelSource.Outcome, LabelSource.Adjudicator];

    private readonly List<(EvalItem Item, LabelSource Source, string Label)> _labels = [];

    /// <summary>Adds an item with one label.</summary>
    internal FakeEvalStore Label(string question, string id, string inputJson, string label, LabelSource source = LabelSource.Outcome)
    {
        var item = _labels.Select(l => l.Item).FirstOrDefault(i => i.Id.Value == id)
            ?? new EvalItem(EvalItemId.From(id), question, inputJson, "decision:" + id, ModelDecision: null, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        _labels.Add((item, source, label));
        return this;
    }

    public ValueTask<Result<IReadOnlyList<LabeledItem>>> Labeled(string question, LabelSource? source, CancellationToken cancellationToken)
    {
        var chosen = _labels
            .Where(l => string.Equals(l.Item.Question, question, StringComparison.Ordinal) && (source is null || l.Source == source) && l.Label != "skip")
            .GroupBy(l => l.Item.Id.Value, StringComparer.Ordinal)
            .Select(g => g.MinBy(l => Array.IndexOf(_strength, l.Source)))
            .Select(l => new LabeledItem(l.Item, l.Label, l.Source))
            .ToList();
        return ValueTask.FromResult(Results.Success<IReadOnlyList<LabeledItem>>(chosen));
    }

    public ValueTask<Result<bool>> AddItem(EvalItem item, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<EvalItem>> FindItem(EvalItemId id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<Unit>> RecordLabel(EvalItemId item, LabelSource source, string label, DateTimeOffset at, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<EvalItem?>> NextUnlabeled(string? question, LabelSource? source, IReadOnlySet<EvalItemId> exclude, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<LabelCount>>> Counts(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<LabelAgreement>> Agreement(string question, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<DecidedInput>>> DecidedSince(string? question, DateTimeOffset since, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<OutcomeCandidate>>> OutcomeCandidates(CancellationToken cancellationToken) => throw new NotSupportedException();
}
