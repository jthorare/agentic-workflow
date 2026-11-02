using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Ports;
using Aw.Kernel;

namespace Aw.Judge.Tests.Adjudicate;

/// <summary>The part of IEvalStore adjudicate uses, in memory: items oldest first, labels per item and source. The other reads are not used here.</summary>
internal sealed class AdjudicateEvalStore : IEvalStore
{
    private readonly List<EvalItem> _items = [];

    /// <summary>The labels given, by item and source.</summary>
    public Dictionary<(EvalItemId Item, LabelSource Source), (string Label, DateTimeOffset At)> Labels { get; } = [];

    /// <summary>Adds an item.</summary>
    public AdjudicateEvalStore With(EvalItem item)
    {
        _items.Add(item);
        return this;
    }

    public ValueTask<Result<bool>> AddItem(EvalItem item, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<EvalItem>> FindItem(EvalItemId id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<Unit>> RecordLabel(EvalItemId item, LabelSource source, string label, DateTimeOffset at, CancellationToken cancellationToken)
    {
        Labels[(item, source)] = (label, at);
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    public ValueTask<Result<EvalItem?>> NextUnlabeled(string? question, LabelSource? source, IReadOnlySet<EvalItemId> exclude, CancellationToken cancellationToken)
    {
        var next = _items.FirstOrDefault(item =>
            (question is null || item.Question == question)
            && !exclude.Contains(item.Id)
            && !Labels.Keys.Any(key => key.Item == item.Id && (source is null || key.Source == source)));
        return ValueTask.FromResult(Results.Success(next));
    }

    public ValueTask<Result<IReadOnlyList<LabeledItem>>> Labeled(string question, LabelSource? source, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<LabelCount>>> Counts(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<LabelAgreement>> Agreement(string question, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<DecidedInput>>> DecidedSince(string? question, DateTimeOffset since, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<OutcomeCandidate>>> OutcomeCandidates(CancellationToken cancellationToken) => throw new NotSupportedException();
}
