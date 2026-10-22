using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>The profile's 'context:' section as written (spec §8.8): every key optional, unknown keys refused.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ContextSection(
    IReadOnlyList<string>? Repos = null,
    DocsSection? Docs = null,
    string? Deadline = null,
    int? MaxBudget = null,
    int? MinHits = null,
    SessionsSection? Sessions = null,
    SourcesSection? Sources = null,
    ScrubSection? Scrub = null,
    int? CascadeDepth = null,
    int? CascadeComponentFanOut = null,
    LabelingSection? Labeling = null,
    EvalSection? Eval = null,
    CodeSection? Code = null,
    ExtractSection? Extract = null,
    PublishSection? Publish = null,
    RankerSection? Ranker = null);
