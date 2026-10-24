using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.PromptSort;
using Aw.Kernel;

namespace Aw.Judge.Testing;

/// <summary>The in-memory IPromptSortStore fake; it passes PromptSortStoreContract.</summary>
public sealed class InMemoryPromptSortStore : IPromptSortStore
{
    private static readonly Error _unavailable = new("AW-DATA-002", ErrorKind.Unavailable, "the in-memory prompt-sort store is set unavailable");
    private readonly Dictionary<string, PromptSortRecord> _records = new(StringComparer.Ordinal);

    /// <summary>When true every call returns an error, as a store that can't be opened.</summary>
    public bool Unavailable { get; init; }

    /// <summary>The records saved so far, by decision.</summary>
    public IReadOnlyCollection<PromptSortRecord> Records => _records.Values;

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Save(PromptSortRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (Unavailable)
        {
            return ValueTask.FromResult(Results.Failure<Unit>(_unavailable));
        }

        _records[record.Decision.Value] = record with { Axes = [.. record.Axes.OrderBy(a => a.Axis, StringComparer.Ordinal)] };
        return ValueTask.FromResult(Results.Ok());
    }

    /// <inheritdoc />
    public ValueTask<Result<PromptSortRecord>> Find(DecisionId decision, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            return ValueTask.FromResult(Results.Failure<PromptSortRecord>(_unavailable));
        }

        return ValueTask.FromResult(_records.TryGetValue(decision.Value, out var record)
            ? Results.Success(record)
            : Results.Failure<PromptSortRecord>(JudgeErrors.UnknownDecision.With($"the sorter kept nothing for decision {decision.Value}")));
    }
}
