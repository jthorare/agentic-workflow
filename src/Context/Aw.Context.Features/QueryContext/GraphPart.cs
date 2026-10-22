using Aw.Context.Core.Packing;
using Aw.Context.Core.Ranking;
using Aw.Context.Core.Results;

namespace Aw.Context.Features.QueryContext;

/// <summary>What the graph adds to an answer: its items, the map's citation, the anchors, the version read and the suggestions.</summary>
/// <param name="Candidates">The graph, affects, decisions and code items, in order.</param>
/// <param name="Cites">The map's citation ('C1'), or none.</param>
/// <param name="Anchors">The anchors' keys.</param>
/// <param name="MapVersion">The map version read ('v1'), or null without a map.</param>
/// <param name="Suggested">Suggested anchors.</param>
internal sealed record GraphPart(IReadOnlyList<PackCandidate> Candidates, IReadOnlyDictionary<string, Citation> Cites, IReadOnlyList<string> Anchors,
    string? MapVersion, IReadOnlyList<string> Suggested)
{
    /// <summary>Intent against intent: checked decision pairs that contradict on a reached node (spec §8.4).</summary>
    internal IReadOnlyList<ResultConflict> Conflicts { get; init; } = [];

    /// <summary>Per decision item key, the key of the record its quote comes from (so the answer can cite that record instead of the map).</summary>
    internal IReadOnlyDictionary<string, string> DecisionRecords { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The keys of the records the answer's nodes are linked to, to tell whether any still waits for decision extraction.</summary>
    internal IReadOnlyList<string> LinkedRecords { get; init; } = [];

    /// <summary>What the map says about each record the answer's nodes are linked to, by the record's canonical key, for the ranker's features.</summary>
    internal IReadOnlyDictionary<string, RecordSignals> RecordSignals { get; init; } = new Dictionary<string, RecordSignals>(StringComparer.Ordinal);

    /// <summary>The code units the component hop skipped for the fan-out cap (scrubbed); empty without the hop.</summary>
    internal IReadOnlyList<SkippedComponent> SkippedComponents { get; init; } = [];

    /// <summary>The diff shape's facts, or null for every other shape.</summary>
    internal DiffPart? Diff { get; init; }

    internal static GraphPart None { get; } = new([], new Dictionary<string, Citation>(StringComparer.Ordinal), [], MapVersion: null, []);
}
