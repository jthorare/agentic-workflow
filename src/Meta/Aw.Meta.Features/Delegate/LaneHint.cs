namespace Aw.Meta.Features.Delegate;

/// <summary>A lane `aw-meta delegate` lists when no lane was given.</summary>
/// <param name="Lane">The lane.</param>
/// <param name="Hits">Its keywords (team config delegation.lanes) that occur in the title or the text; empty for none.</param>
public sealed record LaneHint(string Lane, IReadOnlyList<string> Hits);
